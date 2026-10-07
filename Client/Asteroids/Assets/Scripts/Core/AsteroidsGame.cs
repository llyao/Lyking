using System;
using System.Collections.Generic;

namespace Lyking.Asteroids.Core
{
    /// <summary>
    /// 小行星核心战局：确定性连续时域模拟，无 Unity 依赖。
    /// 小行星出生点、漂移方向与自旋由注入种子的 <see cref="Random"/> 决定，其余运动全部为确定函数；
    /// <see cref="Step"/> 内按固定 <see cref="SubStepSeconds"/> 子步长推进，运算顺序固定，同种子同输入结果一致。
    /// 飞船在场心出生、沿朝向推进带惯性阻尼，全场环形环绕（越界从对侧穿出）；
    /// 大档小行星碎成 2 个中档、中档碎成 2 个小档、小档直接消灭，全灭进入下一波（数量 +1、速度 +10%）；
    /// 飞船被撞失 1 命并场心重生进入无敌期，3 命用尽游戏结束。
    /// </summary>
    public sealed class AsteroidsGame
    {
        private const int MaxEventLogs = 128;
        private const float TwoPi = 6.2831855f;

        /// <summary>固定物理子步长（秒）：所有推进按该步长的整数次执行，保证确定性。</summary>
        public const float SubStepSeconds = 1f / 120f;

        /// <summary>小行星出生点重摇上限：与飞船安全距离不足时最多重摇该次数，保证随机调用次数确定。</summary>
        private const int MaxSpawnRetries = 32;

        private readonly AsteroidsRules _rules;
        private readonly Random _random;
        private readonly List<AsteroidsBullet> _bullets = new List<AsteroidsBullet>();
        private readonly List<AsteroidsAsteroid> _asteroids = new List<AsteroidsAsteroid>();
        private readonly List<AsteroidsEventLog> _events = new List<AsteroidsEventLog>();
        private float _accumulator;
        private float _cooldownRemaining;
        private float _invulnerableRemaining;

        public AsteroidsGame(AsteroidsRules? rules = null, int seed = 0)
        {
            _rules = rules ?? AsteroidsRules.Default;
            _random = new Random(seed);
            Lives = _rules.MaxLives;
            Wave = 1;
            Ship = new AsteroidsShip(_rules.ShipSpawnPosition, new Vec2(0f, 0f), MathF.PI / 2f);
            Log(AsteroidsEventType.GameStarted);
            Log(AsteroidsEventType.WaveStarted);
            SpawnWave();
        }

        public AsteroidsRules Rules => _rules;

        public float Width => _rules.Width;

        public float Height => _rules.Height;

        /// <summary>玩家飞船：位置、速度与朝向。</summary>
        public AsteroidsShip Ship { get; private set; }

        /// <summary>重生无敌剩余时间（秒），归零后恢复碰撞判定。</summary>
        public float InvulnerableRemaining => _invulnerableRemaining;

        /// <summary>飞船当前是否处于无敌期，无敌期内免疫小行星碰撞。</summary>
        public bool IsInvulnerable => _invulnerableRemaining > 0f;

        public int Score { get; private set; }

        public int Lives { get; private set; }

        public int Wave { get; private set; }

        /// <summary>战局累计时间（秒），只被子步推进累加。</summary>
        public float Elapsed { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        public IReadOnlyList<AsteroidsBullet> Bullets => _bullets;

        public IReadOnlyList<AsteroidsAsteroid> Asteroids => _asteroids;

        public IReadOnlyList<AsteroidsEventLog> Events => _events;

        /// <summary>
        /// 推进战局：先处理本帧一次射击按下沿，再把 <paramref name="deltaTime"/> 按固定子步长切分推进。
        /// 射击按下沿每次调用至多生效一次且受冷却限制；战局结束后调用直接忽略。
        /// </summary>
        public void Step(float deltaTime, in AsteroidsInput input)
        {
            if (IsFinished) return;
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时间不能为负。");

            if (input.Fire) TryShoot();

            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds && !IsFinished)
            {
                _accumulator -= SubStepSeconds;
                AdvanceSubStep(Math.Clamp(input.Turn, -1f, 1f), input.Thrust);
            }
        }

