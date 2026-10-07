using System;
using System.Collections.Generic;

namespace Lyking.BattleCity.Core
{
    /// <summary>
    /// 坦克大战规则常量：网格地图、坦克与子弹参数、射击冷却、敌人数量与生成节奏、
    /// 生命、无敌期与计分。敌速随关卡递增为确定性纯函数，由 Core 提供给表现层与测试。
    /// </summary>
    public sealed class BattleCityRules
    {
        private static readonly string[] BuiltInMap =
        {
            // 行从上到下书写：第 1 行是场顶（敌人出生行），最后一行是场底（基地与玩家出生行）。
            ".............",
            ".#..BB.BB..#.",
            ".#..BB.BB..#.",
            "....BB.BB....",
            ".BB.......BB.",
            ".BB.#...#.BB.",
            "....#...#....",
            "BB...BBB...BB",
            "...BBBBBBB...",
            "..B.B.B.B.B..",
            "..B.......B..",
            ".....BBB.....",
            "....PBEB.....",
        };

        /// <summary>默认规则表：内置 13x13 经典布防地图。</summary>
        public static readonly BattleCityRules Default = new BattleCityRules(
            mapRows: BuiltInMap,
            playerSpeed: 3.2f,
            enemyBaseSpeed: 2.6f,
            bulletSpeed: 6f,
            bulletRadius: 0.12f,
            tankHalfSize: 0.5f,
            playerFireCooldownSeconds: 0.5f,
            enemyFireCooldownSeconds: 1.5f,
            enemiesPerLevel: 6,
            spawnIntervalSeconds: 3f,
            maxLives: 3,
            respawnInvincibleSeconds: 2f,
            scorePerKill: 100,
            levelSpeedGain: 0.1f);

        public BattleCityRules(
            IReadOnlyList<string> mapRows,
            float playerSpeed,
            float enemyBaseSpeed,
            float bulletSpeed,
            float bulletRadius,
            float tankHalfSize,
            float playerFireCooldownSeconds,
            float enemyFireCooldownSeconds,
            int enemiesPerLevel,
            float spawnIntervalSeconds,
            int maxLives,
            float respawnInvincibleSeconds,
            int scorePerKill,
            float levelSpeedGain)
        {
            if (mapRows is null || mapRows.Count == 0) throw new ArgumentException("地图行不能为空。", nameof(mapRows));
            if (playerSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(playerSpeed), playerSpeed, "玩家速度必须为正。");
            if (enemyBaseSpeed < 0f) throw new ArgumentOutOfRangeException(nameof(enemyBaseSpeed), enemyBaseSpeed, "敌人基础速度不能为负。");
            if (bulletSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletSpeed), bulletSpeed, "子弹速度必须为正。");
            if (bulletRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(bulletRadius), bulletRadius, "子弹半径必须为正。");
            if (tankHalfSize <= 0f) throw new ArgumentOutOfRangeException(nameof(tankHalfSize), tankHalfSize, "坦克半宽必须为正。");
            if (playerFireCooldownSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(playerFireCooldownSeconds), playerFireCooldownSeconds, "玩家射击冷却必须为正。");
            if (enemyFireCooldownSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(enemyFireCooldownSeconds), enemyFireCooldownSeconds, "敌人射击冷却必须为正。");
            if (enemiesPerLevel < 1) throw new ArgumentOutOfRangeException(nameof(enemiesPerLevel), enemiesPerLevel, "每关敌人数至少为 1。");
            if (spawnIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(spawnIntervalSeconds), spawnIntervalSeconds, "敌人生成间隔必须为正。");
            if (maxLives < 1) throw new ArgumentOutOfRangeException(nameof(maxLives), maxLives, "最大生命至少为 1。");
            if (respawnInvincibleSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(respawnInvincibleSeconds), respawnInvincibleSeconds, "重生无敌期不能为负。");
            if (scorePerKill <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerKill), scorePerKill, "每杀得分必须为正。");
            if (levelSpeedGain < 0f) throw new ArgumentOutOfRangeException(nameof(levelSpeedGain), levelSpeedGain, "每关提速比例不能为负。");

            MapRows = mapRows;
            PlayerSpeed = playerSpeed;
            EnemyBaseSpeed = enemyBaseSpeed;
            BulletSpeed = bulletSpeed;
            BulletRadius = bulletRadius;
            TankHalfSize = tankHalfSize;
            PlayerFireCooldownSeconds = playerFireCooldownSeconds;
            EnemyFireCooldownSeconds = enemyFireCooldownSeconds;
            EnemiesPerLevel = enemiesPerLevel;
            SpawnIntervalSeconds = spawnIntervalSeconds;
            MaxLives = maxLives;
            RespawnInvincibleSeconds = respawnInvincibleSeconds;
            ScorePerKill = scorePerKill;
            LevelSpeedGain = levelSpeedGain;
        }

        /// <summary>内置或自定义地图字符串行，行从上到下书写。</summary>
        public IReadOnlyList<string> MapRows { get; }

        public float PlayerSpeed { get; }

        /// <summary>第 1 关的敌人速度；允许 0，供测试布景使用静止敌人。</summary>
        public float EnemyBaseSpeed { get; }

        public float BulletSpeed { get; }

        public float BulletRadius { get; }

        /// <summary>坦克半宽：1 格体宽对应 0.5。</summary>
        public float TankHalfSize { get; }

        public float PlayerFireCooldownSeconds { get; }

        public float EnemyFireCooldownSeconds { get; }

        /// <summary>每关需要全歼的敌人数量。</summary>
        public int EnemiesPerLevel { get; }

        /// <summary>敌人入场间隔（秒）。</summary>
        public float SpawnIntervalSeconds { get; }

        public int MaxLives { get; }

        /// <summary>重生后的无敌时长（秒）。</summary>
        public float RespawnInvincibleSeconds { get; }

        public int ScorePerKill { get; }

        /// <summary>每关敌人提速比例。</summary>
        public float LevelSpeedGain { get; }

        /// <summary>第 <paramref name="level"/> 关的敌人速度（单位/秒），每关提速一次，确定性纯函数。</summary>
        public float EnemySpeedFor(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            return EnemyBaseSpeed * (1f + (level - 1) * LevelSpeedGain);
        }
    }
}
