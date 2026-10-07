using System;
using System.Collections.Generic;

namespace Lyking.BattleCity.Core
{
    /// <summary>
    /// 坦克大战核心战局：确定性连续时域模拟，无 Unity 依赖。
    /// 敌人出生槽位起点与追击目标由注入种子的 <see cref="Random"/> 决定，转向决策为纯函数；
    /// <see cref="Step"/> 内按固定 <see cref="SubStepSeconds"/> 子步长推进，运算顺序固定，同种子同输入结果一致。
    /// 玩家与敌人坦克在网格地图上连续移动（1 格体宽、四向转向），被钢墙、砖墙、基地、场界或彼此阻挡时停下；
    /// 子弹直线飞行，摧毁砖墙、被钢墙挡下、敌我对消、命中坦克与基地结算；
    /// 玩家中弹失命回出生点并获得无敌期，全歼本关敌人过关（砖墙重置、敌速提升），
    /// 生命耗尽或基地被毁游戏结束。
    /// </summary>
    public sealed class BattleCityGame
    {
        private const int MaxEventLogs = 128;

        /// <summary>固定物理子步长（秒）：所有推进按该步长的整数次执行，保证确定性。</summary>
        public const float SubStepSeconds = 1f / 120f;

        /// <summary>格心对齐与坦克贴边的判定余量：贴着不算重叠。</summary>
        private const float CellEpsilon = 1e-4f;

        private readonly BattleCityRules _rules;
        private readonly BattleCityMap _map;
        private readonly Random _random;
        private readonly List<BattleCityEnemy> _enemies = new List<BattleCityEnemy>();
        private readonly List<BattleCityBullet> _bullets = new List<BattleCityBullet>();
        private readonly List<BattleCityEventLog> _events = new List<BattleCityEventLog>();
        private float _accumulator;
        private float _playerCooldownRemaining;
        private float _invincibleRemaining;
        private float _spawnTimer;
        private int _spawnedThisLevel;
        private int _killedThisLevel;
        private int _nextSpawnSlot;

        public BattleCityGame(BattleCityRules? rules = null, int seed = 0)
        {
            _rules = rules ?? BattleCityRules.Default;
            _map = new BattleCityMap(_rules.MapRows);
            for (int slot = 0; slot < 3; slot++)
            {
                if (_map.IsBlockedForTank(_map.EnemySpawnCell(slot)))
                    throw new ArgumentException("顶部敌人出生格必须为空地。", nameof(rules));
            }

            _random = new Random(seed);
            _nextSpawnSlot = _random.Next(3);
            Lives = _rules.MaxLives;
            Level = 1;
            Player = new BattleCityTank(CellCenter(_map.PlayerSpawn), TankDir.Up);
            Log(BattleCityEventType.Spawned);
            if (TrySpawnEnemy()) _spawnedThisLevel++;
        }

        public BattleCityRules Rules => _rules;

        public BattleCityMap Map => _map;

        /// <summary>玩家坦克：位置与朝向。</summary>
        public BattleCityTank Player { get; private set; }

        public int Score { get; private set; }

        public int Lives { get; private set; }

        public int Level { get; private set; }

        /// <summary>战局累计时间（秒），只被子步推进累加。</summary>
        public float Elapsed { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        public IReadOnlyList<BattleCityEnemy> Enemies => _enemies;

        public IReadOnlyList<BattleCityBullet> Bullets => _bullets;

        public IReadOnlyList<BattleCityEventLog> Events => _events;

        /// <summary>玩家剩余无敌时间（秒），为 0 表示可被命中。</summary>
        public float InvincibleRemaining => _invincibleRemaining;

        /// <summary>本关还需消灭的敌人数。</summary>
        public int EnemiesRemaining => _rules.EnemiesPerLevel - _killedThisLevel;

        /// <summary>
        /// 推进战局：先处理本帧一次射击沿，再把 <paramref name="deltaTime"/> 按固定子步长切分推进。
        /// 射击沿每次调用至多生效一次且受冷却限制；战局结束后调用直接忽略。
        /// </summary>
        public void Step(float deltaTime, in TankInput input)
        {
            if (IsFinished) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            if (input.Fire) TryPlayerShoot();

            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds && !IsFinished)
            {
                _accumulator -= SubStepSeconds;
                AdvanceSubStep(input.MoveDir);
            }
        }

