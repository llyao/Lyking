using System;

namespace Lyking.SpaceShooter.Core
{
    /// <summary>
    /// 太空射击规则常量：场地尺寸、玩家、子弹、敌人、生成节奏、生命与计分。
    /// 生成间隔随波次线性缩短并夹在 <see cref="SpawnMinInterval"/>，敌人下落速度随波次递增，
    /// 均为确定性纯函数，由 Core 提供给表现层与测试。
    /// </summary>
    public sealed class ShooterRules
    {
        public static readonly ShooterRules Default = new ShooterRules(
            width: 20f,
            height: 14f,
            playerHalfWidth: 1f,
            playerHalfHeight: 0.5f,
            playerSpeed: 8f,
            playerStartY: 1f,
            bulletHalfWidth: 0.15f,
            bulletHalfHeight: 0.45f,
            bulletSpeed: 12f,
            fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f,
            enemyHalfHeight: 0.6f,
            enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 1f,
            enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 2f,
            spawnMinInterval: 0.7f,
            spawnShrinkPerWave: 0.15f,
            maxLives: 3,
            scorePerKill: 100,
            killsPerWave: 10,
            waveSpeedGain: 0.1f);

        public ShooterRules(
            float width,
            float height,
            float playerHalfWidth,
            float playerHalfHeight,
            float playerSpeed,
            float playerStartY,
            float bulletHalfWidth,
            float bulletHalfHeight,
            float bulletSpeed,
            float fireCooldownSeconds,
            float enemyHalfWidth,
            float enemyHalfHeight,
            float enemyBaseFallSpeed,
            float enemySwayAmplitude,
            float enemySwayFrequencyHz,
            float spawnBaseInterval,
            float spawnMinInterval,
            float spawnShrinkPerWave,
            int maxLives,
            int scorePerKill,
            int killsPerWave,
            float waveSpeedGain)
        {
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width), width, "场地宽度必须为正。");
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height), height, "场地高度必须为正。");
            if (playerHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfWidth), playerHalfWidth, "玩家半宽必须为正。");
            if (playerHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfHeight), playerHalfHeight, "玩家半高必须为正。");
            if (playerHalfWidth * 2f > width) throw new ArgumentOutOfRangeException(nameof(playerHalfWidth), playerHalfWidth, "玩家宽度不能超过场地。");
            if (playerStartY < playerHalfHeight || playerStartY > height - playerHalfHeight)
                throw new ArgumentOutOfRangeException(nameof(playerStartY), playerStartY, "玩家初始纵坐标必须让玩家完整处于场内。");
            if (playerSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(playerSpeed), playerSpeed, "玩家速度必须为正。");
            if (bulletHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletHalfWidth), bulletHalfWidth, "子弹半宽必须为正。");
            if (bulletHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletHalfHeight), bulletHalfHeight, "子弹半高必须为正。");
            if (bulletSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletSpeed), bulletSpeed, "子弹速度必须为正。");
            if (fireCooldownSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(fireCooldownSeconds), fireCooldownSeconds, "射击冷却必须为正。");
            if (enemyHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(enemyHalfWidth), enemyHalfWidth, "敌人半宽必须为正。");
            if (enemyHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(enemyHalfHeight), enemyHalfHeight, "敌人半高必须为正。");
            if (enemyBaseFallSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(enemyBaseFallSpeed), enemyBaseFallSpeed, "敌人下落速度必须为正。");
            if (enemySwayAmplitude < 0f) throw new ArgumentOutOfRangeException(nameof(enemySwayAmplitude), enemySwayAmplitude, "敌人摆幅不能为负。");
            if (enemySwayFrequencyHz < 0f) throw new ArgumentOutOfRangeException(nameof(enemySwayFrequencyHz), enemySwayFrequencyHz, "敌人摆动频率不能为负。");
            if (enemyHalfWidth * 2f + enemySwayAmplitude * 2f > width)
                throw new ArgumentOutOfRangeException(nameof(enemySwayAmplitude), enemySwayAmplitude, "敌人连同横摆范围必须留在场内。");
            if (spawnBaseInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(spawnBaseInterval), spawnBaseInterval, "生成间隔必须为正。");
            if (spawnMinInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(spawnMinInterval), spawnMinInterval, "最小生成间隔必须为正。");
            if (spawnShrinkPerWave < 0f) throw new ArgumentOutOfRangeException(nameof(spawnShrinkPerWave), spawnShrinkPerWave, "每波缩量不能为负。");
            if (maxLives < 1) throw new ArgumentOutOfRangeException(nameof(maxLives), maxLives, "最大生命至少为 1。");
            if (scorePerKill <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerKill), scorePerKill, "每杀得分必须为正。");
            if (killsPerWave < 1) throw new ArgumentOutOfRangeException(nameof(killsPerWave), killsPerWave, "每波击杀数至少为 1。");
            if (waveSpeedGain < 0f) throw new ArgumentOutOfRangeException(nameof(waveSpeedGain), waveSpeedGain, "每波提速比例不能为负。");

            Width = width;
            Height = height;
            PlayerHalfWidth = playerHalfWidth;
            PlayerHalfHeight = playerHalfHeight;
            PlayerSpeed = playerSpeed;
            PlayerStartY = playerStartY;
            BulletHalfWidth = bulletHalfWidth;
            BulletHalfHeight = bulletHalfHeight;
            BulletSpeed = bulletSpeed;
            FireCooldownSeconds = fireCooldownSeconds;
            EnemyHalfWidth = enemyHalfWidth;
            EnemyHalfHeight = enemyHalfHeight;
            EnemyBaseFallSpeed = enemyBaseFallSpeed;
            EnemySwayAmplitude = enemySwayAmplitude;
            EnemySwayFrequencyHz = enemySwayFrequencyHz;
            SpawnBaseInterval = spawnBaseInterval;
            SpawnMinInterval = spawnMinInterval;
            SpawnShrinkPerWave = spawnShrinkPerWave;
            MaxLives = maxLives;
            ScorePerKill = scorePerKill;
            KillsPerWave = killsPerWave;
            WaveSpeedGain = waveSpeedGain;
        }

        public float Width { get; }

        public float Height { get; }

        public float PlayerHalfWidth { get; }

        public float PlayerHalfHeight { get; }

        public float PlayerSpeed { get; }

        public float PlayerStartY { get; }

        public float BulletHalfWidth { get; }

        public float BulletHalfHeight { get; }

        public float BulletSpeed { get; }

        public float FireCooldownSeconds { get; }

        public float EnemyHalfWidth { get; }

        public float EnemyHalfHeight { get; }

        public float EnemyBaseFallSpeed { get; }

        public float EnemySwayAmplitude { get; }

        public float EnemySwayFrequencyHz { get; }

        public float SpawnBaseInterval { get; }

        public float SpawnMinInterval { get; }

        public float SpawnShrinkPerWave { get; }

        public int MaxLives { get; }

        public int ScorePerKill { get; }

        public int KillsPerWave { get; }

        public float WaveSpeedGain { get; }

        /// <summary>玩家中心横坐标的下限。</summary>
        public float MinPlayerX => PlayerHalfWidth;

        /// <summary>玩家中心横坐标的上限。</summary>
        public float MaxPlayerX => Width - PlayerHalfWidth;

        /// <summary>敌人出生中心横坐标的下限：保证横摆全程不出场地。</summary>
        public float MinEnemySpawnX => EnemyHalfWidth + EnemySwayAmplitude;

        /// <summary>敌人出生中心横坐标的上限：保证横摆全程不出场地。</summary>
        public float MaxEnemySpawnX => Width - EnemyHalfWidth - EnemySwayAmplitude;

        /// <summary>敌人出生中心纵坐标：恰好完整藏在场顶之外。</summary>
        public float EnemySpawnY => Height + EnemyHalfHeight;

        /// <summary>第 <paramref name="wave"/> 波的敌人生成间隔（秒），随波次缩短并被下限夹住，确定性纯函数。</summary>
        public float SpawnIntervalFor(int wave)
        {
            if (wave < 1) throw new ArgumentOutOfRangeException(nameof(wave), wave, "波次从 1 开始。");
            return Math.Max(SpawnMinInterval, SpawnBaseInterval - (wave - 1) * SpawnShrinkPerWave);
        }

        /// <summary>第 <paramref name="wave"/> 波的敌人下落速度（单位/秒），每波提速一次，确定性纯函数。</summary>
        public float FallSpeedFor(int wave)
        {
            if (wave < 1) throw new ArgumentOutOfRangeException(nameof(wave), wave, "波次从 1 开始。");
            return EnemyBaseFallSpeed * (1f + (wave - 1) * WaveSpeedGain);
        }

        /// <summary>累计 <paramref name="kills"/> 次击杀对应的波次：每 <see cref="KillsPerWave"/> 杀升一波。</summary>
        public int WaveFor(int kills)
        {
            if (kills < 0) throw new ArgumentOutOfRangeException(nameof(kills), kills, "击杀数不能为负。");
            return kills / KillsPerWave + 1;
        }
    }
}
