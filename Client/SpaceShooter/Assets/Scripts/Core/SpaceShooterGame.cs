using System;
using System.Collections.Generic;

namespace Lyking.SpaceShooter.Core
{
    /// <summary>
    /// 太空射击核心战局：确定性连续时域模拟，无 Unity 依赖。
    /// 敌人水平出生位置由注入种子的 <see cref="Random"/> 决定，其余运动全部为确定函数；
    /// <see cref="Step"/> 内按固定 <see cref="SubStepSeconds"/> 子步长推进，运算顺序固定，同种子同输入结果一致。
    /// 玩家在底部横移、按射击沿发射上升子弹；敌人自场顶下落并绕出生锚点正弦横摆；
    /// 子弹命中敌人即消灭加分，敌人撞玩家扣命、落底逃逸即移除，生命耗尽游戏结束，每 10 杀升一波。
    /// </summary>
    public sealed class SpaceShooterGame
    {
        private const int MaxEventLogs = 128;
        private const float TwoPi = 6.2831855f;

        /// <summary>固定物理子步长（秒）：所有推进按该步长的整数次执行，保证确定性。</summary>
        public const float SubStepSeconds = 1f / 120f;

        private readonly ShooterRules _rules;
        private readonly Random _random;
        private readonly List<ShooterBullet> _bullets = new List<ShooterBullet>();
        private readonly List<ShooterEnemy> _enemies = new List<ShooterEnemy>();
        private readonly List<ShooterEventLog> _events = new List<ShooterEventLog>();
        private float _accumulator;
        private float _cooldownRemaining;
        private float _spawnTimer;

        public SpaceShooterGame(ShooterRules? rules = null, int seed = 0)
        {
            _rules = rules ?? ShooterRules.Default;
            _random = new Random(seed);
            Lives = _rules.MaxLives;
            Wave = 1;
            Player = new Vec2(_rules.Width / 2f, _rules.PlayerStartY);
            Log(ShooterEventType.Spawned);
            SpawnEnemy();
        }

        public ShooterRules Rules => _rules;

        public float Width => _rules.Width;

        public float Height => _rules.Height;

        /// <summary>玩家中心位置，纵坐标固定，仅横向移动。</summary>
        public Vec2 Player { get; private set; }

        public int Score { get; private set; }

        public int Lives { get; private set; }

        public int Wave { get; private set; }

        public int Kills { get; private set; }

        /// <summary>战局累计时间（秒），只被子步推进累加。</summary>
        public float Elapsed { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        public IReadOnlyList<ShooterBullet> Bullets => _bullets;

        public IReadOnlyList<ShooterEnemy> Enemies => _enemies;

        public IReadOnlyList<ShooterEventLog> Events => _events;

        /// <summary>
        /// 推进战局：先处理本帧一次射击沿，再把 <paramref name="deltaTime"/> 按固定子步长切分推进。
        /// 射击沿每次调用至多生效一次且受冷却限制；战局结束后调用直接忽略。
        /// </summary>
        public void Step(float deltaTime, in ShooterInput input)
        {
            if (IsFinished) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            if (input.Fire) TryShoot();

            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds && !IsFinished)
            {
                _accumulator -= SubStepSeconds;
                AdvanceSubStep(Math.Clamp(input.MoveX, -1f, 1f));
            }
        }

        /// <summary>推进一个固定子步：计时、移动、生成、命中与受击依次结算，顺序固定。</summary>
        private void AdvanceSubStep(float moveX)
        {
            float dt = SubStepSeconds;
            Elapsed += dt;
            _cooldownRemaining = MathF.Max(0f, _cooldownRemaining - dt);

            Player = new Vec2(Math.Clamp(Player.X + moveX * _rules.PlayerSpeed * dt, _rules.MinPlayerX, _rules.MaxPlayerX), Player.Y);

            _spawnTimer += dt;
            float interval = _rules.SpawnIntervalFor(Wave);
            if (_spawnTimer >= interval)
            {
                _spawnTimer -= interval;
                SpawnEnemy();
            }

            for (int i = 0; i < _bullets.Count; i++)
            {
                _bullets[i] = new ShooterBullet(_bullets[i].Position.WithY(_bullets[i].Position.Y + _rules.BulletSpeed * dt));
            }

            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                double phase = enemy.Phase + TwoPi * _rules.EnemySwayFrequencyHz * dt;
                float x = enemy.SpawnOrigin.X + _rules.EnemySwayAmplitude * MathF.Sin((float)phase);
                _enemies[i] = new ShooterEnemy(new Vec2(x, enemy.Position.Y - _rules.FallSpeedFor(Wave) * dt), enemy.SpawnOrigin, phase);
            }

