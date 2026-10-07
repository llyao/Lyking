using System;
using System.Collections.Generic;

namespace Lyking.Bomberman.Core
{
    /// <summary>
    /// 炸弹人核心战局：连续时钟推进 + 离散格点结算，无 Unity 依赖。
    /// 玩家移动即时生效；炸弹引信、火焰燃烧与敌人游走按时钟推进。
    /// 地图、软墙分布、出口与敌人出生由注入种子确定性生成，敌人中途转向由同一
    /// <see cref="Random"/> 流驱动，同种子同输入序列的模拟结果完全一致。
    /// 炸弹放置后立即阻挡通行（玩家可从其上走离），引信到期爆炸产生十字火焰：
    /// 硬墙挡住该方向，软墙被摧毁并挡住该方向，火焰引燃其他炸弹立即连锁，
    /// 炸死范围内敌人与玩家；敌人全灭且出口显露后，玩家走上出口即过关。
    /// </summary>
    public sealed class BombermanGame
    {
        private const int MaxEventLogs = 128;

        /// <summary>方向枚举顺序：敌人转向候选与火焰延伸均按此顺序结算，保证确定性。</summary>
        private static readonly BombermanDir[] Directions =
        {
            BombermanDir.Up,
            BombermanDir.Down,
            BombermanDir.Left,
            BombermanDir.Right,
        };

        private readonly BombermanRules _rules;
        private readonly int _seed;
        private readonly Random _random;
        private readonly List<BombermanBomb> _bombs = new List<BombermanBomb>();
        private readonly List<BombermanFlame> _flames = new List<BombermanFlame>();
        private readonly List<BombermanEnemy> _enemies = new List<BombermanEnemy>();
        private readonly List<BombermanEventLog> _events = new List<BombermanEventLog>();
        private BombermanTile[,] _tiles;
        private GridPos _exit;
        private GridPos _player;
        private int _level;
        private int _score;
        private int _lives;
        private float _elapsed;
        private bool _exitRevealed;
        private bool _isGameOver;

        public BombermanGame(int seed, BombermanRules? rules = null)
        {
            _rules = rules ?? BombermanRules.Default;
            _seed = seed;
            _random = new Random(seed);
            _lives = _rules.MaxLives;
            _tiles = new BombermanTile[_rules.Height, _rules.Width];
            StartLevel(1);
        }

        public BombermanRules Rules => _rules;

        /// <summary>创建战局时注入的种子。</summary>
        public int Seed => _seed;

        /// <summary>当前关卡序号（从 1 开始）。</summary>
        public int Level => _level;

        public int Width => _rules.Width;

        public int Height => _rules.Height;

        public GridPos Player => _player;

        /// <summary>出口所在格。</summary>
        public GridPos Exit => _exit;

        public int Score => _score;

        public int Lives => _lives;

        /// <summary>战局累计时间（秒）。</summary>
        public float Elapsed => _elapsed;

        /// <summary>出口是否已显露（藏有出口的软墙已被摧毁）。</summary>
        public bool IsExitRevealed => _exitRevealed;

        /// <summary>出口是否已打开：出口显露且敌人全灭，走上出口即过关。</summary>
        public bool IsExitOpen => _exitRevealed && _enemies.Count == 0;

        public bool IsGameOver => _isGameOver;

        public IReadOnlyList<BombermanBomb> Bombs => _bombs;

        public IReadOnlyList<BombermanFlame> Flames => _flames;

        public IReadOnlyList<BombermanEnemy> Enemies => _enemies;

        public IReadOnlyList<BombermanEventLog> Events => _events;

        /// <summary>查询当前地形（软墙被摧毁后变为地板）；网格之外视为硬墙。</summary>
        public BombermanTile TileAt(GridPos pos)
        {
            if (pos.X < 0 || pos.X >= _rules.Width || pos.Y < 0 || pos.Y >= _rules.Height) return BombermanTile.HardWall;
            return _tiles[pos.Y, pos.X];
        }

        public bool IsBombAt(GridPos pos)
        {
            foreach (var bomb in _bombs)
            {
                if (bomb.Position.Equals(pos)) return true;
            }

            return false;
        }