        /// <summary>推进一个固定子步：计时、移动、开火、子弹结算与生成依次进行，顺序固定。</summary>
        private void AdvanceSubStep(TankDir moveDir)
        {
            float dt = SubStepSeconds;
            Elapsed += dt;
            _invincibleRemaining = MathF.Max(0f, _invincibleRemaining - dt);
            _playerCooldownRemaining = MathF.Max(0f, _playerCooldownRemaining - dt);

            // 1. 玩家移动（朝向即转向意图）
            if (moveDir != TankDir.None) MovePlayer(moveDir, dt);

            // 2. 敌人移动与格心决策
            float enemySpeed = _rules.EnemySpeedFor(Level);
            for (int i = 0; i < _enemies.Count; i++)
            {
                _enemies[i] = MoveEnemy(_enemies[i], i, enemySpeed, dt);
            }

            // 3. 敌人按各自冷却开火
            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                float timer = enemy.FireTimer - dt;
                if (timer <= 0f)
                {
                    var muzzle = enemy.Position + enemy.Facing.ToVector() * (_rules.TankHalfSize + _rules.BulletRadius);
                    _bullets.Add(new BattleCityBullet(muzzle, enemy.Facing, fromPlayer: false));
                    Log(BattleCityEventType.EnemyShot);
                    timer = _rules.EnemyFireCooldownSeconds;
                }

                _enemies[i] = enemy.WithFireTimer(timer);
            }

            // 4. 子弹推进
            for (int i = 0; i < _bullets.Count; i++)
            {
                _bullets[i] = _bullets[i].Advanced(_rules.BulletSpeed * dt);
            }

            // 5. 结算：命中敌人、敌我对消、撞场界与墙体、命中玩家，顺序固定
            ResolvePlayerBulletHits();
            ResolveBulletClashes();
            ResolveBulletsAgainstField();
            ResolveEnemyBulletHits();
            if (IsFinished) return;

            // 6. 敌人生成
            TickSpawn(dt);
        }

        /// <summary>玩家转向并移动：被钢墙、砖墙、基地、场界或敌方坦克阻挡时停在阻挡前。</summary>
        private void MovePlayer(TankDir dir, float dt)
        {
            var previous = Player.Position;
            var desired = MoveTank(previous, dir, _rules.PlayerSpeed, dt);
            if (!desired.Equals(previous) && OverlapsAnyEnemy(desired) && !OverlapsAnyEnemy(previous))
            {
                desired = previous;
            }

            Player = new BattleCityTank(desired, dir);
        }

        /// <summary>
        /// 敌人移动与决策：沿朝向移动，被坦克挡住则原地等待；
        /// 跨过格心时对齐到格心并按纯函数重新选向（撞墙贴停的位置恰为格心，天然触发撞墙重选）。
        /// </summary>
        private BattleCityEnemy MoveEnemy(BattleCityEnemy enemy, int enemyIndex, float speed, float dt)
        {
            var previous = enemy.Position;
            var desired = MoveTank(previous, enemy.Facing, speed, dt);
            if (!desired.Equals(previous))
            {
                bool blockedDesired = OverlapsPlayer(desired) || OverlapsAnyEnemyExcept(desired, enemyIndex);
                bool blockedPrevious = OverlapsPlayer(previous) || OverlapsAnyEnemyExcept(previous, enemyIndex);
                if (blockedDesired && !blockedPrevious) desired = previous;
            }

            if (desired.Equals(previous)) return enemy;

            if (TrySnapToCellCenter(previous, desired, enemy.Facing, out var snapped))
            {
                var cell = CellOf(snapped);
                var dir = DecideEnemyDirection(
                    cell,
                    enemy.ChasePlayer ? CellOf(Player.Position) : _map.BaseCell,
                    enemy.Facing,
                    !_map.IsBlockedForTank(cell + TankDir.Up.ToDelta()),
                    !_map.IsBlockedForTank(cell + TankDir.Down.ToDelta()),
                    !_map.IsBlockedForTank(cell + TankDir.Left.ToDelta()),
                    !_map.IsBlockedForTank(cell + TankDir.Right.ToDelta()),
                    _random.NextDouble());
                return new BattleCityEnemy(snapped, dir, enemy.ChasePlayer, enemy.FireTimer);
            }

            return enemy.WithPosition(desired);
        }

