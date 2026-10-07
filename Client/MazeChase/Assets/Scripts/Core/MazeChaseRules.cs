using System;

namespace Lyking.MazeChase.Core
{
    /// <summary>
    /// 吃豆迷宫规则常量：内置 15x11 迷宫、移动速度、惊惧时长、回巢延迟、计分、生命与过关提速。
    /// 过关速度缩放为确定性纯函数，由 Core 提供给表现层与测试。
    /// </summary>
    public sealed class MazeChaseRules
    {
        private static readonly string[] BuiltInMapRows =
        {
            // 15x11 迷宫：四角能量豆、中部两个幽灵巢格、左右两条竖向回路，全部豆子互相连通。
            "###############",
            "#o....#.#....o#",
            "#.###.#.#.###.#",
            "#.....G.G.....#",
            "#.###.#.#.###.#",
            "#.............#",
            "#.###.#.#.###.#",
            "#.....P.......#",
            "#.###.#.#.###.#",
            "#o....#.#....o#",
            "###############",
        };

        /// <summary>默认规则表：内置迷宫、4.5 格/秒玩家、6 秒惊惧与 3 条生命。</summary>
        public static readonly MazeChaseRules Default = new MazeChaseRules(
            new MazeMap(BuiltInMapRows),
            playerSpeed: 4.5f,
            ghostSpeed: 4f,
            powerDurationSeconds: 6f,
            respawnDelaySeconds: 1.5f,
            collisionDistance: 0.6f,
            scorePerPellet: 10,
            scorePerPowerPellet: 50,
            scorePerGhost: 200,
            lives: 3,
            ghostCount: 3,
            levelSpeedGain: 0.1f);

        public MazeChaseRules(
            MazeMap map,
            float playerSpeed,
            float ghostSpeed,
            float powerDurationSeconds,
            float respawnDelaySeconds,
            float collisionDistance,
            int scorePerPellet,
            int scorePerPowerPellet,
            int scorePerGhost,
            int lives,
            int ghostCount,
            float levelSpeedGain)
        {
            if (map is null) throw new ArgumentNullException(nameof(map), "地图不能为空。");
            if (playerSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(playerSpeed), playerSpeed, "玩家速度必须为正。");
            if (ghostSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(ghostSpeed), ghostSpeed, "幽灵速度必须为正。");
            if (powerDurationSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(powerDurationSeconds), powerDurationSeconds, "惊惧时长必须为正。");
            if (respawnDelaySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(respawnDelaySeconds), respawnDelaySeconds, "复活延迟不能为负。");
            if (collisionDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(collisionDistance), collisionDistance, "碰撞距离必须为正。");
            if (scorePerPellet <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerPellet), scorePerPellet, "每豆得分必须为正。");
            if (scorePerPowerPellet <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerPowerPellet), scorePerPowerPellet, "能量豆得分必须为正。");
            if (scorePerGhost <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerGhost), scorePerGhost, "吃幽灵得分必须为正。");
            if (lives < 1) throw new ArgumentOutOfRangeException(nameof(lives), lives, "生命至少为 1。");
            if (ghostCount < 1) throw new ArgumentOutOfRangeException(nameof(ghostCount), ghostCount, "幽灵数量至少为 1。");
            if (levelSpeedGain < 0f) throw new ArgumentOutOfRangeException(nameof(levelSpeedGain), levelSpeedGain, "过关提速比例不能为负。");

            Map = map;
            PlayerSpeed = playerSpeed;
            GhostSpeed = ghostSpeed;
            PowerDurationSeconds = powerDurationSeconds;
            RespawnDelaySeconds = respawnDelaySeconds;
            CollisionDistance = collisionDistance;
            ScorePerPellet = scorePerPellet;
            ScorePerPowerPellet = scorePerPowerPellet;
            ScorePerGhost = scorePerGhost;
            Lives = lives;
            GhostCount = ghostCount;
            LevelSpeedGain = levelSpeedGain;
        }

        public MazeMap Map { get; }

        /// <summary>第 1 关的玩家速度（格/秒）。</summary>
        public float PlayerSpeed { get; }

        /// <summary>第 1 关的幽灵速度（格/秒）。</summary>
        public float GhostSpeed { get; }

        /// <summary>能量豆惊惧时长（秒）。</summary>
        public float PowerDurationSeconds { get; }

        /// <summary>幽灵被吃或复位回巢后的复活延迟（秒）。</summary>
        public float RespawnDelaySeconds { get; }

        /// <summary>玩家与幽灵中心距小于该值（格）即判定碰撞。</summary>
        public float CollisionDistance { get; }

        public int ScorePerPellet { get; }

        public int ScorePerPowerPellet { get; }

        public int ScorePerGhost { get; }

        public int Lives { get; }

        public int GhostCount { get; }

        /// <summary>每过关一关的整体速度提升比例。</summary>
        public float LevelSpeedGain { get; }

        /// <summary>第 <paramref name="level"/> 关的整体速度缩放：每过关提速 <see cref="LevelSpeedGain"/>，确定性纯函数。</summary>
        public float SpeedScaleFor(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            return 1f + (level - 1) * LevelSpeedGain;
        }

        /// <summary>第 <paramref name="level"/> 关的玩家速度（格/秒），确定性纯函数。</summary>
        public float PlayerSpeedFor(int level) => PlayerSpeed * SpeedScaleFor(level);

        /// <summary>第 <paramref name="level"/> 关的幽灵速度（格/秒），确定性纯函数。</summary>
        public float GhostSpeedFor(int level) => GhostSpeed * SpeedScaleFor(level);
    }
}