        public bool IsFlameAt(GridPos pos)
        {
            foreach (var flame in _flames)
            {
                if (flame.Position.Equals(pos)) return true;
            }

            return false;
        }

        /// <summary>
        /// 推进战局：先即时结算本帧输入（移动与放弹），再按时钟推进引信、火焰与敌人。
        /// 游戏结束后调用直接忽略。
        /// </summary>
        public void Step(float deltaTime, in BombInput input)
        {
            if (_isGameOver) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            ApplyInput(in input);
            Advance(deltaTime);
        }

        /// <summary>移动即时结算，放置炸弹不受时间影响；过关切换关卡后本帧不再放弹。</summary>
        private void ApplyInput(in BombInput input)
        {
            if (input.MoveDir != BombermanDir.None)
            {
                var target = _player + input.MoveDir.ToDelta();
                if (IsBlocked(target))
                {
                    Log(BombermanEventType.MoveRejected);
                }
                else
                {
                    _player = target;
                    Log(BombermanEventType.Moved);
                    CheckEnemyContact();
                    if (!_isGameOver && TryClearLevel()) return;
                }
            }

            if (input.PlaceBomb) TryPlaceBomb();
        }

        private void Advance(float deltaTime)
        {
            _elapsed += deltaTime;
            AdvanceBombs(deltaTime);
            AdvanceFlames(deltaTime);
            AdvanceEnemies(deltaTime);
            CheckEnemyContact();
        }

        private void AdvanceBombs(float deltaTime)
        {
            var detonations = new Queue<GridPos>();
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                var bomb = new BombermanBomb(_bombs[i].Position, _bombs[i].FuseRemaining - deltaTime);
                if (bomb.FuseRemaining <= 0f)
                {
                    _bombs.RemoveAt(i);
                    detonations.Enqueue(bomb.Position);
                }
                else
                {
                    _bombs[i] = bomb;
                }
            }

            while (detonations.Count > 0) Detonate(detonations.Dequeue(), detonations);
        }

        private void AdvanceFlames(float deltaTime)
        {
            for (int i = _flames.Count - 1; i >= 0; i--)
            {
                float remaining = _flames[i].Remaining - deltaTime;
                if (remaining <= 0f) _flames.RemoveAt(i);
                else _flames[i] = new BombermanFlame(_flames[i].Position, remaining);
            }
        }

        /// <summary>敌人按格点游走：以恒定速度逼近下一格，到格点时优先直行，遇阻按种子随机转向。</summary>
        private void AdvanceEnemies(float deltaTime)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                float progress = enemy.Progress + _rules.EnemySpeed * deltaTime;
                while (progress >= 1f)
                {
                    if (enemy.Direction == BombermanDir.None)
                    {
                        progress = 0f;
                        break;
                    }

                    progress -= 1f;
                    var next = enemy.Position + enemy.Direction.ToDelta();
                    var dir = DecideDirection(next, enemy.Direction);
                    enemy = new BombermanEnemy(next, dir, 0f);
                }