        /// <summary>
        /// 敌人 AI 转向决策：纯函数。优先选择朝目标格的主轴方向，次选另一轴向；
        /// 两轴距离相等时按 <paramref name="random01"/> 平局；朝向目标的方向都被阻挡时
        /// 按固定顺序（上、右、下、左）结合 <paramref name="random01"/> 在开放方向中挑选；
        /// 全部阻挡则保持原方向。
        /// </summary>
        public static TankDir DecideEnemyDirection(
            GridPos cell,
            GridPos target,
            TankDir current,
            bool upOpen,
            bool downOpen,
            bool leftOpen,
            bool rightOpen,
            double random01)
        {
            bool IsOpen(TankDir dir) => dir switch
            {
                TankDir.Up => upOpen,
                TankDir.Down => downOpen,
                TankDir.Left => leftOpen,
                TankDir.Right => rightOpen,
                _ => false,
            };

            int dx = target.X - cell.X;
            int dy = target.Y - cell.Y;
            TankDir vertical = dy >= 0 ? TankDir.Up : TankDir.Down;
            TankDir horizontal = dx >= 0 ? TankDir.Right : TankDir.Left;
            (TankDir primary, TankDir secondary) = Math.Abs(dy) >= Math.Abs(dx) ? (vertical, horizontal) : (horizontal, vertical);

            if (Math.Abs(dy) == Math.Abs(dx))
            {
                // 平局：随机决定先试哪个朝向目标的方向
                TankDir first = random01 < 0.5 ? primary : secondary;
                TankDir second = first == primary ? secondary : primary;
                if (IsOpen(first)) return first;
                if (IsOpen(second)) return second;
            }
            else
            {
                if (IsOpen(primary)) return primary;
                if (IsOpen(secondary)) return secondary;
            }

            // 朝向目标的方向都被阻挡：按固定顺序与随机序号在开放方向中挑选
            int openCount = (upOpen ? 1 : 0) + (downOpen ? 1 : 0) + (leftOpen ? 1 : 0) + (rightOpen ? 1 : 0);
            if (openCount == 0) return current;
            int pick = (int)(random01 * openCount) % openCount;
            int seen = 0;
            foreach (var dir in new[] { TankDir.Up, TankDir.Right, TankDir.Down, TankDir.Left })
            {
                if (!IsOpen(dir)) continue;
                if (seen == pick) return dir;
                seen++;
            }

            return current;
        }

        /// <summary>
        /// 坦克沿朝向移动一段距离：先夹到场内，再检查前缘跨入的列/行，
        /// 遇钢墙、砖墙或基地则贴墙停住（贴停位置恰为格心，供敌人决策）。
        /// </summary>
        private Vec2 MoveTank(Vec2 position, TankDir dir, float speed, float dt)
        {
            float half = _rules.TankHalfSize;
            var desired = position + dir.ToVector() * (speed * dt);
            float x = Math.Clamp(desired.X, half, _map.Width - half);
            float y = Math.Clamp(desired.Y, half, _map.Height - half);

            if (dir == TankDir.Left || dir == TankDir.Right)
            {
                int rowMin = FloorIndex(y - half + CellEpsilon);
                int rowMax = FloorIndex(y + half - CellEpsilon);
                int col = FloorIndex(dir == TankDir.Right ? x + half : x - half);
                for (int row = rowMin; row <= rowMax; row++)
                {
                    if (!_map.IsBlockedForTank(new GridPos(col, row))) continue;
                    x = dir == TankDir.Right ? Math.Min(x, col - half) : Math.Max(x, col + 1f + half);
                }
            }
            else if (dir == TankDir.Up || dir == TankDir.Down)
            {
                int colMin = FloorIndex(x - half + CellEpsilon);
                int colMax = FloorIndex(x + half - CellEpsilon);
                int row = FloorIndex(dir == TankDir.Up ? y + half : y - half);
                for (int col = colMin; col <= colMax; col++)
                {
                    if (!_map.IsBlockedForTank(new GridPos(col, row))) continue;
                    y = dir == TankDir.Up ? Math.Min(y, row - half) : Math.Max(y, row + 1f + half);
                }
            }

            return new Vec2(x, y);
        }

