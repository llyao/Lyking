using System;

namespace Lyking.EndlessRunner.Core
{
    /// <summary>
    /// 无尽跑酷规则常量：场地尺寸、玩家体型与固定横坐标、重力与跳段、快落倍率、
    /// 障碍高矮尺寸与生成间距、滚动速度增长曲线与计分换算。
    /// 全部为确定性纯数据，由 Core 提供给表现层驱动与绘制。
    /// </summary>
    public sealed class RunnerRules
    {
        public static readonly RunnerRules Default = new RunnerRules(
            fieldWidth: 20f,
            fieldHeight: 10f,
            groundHeight: 0f,
            playerX: 4f,
            playerHalfWidth: 0.4f,
            playerHalfHeight: 0.5f,
            gravity: 20f,
            jumpSpeed: 8f,
            doubleJumpSpeed: 5f,
            maxJumps: 2,
            maxFallSpeed: 12f,
            fastFallMultiplier: 3f,
            shortObstacleWidth: 0.8f,
            shortObstacleHeight: 0.6f,
            tallObstacleWidth: 0.8f,
            tallObstacleHeight: 1.2f,
            baseSpeed: 5f,
            maxSpeed: 9f,
            speedGainPerMeter: 0.01f,
            minSpacing: 4f,
            maxSpacing: 8f,
            tallObstacleChance: 0.5f,
            firstObstacleDistance: 10f,
            scorePerMeter: 10);