            // 先整体移除子弹，再按位置结算命中，保持同帧内确定的过滤顺序。
            _bullets.RemoveAll(bullet => bullet.Position.Y - _rules.BulletHalfHeight > _rules.Height);

            for (int b = _bullets.Count - 1; b >= 0; b--)
            {
                int hitIndex = FindHitEnemy(_bullets[b].Position);
                if (hitIndex < 0) continue;

                _bullets.RemoveAt(b);
                _enemies.RemoveAt(hitIndex);
                Score += _rules.ScorePerKill;
                Kills++;
                Log(ShooterEventType.EnemyKilled);
                if (_rules.WaveFor(Kills) > Wave)
                {
                    Wave = _rules.WaveFor(Kills);
                    Log(ShooterEventType.WaveUp);
                }
            }

            for (int e = _enemies.Count - 1; e >= 0; e--)
            {
                var enemy = _enemies[e];
                if (Overlap(enemy.Position, _rules.EnemyHalfWidth, _rules.EnemyHalfHeight, Player, _rules.PlayerHalfWidth, _rules.PlayerHalfHeight))
                {
                    _enemies.RemoveAt(e);
                    Log(ShooterEventType.PlayerHit);
                    Lives--;
                    Log(ShooterEventType.LifeLost);
                    if (Lives <= 0)
                    {
                        IsGameOver = true;
                        Log(ShooterEventType.GameOver);
                        return;
                    }
                }
                else if (enemy.Position.Y < -_rules.EnemyHalfHeight)
                {
                    _enemies.RemoveAt(e);
                    Log(ShooterEventType.EnemyEscaped);
                }
            }
        }

        /// <summary>按冷却闸门发射一颗子弹，冷却未就绪时静默忽略。</summary>
        private void TryShoot()
        {
            if (_cooldownRemaining > 0f) return;

            _cooldownRemaining = _rules.FireCooldownSeconds;
            var muzzle = new Vec2(Player.X, Player.Y + _rules.PlayerHalfHeight + _rules.BulletHalfHeight);
            _bullets.Add(new ShooterBullet(muzzle));
            Log(ShooterEventType.Shot);
        }

        /// <summary>在场顶外随机横坐标生成一名敌人，相位从 0 开始。</summary>
        private void SpawnEnemy()
        {
            float spawnX = _rules.MinEnemySpawnX + (float)_random.NextDouble() * (_rules.MaxEnemySpawnX - _rules.MinEnemySpawnX);
            _enemies.Add(new ShooterEnemy(new Vec2(spawnX, _rules.EnemySpawnY)));
            Log(ShooterEventType.EnemySpawned);
        }

        private int FindHitEnemy(in Vec2 bullet)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (Overlap(bullet, _rules.BulletHalfWidth, _rules.BulletHalfHeight, _enemies[i].Position, _rules.EnemyHalfWidth, _rules.EnemyHalfHeight))
                    return i;
            }

            return -1;
        }

        /// <summary>轴对齐矩形相交判定：中心距在两个半尺寸之和内即相交。</summary>
        private static bool Overlap(in Vec2 a, float aHalfWidth, float aHalfHeight, in Vec2 b, float bHalfWidth, float bHalfHeight)
        {
            return MathF.Abs(a.X - b.X) < aHalfWidth + bHalfWidth
                && MathF.Abs(a.Y - b.Y) < aHalfHeight + bHalfHeight;
        }

        private void Log(ShooterEventType type)
        {
            _events.Add(new ShooterEventLog(Elapsed, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