        /// <summary>推进一个固定子步：计时、飞船、子弹、小行星、命中与受击依次结算，顺序固定。</summary>
        private void AdvanceSubStep(float turn, bool thrust)
        {
            float dt = SubStepSeconds;
            Elapsed += dt;
            _cooldownRemaining = MathF.Max(0f, _cooldownRemaining - dt);
            _invulnerableRemaining = MathF.Max(0f, _invulnerableRemaining - dt);

            AdvanceShipSubStep(dt, turn, thrust);
            AdvanceBulletsSubStep(dt);
            AdvanceAsteroidsSubStep(dt);
            SettleBulletHits();
            SettleShipCollision();
            if (IsFinished) return;

            // 小行星全灭即进入下一波：数量 +1、漂移速度 +10%。
            if (_asteroids.Count == 0)
            {
                Wave++;
                Log(AsteroidsEventType.WaveStarted);
                SpawnWave();
            }
        }

        /// <summary>推进飞船一个子步：转向、推进、阻尼、限速、位移与环绕，顺序固定。</summary>
        private void AdvanceShipSubStep(float dt, float turn, bool thrust)
        {
            var ship = Ship;
            float heading = ship.Heading - turn * _rules.ShipTurnSpeed * dt;
            var velocity = ship.Velocity;
            if (thrust)
            {
                // 推进沿转向后的朝向加速。
                var forward = new Vec2(MathF.Cos(heading), MathF.Sin(heading));
                velocity += forward * (_rules.ShipThrustAcceleration * dt);
            }

            // 空间阻尼按子步等比衰减速度，再夹到最大速度。
            velocity = velocity * MathF.Max(0f, 1f - _rules.ShipDampingPerSecond * dt);
            float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            if (speed > _rules.ShipMaxSpeed) velocity = velocity * (_rules.ShipMaxSpeed / speed);

            var position = WrapPosition(ship.Position + velocity * dt);
            Ship = new AsteroidsShip(position, velocity, heading);
        }