        public RunnerRules(
            float fieldWidth,
            float fieldHeight,
            float groundHeight,
            float playerX,
            float playerHalfWidth,
            float playerHalfHeight,
            float gravity,
            float jumpSpeed,
            float doubleJumpSpeed,
            int maxJumps,
            float maxFallSpeed,
            float fastFallMultiplier,
            float shortObstacleWidth,
            float shortObstacleHeight,
            float tallObstacleWidth,
            float tallObstacleHeight,
            float baseSpeed,
            float maxSpeed,
            float speedGainPerMeter,
            float minSpacing,
            float maxSpacing,
            float tallObstacleChance,
            float firstObstacleDistance,
            int scorePerMeter)
        {
            if (fieldWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldWidth), fieldWidth, "场地宽度必须为正。");
            if (fieldHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldHeight), fieldHeight, "场地高度必须为正。");
            if (groundHeight < 0f) throw new ArgumentOutOfRangeException(nameof(groundHeight), groundHeight, "地面高度不能为负。");
            if (groundHeight >= fieldHeight) throw new ArgumentOutOfRangeException(nameof(groundHeight), groundHeight, "地面高度必须低于场地高度。");
            if (playerHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfWidth), playerHalfWidth, "玩家半宽必须为正。");
            if (playerHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfHeight), playerHalfHeight, "玩家半高必须为正。");
            if (playerX < playerHalfWidth || playerX > fieldWidth - playerHalfWidth) throw new ArgumentOutOfRangeException(nameof(playerX), playerX, "玩家横坐标必须让玩家完整留在场地内。");
            if (groundHeight + playerHalfHeight >= fieldHeight) throw new ArgumentOutOfRangeException(nameof(playerHalfHeight), playerHalfHeight, "站立的玩家必须完整落在场地高度内。");
            if (gravity <= 0f) throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "重力加速度必须为正。");
            if (jumpSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(jumpSpeed), jumpSpeed, "起跳初速必须为正。");
            if (doubleJumpSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(doubleJumpSpeed), doubleJumpSpeed, "二段跳初速必须为正。");
            if (doubleJumpSpeed > jumpSpeed) throw new ArgumentOutOfRangeException(nameof(doubleJumpSpeed), doubleJumpSpeed, "二段跳初速不能高于首跳。");
            if (maxJumps < 1) throw new ArgumentOutOfRangeException(nameof(maxJumps), maxJumps, "跳段数至少为 1。");
            if (maxFallSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(maxFallSpeed), maxFallSpeed, "下落速度上限必须为正。");
            if (fastFallMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(fastFallMultiplier), fastFallMultiplier, "快落倍率不能小于 1。");
            if (shortObstacleWidth <= 0f || tallObstacleWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(shortObstacleWidth), shortObstacleWidth, "障碍宽度必须为正。");
            if (shortObstacleHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(shortObstacleHeight), shortObstacleHeight, "矮障碍高度必须为正。");
            if (tallObstacleHeight < shortObstacleHeight) throw new ArgumentOutOfRangeException(nameof(tallObstacleHeight), tallObstacleHeight, "高障碍不能矮于矮障碍。");
            if (tallObstacleHeight > fieldHeight - groundHeight) throw new ArgumentOutOfRangeException(nameof(tallObstacleHeight), tallObstacleHeight, "障碍必须落在地面之上。");
            if (baseSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(baseSpeed), baseSpeed, "初始滚动速度必须为正。");
            if (maxSpeed < baseSpeed) throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "速度上限不能低于初始速度。");
            if (speedGainPerMeter < 0f) throw new ArgumentOutOfRangeException(nameof(speedGainPerMeter), speedGainPerMeter, "每米增速不能为负。");
            if (minSpacing <= 0f) throw new ArgumentOutOfRangeException(nameof(minSpacing), minSpacing, "障碍最小间距必须为正。");
            if (maxSpacing < minSpacing) throw new ArgumentOutOfRangeException(nameof(maxSpacing), maxSpacing, "障碍最大间距不能小于最小间距。");
            if (tallObstacleChance < 0f || tallObstacleChance > 1f) throw new ArgumentOutOfRangeException(nameof(tallObstacleChance), tallObstacleChance, "高障碍概率必须落在 0 到 1 之间。");
            if (firstObstacleDistance < 0f) throw new ArgumentOutOfRangeException(nameof(firstObstacleDistance), firstObstacleDistance, "首个障碍的滚动距离不能为负。");
            if (scorePerMeter <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerMeter), scorePerMeter, "每米得分必须为正。");

            FieldWidth = fieldWidth;
            FieldHeight = fieldHeight;
            GroundHeight = groundHeight;
            PlayerX = playerX;
            PlayerHalfWidth = playerHalfWidth;
            PlayerHalfHeight = playerHalfHeight;
            Gravity = gravity;
            JumpSpeed = jumpSpeed;
            DoubleJumpSpeed = doubleJumpSpeed;
            MaxJumps = maxJumps;
            MaxFallSpeed = maxFallSpeed;
            FastFallMultiplier = fastFallMultiplier;
            ShortObstacleWidth = shortObstacleWidth;
            ShortObstacleHeight = shortObstacleHeight;
            TallObstacleWidth = tallObstacleWidth;
            TallObstacleHeight = tallObstacleHeight;
            BaseSpeed = baseSpeed;
            MaxSpeed = maxSpeed;
            SpeedGainPerMeter = speedGainPerMeter;
            MinSpacing = minSpacing;
            MaxSpacing = maxSpacing;
            TallObstacleChance = tallObstacleChance;
            FirstObstacleDistance = firstObstacleDistance;
            ScorePerMeter = scorePerMeter;
        }

        /// <summary>场地宽度（逻辑坐标，X 向右）。</summary>
        public float FieldWidth { get; }

        /// <summary>场地高度（逻辑坐标，Y 向上）。</summary>
        public float FieldHeight { get; }

        /// <summary>地面高度（Y 坐标），障碍贴地放置、玩家落回此处站稳。</summary>
        public float GroundHeight { get; }

        /// <summary>玩家固定的横坐标（中心）。</summary>
        public float PlayerX { get; }

        public float PlayerHalfWidth { get; }

        public float PlayerHalfHeight { get; }

        /// <summary>重力加速度（每秒下落增速）。</summary>
        public float Gravity { get; }

        /// <summary>地面起跳初速。</summary>
        public float JumpSpeed { get; }

        /// <summary>空中二段跳初速，低于首跳。</summary>
        public float DoubleJumpSpeed { get; }

        /// <summary>落地前可用的跳段总数（地面首跳 + 空中跳）。</summary>
        public int MaxJumps { get; }

        /// <summary>下落速度上限。</summary>
        public float MaxFallSpeed { get; }

        /// <summary>快落倍率：空中按住快落键时重力加速度放大的倍数。</summary>
        public float FastFallMultiplier { get; }

        public float ShortObstacleWidth { get; }

        public float ShortObstacleHeight { get; }

        public float TallObstacleWidth { get; }

        public float TallObstacleHeight { get; }

        /// <summary>初始滚动速度（米/秒），世界向左滚动。</summary>
        public float BaseSpeed { get; }

        /// <summary>滚动速度上限。</summary>
        public float MaxSpeed { get; }

        /// <summary>每滚动 1 米的速度增量。</summary>
        public float SpeedGainPerMeter { get; }

        /// <summary>相邻障碍的最小间距。</summary>
        public float MinSpacing { get; }

        /// <summary>相邻障碍的最大间距。</summary>
        public float MaxSpacing { get; }

        /// <summary>生成高障碍的概率，其余为矮障碍。</summary>
        public float TallObstacleChance { get; }

        /// <summary>开局到首个障碍出现需要的世界滚动距离。</summary>
        public float FirstObstacleDistance { get; }

        /// <summary>每滚动 1 米的得分。</summary>
        public int ScorePerMeter { get; }

        /// <summary>障碍间距的取值跨度，供随机生成使用。</summary>
        public float SpacingRange => MaxSpacing - MinSpacing;

        /// <summary>前方生成线的横坐标：越出场地右缘半个障碍宽，保证出现时不可见。</summary>
        public float SpawnX => FieldWidth + Math.Max(ShortObstacleWidth, TallObstacleWidth) * 0.5f;

        /// <summary>滚动 <paramref name="distance"/> 米后的速度，线性提升并被 <see cref="MaxSpeed"/> 夹住，确定性纯函数。</summary>
        public float SpeedFor(float distance)
        {
            if (distance < 0f) throw new ArgumentOutOfRangeException(nameof(distance), distance, "滚动距离不能为负。");
            return Math.Min(MaxSpeed, BaseSpeed + distance * SpeedGainPerMeter);
        }
    }
}
