using System;

namespace Lyking.Asteroids.Core
{
    /// <summary>
    /// 小行星规则常量：场地尺寸、飞船运动（旋转、推进、阻尼）、子弹、小行星三档半径与分值、波次节奏、生命与重生无敌。
    /// 每波数量与速度均为确定性纯函数，由 Core 提供给表现层与测试。
    /// </summary>
    public sealed class AsteroidsRules
    {
        public static readonly AsteroidsRules Default = new AsteroidsRules(
            width: 24f,
            height: 16f,
            shipRadius: 0.6f,
            shipTurnSpeed: 4f,
            shipThrustAcceleration: 10f,
            shipMaxSpeed: 9f,
            shipDampingPerSecond: 0.45f,
            bulletRadius: 0.15f,
            bulletSpeed: 16f,
            bulletLifetimeSeconds: 1.1f,
            fireCooldownSeconds: 0.3f,
            asteroidLargeRadius: 1.6f,
            asteroidMediumRadius: 1f,
            asteroidSmallRadius: 0.55f,
            asteroidLargeScore: 20,
            asteroidMediumScore: 50,
            asteroidSmallScore: 100,
            asteroidBaseSpeed: 2.4f,
            asteroidSpinRange: 1.5f,
            asteroidsPerWave: 4,
            waveAsteroidGain: 1,
            waveSpeedGain: 0.1f,
            maxLives: 3,
            respawnInvulnerableSeconds: 2.5f);