                _enemies[i] = new BombermanEnemy(enemy.Position, enemy.Direction, progress);
            }
        }

        /// <summary>格点决策：前方仍是地板则保持直行（不消耗随机数），否则在可走方向中随机挑选。</summary>
        private BombermanDir DecideDirection(GridPos pos, BombermanDir current)
        {
            if (IsFloorTile(pos + current.ToDelta())) return current;

            var passable = new List<BombermanDir>(4);
            foreach (var dir in Directions)
            {
                if (IsFloorTile(pos + dir.ToDelta())) passable.Add(dir);
            }

            return passable.Count == 0 ? BombermanDir.None : passable[_random.Next(passable.Count)];
        }

        /// <summary>敌人只把地板视作可走，炸弹与其他敌人不阻挡敌人。</summary>
        private bool IsFloorTile(GridPos pos) => TileAt(pos) == BombermanTile.Floor;

        /// <summary>玩家移动目标是否被阻挡：非地板格（硬墙、软墙）或已有炸弹。</summary>
        private bool IsBlocked(GridPos pos) => TileAt(pos) != BombermanTile.Floor || IsBombAt(pos);

        /// <summary>任意敌人与玩家同格即失去一条生命并回到出生点；敌人位置不重置。</summary>
        private void CheckEnemyContact()
        {
            if (_isGameOver) return;

            foreach (var enemy in _enemies)
            {
                if (enemy.Position.Equals(_player))
                {
                    LoseLife();
                    break;
                }
            }
        }

        private void TryPlaceBomb()
        {
            if (_isGameOver || _bombs.Count >= _rules.MaxBombs || IsBombAt(_player)) return;

            _bombs.Add(new BombermanBomb(_player, _rules.BombFuseSeconds));
            Log(BombermanEventType.BombPlaced);
        }

        /// <summary>玩家走上已打开的出口即过关：保留分数与生命，进入软墙密度与敌人递增的下一关。</summary>
        private bool TryClearLevel()
        {
            if (!_exitRevealed || _enemies.Count != 0 || !_player.Equals(_exit)) return false;

            Log(BombermanEventType.LevelCleared);
            StartLevel(_level + 1);
            return true;
        }

        /// <summary>
        /// 爆炸一枚炸弹：生成十字火焰并立即结算伤害。
        /// 硬墙挡住该方向；软墙被摧毁（可能显露出口）并挡住该方向；
        /// 范围内其他炸弹被引燃并入队连锁爆炸，范围内的敌人被消灭，玩家被烧到即失命。
        /// </summary>
        private void Detonate(GridPos center, Queue<GridPos> chain)
        {
            Log(BombermanEventType.Explosion);

            var cells = new List<GridPos> { center };
            foreach (var dir in Directions)
            {
                var delta = dir.ToDelta();
                for (int step = 1; step <= _rules.FlameLength; step++)
                {
                    var cell = center + new GridPos(delta.X * step, delta.Y * step);
                    var tile = TileAt(cell);
                    if (tile == BombermanTile.HardWall) break;

                    cells.Add(cell);
                    if (tile == BombermanTile.SoftWall)
                    {
                        _tiles[cell.Y, cell.X] = BombermanTile.Floor;
                        _score += _rules.ScorePerWall;
                        Log(BombermanEventType.WallDestroyed);
                        if (!_exitRevealed && cell.Equals(_exit))
                        {
                            _exitRevealed = true;
                            Log(BombermanEventType.ExitRevealed);
                        }

                        break;
                    }
                }
            }

            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                if (cells.Contains(_bombs[i].Position))
                {
                    var pos = _bombs[i].Position;
                    _bombs.RemoveAt(i);
                    chain.Enqueue(pos);
                }
            }

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (cells.Contains(_enemies[i].Position))
                {
                    _enemies.RemoveAt(i);
                    _score += _rules.ScorePerKill;
                    Log(BombermanEventType.EnemyKilled);
                }
            }

            if (cells.Contains(_player))
            {
                Log(BombermanEventType.PlayerBurned);
                LoseLife();
            }

            foreach (var cell in cells) _flames.Add(new BombermanFlame(cell, _rules.FlameSeconds));
        }

        private void LoseLife()
        {
            _lives--;
            Log(BombermanEventType.LifeLost);
            if (_lives <= 0)
            {
                _isGameOver = true;
                Log(BombermanEventType.GameOver);
                return;
            }

            _player = _rules.PlayerSpawn;
        }

        /// <summary>装载指定关卡：由种子派生的关卡种子确定性生成地图、出口与敌人，清空炸弹与火焰。</summary>
        private void StartLevel(int level)
        {
            var generated = BombermanLevel.Generate(level, _seed, _rules);
            _level = level;
            _tiles = generated.CloneTiles();
            _exit = generated.Exit;
            _exitRevealed = false;
            _player = generated.PlayerSpawn;
            _bombs.Clear();
            _flames.Clear();
            _enemies.Clear();
            foreach (var spawn in generated.EnemySpawns)
            {
                _enemies.Add(new BombermanEnemy(spawn.Position, spawn.Direction, 0f));
            }

            Log(BombermanEventType.LevelStarted);
        }

        private void Log(BombermanEventType type)
        {
            _events.Add(new BombermanEventLog(_elapsed, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