        /// <summary>推进子弹一个子步：位移环绕，寿命归零即移除。</summary>
        private void AdvanceBulletsSubStep(float dt)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                var moved = new AsteroidsBullet(
                    WrapPosition(bullet.Position + bullet.Velocity * dt), bullet.Velocity, bullet.LifeRemaining - dt);
                if (moved.LifeRemaining <= 0f) _bullets.RemoveAt(i);
                else _bullets[i] = moved;
            }
        }

        /// <summary>推进小行星一个子步：漂移环绕与自旋。</summary>
        private void AdvanceAsteroidsSubStep(float dt)
        {
            for (int i = 0; i < _asteroids.Count; i++)
            {
                var asteroid = _asteroids[i];
                _asteroids[i] = new AsteroidsAsteroid(
                    WrapPosition(asteroid.Position + asteroid.Velocity * dt), asteroid.Velocity, asteroid.Size,
                    asteroid.Rotation + asteroid.Spin * dt, asteroid.Spin);
            }
        }

        /// <summary>结算子弹命中：每颗子弹至多摧毁一颗小行星，命中即按档位碎裂加分。</summary>
        private void SettleBulletHits()
        {
            for (int b = _bullets.Count - 1; b >= 0; b--)
            {
                int hitIndex = FindHitAsteroid(_bullets[b].Position, _rules.BulletRadius);
                if (hitIndex < 0) continue;

                _bullets.RemoveAt(b);
                SplitAsteroid(hitIndex);
            }
        }

        /// <summary>结算飞船受击：无敌期内免疫，被撞失 1 命并场心重生，命尽游戏结束。</summary>
        private void SettleShipCollision()
        {
            if (IsInvulnerable) return;

            int hitIndex = FindHitAsteroid(Ship.Position, _rules.ShipRadius);
            if (hitIndex < 0) return;

            Log(AsteroidsEventType.ShipHit);
            Lives--;
            Log(AsteroidsEventType.LifeLost);
            if (Lives <= 0)
            {
                IsGameOver = true;
                Log(AsteroidsEventType.GameOver);
                return;
            }

            Ship = new AsteroidsShip(_rules.ShipSpawnPosition, new Vec2(0f, 0f), MathF.PI / 2f);
            _invulnerableRemaining = _rules.RespawnInvulnerableSeconds;
            Log(AsteroidsEventType.Respawned);
        }

        /// <summary>摧毁指定小行星并按档位结算：大、中档碎成 2 个下一档，小档直接消灭。</summary>
        private void SplitAsteroid(int index)
        {
            var asteroid = _asteroids[index];
            _asteroids.RemoveAt(index);
            Score += _rules.AsteroidScoreFor(asteroid.Size);
            Log(AsteroidsEventType.AsteroidSplit, asteroid.Size);

            if (asteroid.Size == AsteroidSize.Small) return;

            var childSize = asteroid.Size == AsteroidSize.Large ? AsteroidSize.Medium : AsteroidSize.Small;
            float speed = _rules.AsteroidSpeedFor(Wave);
            for (int i = 0; i < 2; i++)
            {
                float angle = (float)_random.NextDouble() * TwoPi;
                float spin = ((float)_random.NextDouble() * 2f - 1f) * _rules.AsteroidSpinRange;
                var velocity = new Vec2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
                _asteroids.Add(new AsteroidsAsteroid(asteroid.Position, velocity, childSize, asteroid.Rotation, spin));
            }
        }

        /// <summary>按冷却闸门发射一颗子弹，冷却未就绪时静默忽略；子弹沿当前朝向、不继承船速。</summary>
        private void TryShoot()
        {
            if (_cooldownRemaining > 0f) return;

            _cooldownRemaining = _rules.FireCooldownSeconds;
            var ship = Ship;
            var forward = new Vec2(MathF.Cos(ship.Heading), MathF.Sin(ship.Heading));
            var muzzle = ship.Position + forward * (_rules.ShipRadius + _rules.BulletRadius);
            _bullets.Add(new AsteroidsBullet(muzzle, forward * _rules.BulletSpeed, _rules.BulletLifetimeSeconds));
            Log(AsteroidsEventType.Shot);
        }

        /// <summary>生成当前波次的大档小行星：出生点避开飞船、漂移方向与自旋由种子决定。</summary>
        private void SpawnWave()
        {
            float speed = _rules.AsteroidSpeedFor(Wave);
            int count = _rules.AsteroidCountFor(Wave);
            for (int i = 0; i < count; i++) _asteroids.Add(SpawnLargeAsteroid(speed));
        }

        /// <summary>生成一颗大档小行星：出生点重摇至安全距离（上限固定），方向与自旋按种子摇出。</summary>
        private AsteroidsAsteroid SpawnLargeAsteroid(float speed)
        {
            var position = _rules.ShipSpawnPosition;
            for (int attempt = 0; attempt < MaxSpawnRetries; attempt++)
            {
                float x = _rules.MinAsteroidSpawnX + (float)_random.NextDouble() * (_rules.MaxAsteroidSpawnX - _rules.MinAsteroidSpawnX);
                float y = _rules.MinAsteroidSpawnY + (float)_random.NextDouble() * (_rules.MaxAsteroidSpawnY - _rules.MinAsteroidSpawnY);
                position = new Vec2(x, y);
                if (position.DistanceTo(_rules.ShipSpawnPosition) >= _rules.AsteroidSafeSpawnDistance) break;
            }

            float angle = (float)_random.NextDouble() * TwoPi;
            float spin = ((float)_random.NextDouble() * 2f - 1f) * _rules.AsteroidSpinRange;
            var velocity = new Vec2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
            return new AsteroidsAsteroid(position, velocity, AsteroidSize.Large, 0f, spin);
        }

        /// <summary>找出一颗与圆形（中心、半径）相交的小行星，按列表顺序返回首个命中。</summary>
        private int FindHitAsteroid(in Vec2 position, float radius)
        {
            for (int i = 0; i < _asteroids.Count; i++)
            {
                var asteroid = _asteroids[i];
                float sum = radius + _rules.AsteroidRadiusFor(asteroid.Size);
                if (position.DistanceTo(asteroid.Position) < sum) return i;
            }

            return -1;
        }

        /// <summary>环形环绕：任一分量越界即从对侧穿出，纯模运算、确定可测。</summary>
        private float Wrap(float value, float max)
        {
            return value - MathF.Floor(value / max) * max;
        }

        private Vec2 WrapPosition(in Vec2 position)
        {
            return new Vec2(Wrap(position.X, _rules.Width), Wrap(position.Y, _rules.Height));
        }

        private void Log(AsteroidsEventType type, AsteroidSize size = AsteroidSize.Large)
        {
            _events.Add(new AsteroidsEventLog(Elapsed, type, size));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