        public AsteroidsRules(
            float width,
            float height,
            float shipRadius,
            float shipTurnSpeed,
            float shipThrustAcceleration,
            float shipMaxSpeed,
            float shipDampingPerSecond,
            float bulletRadius,
            float bulletSpeed,
            float bulletLifetimeSeconds,
            float fireCooldownSeconds,
            float asteroidLargeRadius,
            float asteroidMediumRadius,
            float asteroidSmallRadius,
            int asteroidLargeScore,
            int asteroidMediumScore,
            int asteroidSmallScore,
            float asteroidBaseSpeed,
            float asteroidSpinRange,
            int asteroidsPerWave,
            int waveAsteroidGain,
            float waveSpeedGain,
            int maxLives,
            float respawnInvulnerableSeconds)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width), width, "场地宽度必须为正。");
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height), height, "场地高度必须为正。");
            if (shipRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(shipRadius), shipRadius, "飞船半径必须为正。");
            if (shipRadius * 2f > width) throw new ArgumentOutOfRangeException(nameof(shipRadius), shipRadius, "飞船直径不能超过场地宽度。");
            if (shipRadius * 2f > height) throw new ArgumentOutOfRangeException(nameof(shipRadius), shipRadius, "飞船直径不能超过场地高度。");
            if (shipTurnSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(shipTurnSpeed), shipTurnSpeed, "飞船转向速度必须为正。");
            if (shipThrustAcceleration <= 0f) throw new ArgumentOutOfRangeException(nameof(shipThrustAcceleration), shipThrustAcceleration, "推进加速度必须为正。");
            if (shipMaxSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(shipMaxSpeed), shipMaxSpeed, "飞船最大速度必须为正。");
            if (shipDampingPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(shipDampingPerSecond), shipDampingPerSecond, "空间阻尼不能为负。");
            if (bulletRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletRadius), bulletRadius, "子弹半径必须为正。");
            if (bulletSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletSpeed), bulletSpeed, "子弹速度必须为正。");
            if (bulletLifetimeSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletLifetimeSeconds), bulletLifetimeSeconds, "子弹寿命必须为正。");
            if (fireCooldownSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(fireCooldownSeconds), fireCooldownSeconds, "射击冷却必须为正。");
            if (asteroidLargeRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(asteroidLargeRadius), asteroidLargeRadius, "大档小行星半径必须为正。");
            if (asteroidMediumRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(asteroidMediumRadius), asteroidMediumRadius, "中档小行星半径必须为正。");
            if (asteroidSmallRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(asteroidSmallRadius), asteroidSmallRadius, "小档小行星半径必须为正。");
            if (asteroidLargeRadius * 2f > width || asteroidLargeRadius * 2f > height)
                throw new ArgumentOutOfRangeException(nameof(asteroidLargeRadius), asteroidLargeRadius, "大档小行星直径不能超过场地。");
            if (asteroidLargeScore <= 0) throw new ArgumentOutOfRangeException(nameof(asteroidLargeScore), asteroidLargeScore, "大档分值必须为正。");
            if (asteroidMediumScore <= 0) throw new ArgumentOutOfRangeException(nameof(asteroidMediumScore), asteroidMediumScore, "中档分值必须为正。");
            if (asteroidSmallScore <= 0) throw new ArgumentOutOfRangeException(nameof(asteroidSmallScore), asteroidSmallScore, "小档分值必须为正。");
            if (asteroidBaseSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(asteroidBaseSpeed), asteroidBaseSpeed, "小行星漂移速度必须为正。");
            if (asteroidSpinRange < 0f) throw new ArgumentOutOfRangeException(nameof(asteroidSpinRange), asteroidSpinRange, "小行星自旋范围不能为负。");
            if (asteroidsPerWave < 1) throw new ArgumentOutOfRangeException(nameof(asteroidsPerWave), asteroidsPerWave, "每波小行星数量至少为 1。");
            if (waveAsteroidGain < 0) throw new ArgumentOutOfRangeException(nameof(waveAsteroidGain), waveAsteroidGain, "每波增量不能为负。");
            if (waveSpeedGain < 0f) throw new ArgumentOutOfRangeException(nameof(waveSpeedGain), waveSpeedGain, "每波提速比例不能为负。");
            if (maxLives < 1) throw new ArgumentOutOfRangeException(nameof(maxLives), maxLives, "最大生命至少为 1。");
            if (respawnInvulnerableSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(respawnInvulnerableSeconds), respawnInvulnerableSeconds, "重生无敌时间不能为负。");

            Width = width;
            Height = height;
            ShipRadius = shipRadius;
            ShipTurnSpeed = shipTurnSpeed;
            ShipThrustAcceleration = shipThrustAcceleration;
            ShipMaxSpeed = shipMaxSpeed;
            ShipDampingPerSecond = shipDampingPerSecond;
            BulletRadius = bulletRadius;
            BulletSpeed = bulletSpeed;
            BulletLifetimeSeconds = bulletLifetimeSeconds;
            FireCooldownSeconds = fireCooldownSeconds;
            AsteroidLargeRadius = asteroidLargeRadius;
            AsteroidMediumRadius = asteroidMediumRadius;
            AsteroidSmallRadius = asteroidSmallRadius;
            AsteroidLargeScore = asteroidLargeScore;
            AsteroidMediumScore = asteroidMediumScore;
            AsteroidSmallScore = asteroidSmallScore;
            AsteroidBaseSpeed = asteroidBaseSpeed;
            AsteroidSpinRange = asteroidSpinRange;
            AsteroidsPerWave = asteroidsPerWave;
            WaveAsteroidGain = waveAsteroidGain;
            WaveSpeedGain = waveSpeedGain;
            MaxLives = maxLives;
            RespawnInvulnerableSeconds = respawnInvulnerableSeconds;
        }

        public float Width { get; }

        public float Height { get; }

        public float ShipRadius { get; }

        /// <summary>飞船转向角速度（弧度/秒）。</summary>
        public float ShipTurnSpeed { get; }

        /// <summary>推进加速度（单位/秒²）。</summary>
        public float ShipThrustAcceleration { get; }

        /// <summary>飞船速度上限（单位/秒）。</summary>
        public float ShipMaxSpeed { get; }

        /// <summary>空间阻尼：每秒速度按比例衰减的系数。</summary>
        public float ShipDampingPerSecond { get; }

        public float BulletRadius { get; }

        public float BulletSpeed { get; }

        public float BulletLifetimeSeconds { get; }

        public float FireCooldownSeconds { get; }

        public float AsteroidLargeRadius { get; }

        public float AsteroidMediumRadius { get; }

        public float AsteroidSmallRadius { get; }

        public int AsteroidLargeScore { get; }

        public int AsteroidMediumScore { get; }

        public int AsteroidSmallScore { get; }

        /// <summary>小行星漂移基础速度（单位/秒），随波次按 <see cref="WaveSpeedGain"/> 提速。</summary>
        public float AsteroidBaseSpeed { get; }

        /// <summary>小行星自旋角速度范围（弧度/秒）：实际自旋在 ±该值内由种子决定。</summary>
        public float AsteroidSpinRange { get; }

        /// <summary>第一波生成的小行星数量。</summary>
        public int AsteroidsPerWave { get; }

        /// <summary>每进入新波额外增加的小行星数量。</summary>
        public int WaveAsteroidGain { get; }

        /// <summary>每进入新波漂移速度的提升比例。</summary>
        public float WaveSpeedGain { get; }

        public int MaxLives { get; }

        /// <summary>被击重生后的无敌时长（秒），无敌期内飞船免疫碰撞。</summary>
        public float RespawnInvulnerableSeconds { get; }

        /// <summary>飞船出生点：场心。</summary>
        public Vec2 ShipSpawnPosition => new Vec2(Width / 2f, Height / 2f);

        /// <summary>小行星出生中心横坐标下限：保证整颗出生在场内。</summary>
        public float MinAsteroidSpawnX => AsteroidLargeRadius;

        /// <summary>小行星出生中心横坐标上限：保证整颗出生在场内。</summary>
        public float MaxAsteroidSpawnX => Width - AsteroidLargeRadius;

        /// <summary>小行星出生中心纵坐标下限：保证整颗出生在场内。</summary>
        public float MinAsteroidSpawnY => AsteroidLargeRadius;

        /// <summary>小行星出生中心纵坐标上限：保证整颗出生在场内。</summary>
        public float MaxAsteroidSpawnY => Height - AsteroidLargeRadius;

        /// <summary>小行星出生点与飞船的最小安全距离，出生重摇以此为准。</summary>
        public float AsteroidSafeSpawnDistance => AsteroidLargeRadius + ShipRadius + 1f;

        /// <summary>第 <paramref name="wave"/> 波的小行星数量：逐波递增，确定性纯函数。</summary>
        public int AsteroidCountFor(int wave)
        {
            if (wave < 1) throw new ArgumentOutOfRangeException(nameof(wave), wave, "波次从 1 开始。");
            return AsteroidsPerWave + (wave - 1) * WaveAsteroidGain;
        }

        /// <summary>第 <paramref name="wave"/> 波的小行星漂移速度（单位/秒），每波提速一次，确定性纯函数。</summary>
        public float AsteroidSpeedFor(int wave)
        {
            if (wave < 1) throw new ArgumentOutOfRangeException(nameof(wave), wave, "波次从 1 开始。");
            return AsteroidBaseSpeed * (1f + (wave - 1) * WaveSpeedGain);
        }

        /// <summary>按档位取小行星半径。</summary>
        public float AsteroidRadiusFor(AsteroidSize size) => size switch
        {
            AsteroidSize.Large => AsteroidLargeRadius,
            AsteroidSize.Medium => AsteroidMediumRadius,
            _ => AsteroidSmallRadius,
        };

        /// <summary>按档位取小行星分值：大 20、中 50、小 100，越小越难打分越高。</summary>
        public int AsteroidScoreFor(AsteroidSize size) => size switch
        {
            AsteroidSize.Large => AsteroidLargeScore,
            AsteroidSize.Medium => AsteroidMediumScore,
            _ => AsteroidSmallScore,
        };
    }
}