        /// <summary>按冷却闸门发射一颗玩家子弹，冷却未就绪时静默忽略。</summary>
        private void TryPlayerShoot()
        {
            if (_playerCooldownRemaining > 0f) return;

            _playerCooldownRemaining = _rules.PlayerFireCooldownSeconds;
            var muzzle = Player.Position + Player.Facing.ToVector() * (_rules.TankHalfSize + _rules.BulletRadius);
            _bullets.Add(new BattleCityBullet(muzzle, Player.Facing, fromPlayer: true));
            Log(BattleCityEventType.Shot);
        }

        /// <summary>玩家子弹命中敌人：消灭计分，本关杀敌数达标即过关。</summary>
        private void ResolvePlayerBulletHits()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                if (!bullet.FromPlayer) continue;
                int hitIndex = FindHitEnemy(bullet.Position);
                if (hitIndex < 0) continue;

                _bullets.RemoveAt(i);
                _enemies.RemoveAt(hitIndex);
                Score += _rules.ScorePerKill;
                _killedThisLevel++;
                Log(BattleCityEventType.EnemyKilled);
                if (_killedThisLevel >= _rules.EnemiesPerLevel) ClearLevel();
            }
        }

        /// <summary>敌我子弹迎面相撞双双对消。</summary>
        private void ResolveBulletClashes()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                int clashIndex = -1;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_bullets[i].FromPlayer == _bullets[j].FromPlayer) continue;
                    if (Overlap(_bullets[i].Position, _rules.BulletRadius, _bullets[j].Position, _rules.BulletRadius))
                    {
                        clashIndex = j;
                        break;
                    }
                }

                if (clashIndex < 0) continue;
                _bullets.RemoveAt(i);
                _bullets.RemoveAt(clashIndex);
                Log(BattleCityEventType.BulletClashed);
                i--; // 连同循环自减共跳过两个已移除位置
            }
        }

        /// <summary>子弹与场界、钢墙、砖墙与基地结算：砖墙被摧毁，钢墙挡下子弹，基地被击中即败。</summary>
        private void ResolveBulletsAgainstField()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                var cell = CellOf(bullet.Position);
                if (!_map.IsInside(cell))
                {
                    _bullets.RemoveAt(i);
                    continue;
                }

                switch (_map.CellAt(cell))
                {
                    case BattleCityCell.Steel:
                        _bullets.RemoveAt(i);
                        break;
                    case BattleCityCell.Brick:
                        _map.TryDestroyBrick(cell);
                        _bullets.RemoveAt(i);
                        Log(BattleCityEventType.BrickDestroyed);
                        break;
                    case BattleCityCell.Base:
                        _bullets.RemoveAt(i);
                        Log(BattleCityEventType.BaseHit);
                        IsGameOver = true;
                        Log(BattleCityEventType.GameOver);
                        return;
                }
            }
        }

        /// <summary>敌人子弹命中玩家：失命回出生点并获得无敌期；生命耗尽游戏结束。</summary>
        private void ResolveEnemyBulletHits()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                if (bullet.FromPlayer) continue;
                if (_invincibleRemaining > 0f) continue;
                if (!Overlap(bullet.Position, _rules.BulletRadius, Player.Position, _rules.TankHalfSize)) continue;

                _bullets.RemoveAt(i);
                Log(BattleCityEventType.PlayerHit);
                Lives--;
                Log(BattleCityEventType.LifeLost);
                if (Lives <= 0)
                {
                    IsGameOver = true;
                    Log(BattleCityEventType.GameOver);
                    return;
                }

                Player = new BattleCityTank(CellCenter(_map.PlayerSpawn), TankDir.Up);
                _invincibleRemaining = _rules.RespawnInvincibleSeconds;
            }
        }

        /// <summary>按生成间隔补足本关敌人：出生格被占时保持到期状态，下个子步重试。</summary>
        private void TickSpawn(float dt)
        {
            if (_spawnedThisLevel >= _rules.EnemiesPerLevel) return;

            _spawnTimer += dt;
            if (_spawnTimer < _rules.SpawnIntervalSeconds) return;
            if (TrySpawnEnemy())
            {
                _spawnedThisLevel++;
                _spawnTimer = 0f;
            }
        }

        /// <summary>从当前槽位出生一名敌人：出生格被占则放弃本次尝试；追击目标由种子决定。</summary>
        private bool TrySpawnEnemy()
        {
            var cell = _map.EnemySpawnCell(_nextSpawnSlot);
            var center = CellCenter(cell);
            if (OverlapsPlayer(center) || OverlapsAnyEnemy(center)) return false;

            _nextSpawnSlot = (_nextSpawnSlot + 1) % 3;
            bool chasePlayer = _random.NextDouble() < 0.5;
            _enemies.Add(new BattleCityEnemy(center, TankDir.Down, chasePlayer, _rules.EnemyFireCooldownSeconds));
            Log(BattleCityEventType.EnemySpawned);
            return true;
        }

        /// <summary>全歼本关敌人：进入下一关、重置砖墙并按新关卡提速。</summary>
        private void ClearLevel()
        {
            Level++;
            _killedThisLevel = 0;
            _spawnedThisLevel = 0;
            _spawnTimer = 0f;
            _map.Reset();
            Log(BattleCityEventType.LevelCleared);
        }

        private int FindHitEnemy(Vec2 position)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (Overlap(position, _rules.BulletRadius, _enemies[i].Position, _rules.TankHalfSize)) return i;
            }

            return -1;
        }

        private bool OverlapsPlayer(Vec2 position) => Overlap(position, _rules.TankHalfSize, Player.Position, _rules.TankHalfSize);

        private bool OverlapsAnyEnemy(Vec2 position)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (Overlap(position, _rules.TankHalfSize, _enemies[i].Position, _rules.TankHalfSize)) return true;
            }

            return false;
        }

        private bool OverlapsAnyEnemyExcept(Vec2 position, int excludeIndex)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (i == excludeIndex) continue;
                if (Overlap(position, _rules.TankHalfSize, _enemies[i].Position, _rules.TankHalfSize)) return true;
            }

            return false;
        }

        /// <summary>检测本子步是否沿移动轴跨过所在格中心；跨越则把坐标对齐到格心，保证转向后仍居格行/列。</summary>
        private static bool TrySnapToCellCenter(Vec2 previous, Vec2 current, TankDir facing, out Vec2 snapped)
        {
            float center;
            bool crossed;
            if (facing == TankDir.Right)
            {
                center = MathF.Floor(current.X) + 0.5f;
                crossed = previous.X < center && center <= current.X;
                snapped = new Vec2(center, current.Y);
            }
            else if (facing == TankDir.Left)
            {
                center = MathF.Floor(current.X) + 0.5f;
                crossed = previous.X > center && center >= current.X;
                snapped = new Vec2(center, current.Y);
            }
            else if (facing == TankDir.Up)
            {
                center = MathF.Floor(current.Y) + 0.5f;
                crossed = previous.Y < center && center <= current.Y;
                snapped = new Vec2(current.X, center);
            }
            else
            {
                center = MathF.Floor(current.Y) + 0.5f;
                crossed = previous.Y > center && center >= current.Y;
                snapped = new Vec2(current.X, center);
            }

            return crossed;
        }

        /// <summary>连续坐标所在的格子：分别向下取整。</summary>
        private static GridPos CellOf(Vec2 position)
            => new GridPos((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));

        /// <summary>格子中心的连续坐标。</summary>
        private static Vec2 CellCenter(GridPos cell) => new Vec2(cell.X + 0.5f, cell.Y + 0.5f);

        private static int FloorIndex(float value) => (int)MathF.Floor(value);

        /// <summary>轴对齐方形相交判定：中心距在两个半尺寸之和内即相交。</summary>
        private static bool Overlap(in Vec2 a, float aHalf, in Vec2 b, float bHalf)
        {
            return MathF.Abs(a.X - b.X) < aHalf + bHalf
                && MathF.Abs(a.Y - b.Y) < aHalf + bHalf;
        }

        private void Log(BattleCityEventType type)
        {
            _events.Add(new BattleCityEventLog(Elapsed, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
