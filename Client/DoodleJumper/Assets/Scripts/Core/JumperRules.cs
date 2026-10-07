using System;

namespace Lyking.DoodleJumper.Core
{
    /// <summary>水平场界处理方式：夹边停在界内，或环绕从另一侧出现。</summary>
    public enum JumperWallMode
    {
        /// <summary>夹边：到达左右边缘即停住（默认）。</summary>
        Clamp,

        /// <summary>环绕：越出一侧边缘后从另一侧出现。</summary>
        Wrap,
    }

    /// <summary>
    /// 涂鸦跳跃规则常量：场地宽度与镜头视野、玩家体型、重力与弹跳、水平加速与场界模式、
    /// 平台尺寸与垂直间距（随高度微增并被可跳上限约束）、易碎与移动平台比例、
    /// 移动往返参数、坠落判定窗口与计分换算。
    /// 全部为确定性纯数据与纯函数，由 Core 提供给表现层驱动与绘制。
    /// </summary>
    public sealed class JumperRules
    {
        public static readonly JumperRules Default = new JumperRules(
            fieldWidth: 16f,
            viewHeight: 12f,
            cameraAnchor: 1f / 3f,
            gravity: 20f,
            jumpSpeed: 10f,
            maxFallSpeed: 20f,
            fastFallMultiplier: 3f,
            playerHalfWidth: 0.4f,
            playerHalfHeight: 0.5f,
            maxHorizontalSpeed: 4f,
            horizontalAccel: 24f,
            wallMode: JumperWallMode.Clamp,
            platformWidth: 2.4f,
            platformThickness: 0.4f,
            platformGapMin: 1.2f,
            platformGapMax: 2f,
            gapGrowthPerMeter: 0.02f,
            minGapLimit: 1.6f,
            maxGapLimit: 2.2f,
            fragileChance: 0.25f,
            movingChance: 0.25f,
            movingAmplitude: 2f,
            movingAngularSpeed: MathF.PI / 2f,
            movingPhaseStep: MathF.PI / 2f,
            fallDeathWindow: 8f,
            lookAheadMargin: 2f,
            scorePerMeter: 10);

        public JumperRules(
            float fieldWidth,
            float viewHeight,
            float cameraAnchor,
            float gravity,
            float jumpSpeed,
            float maxFallSpeed,
            float fastFallMultiplier,
            float playerHalfWidth,
            float playerHalfHeight,
            float maxHorizontalSpeed,
            float horizontalAccel,
            JumperWallMode wallMode,
            float platformWidth,
            float platformThickness,
            float platformGapMin,
            float platformGapMax,
            float gapGrowthPerMeter,
            float minGapLimit,
            float maxGapLimit,
            float fragileChance,
            float movingChance,
            float movingAmplitude,
            float movingAngularSpeed,
            float movingPhaseStep,
            float fallDeathWindow,
            float lookAheadMargin,
            int scorePerMeter)
        {
            if (fieldWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldWidth), fieldWidth, "场地宽度必须为正。");
            if (viewHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(viewHeight), viewHeight, "镜头视野高度必须为正。");
            if (cameraAnchor < 0f || cameraAnchor > 1f) throw new ArgumentOutOfRangeException(nameof(cameraAnchor), cameraAnchor, "玩家屏幕锚点比例必须落在 0 到 1 之间。");
            if (gravity <= 0f) throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "重力加速度必须为正。");
            if (jumpSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(jumpSpeed), jumpSpeed, "弹跳速度必须为正。");
            if (maxFallSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(maxFallSpeed), maxFallSpeed, "下落速度上限必须为正。");
            if (fastFallMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(fastFallMultiplier), fastFallMultiplier, "快落倍率不能小于 1。");
            if (playerHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfWidth), playerHalfWidth, "玩家半宽必须为正。");
            if (playerHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(playerHalfHeight), playerHalfHeight, "玩家半高必须为正。");
            if (2f * playerHalfWidth > fieldWidth) throw new ArgumentOutOfRangeException(nameof(playerHalfWidth), playerHalfWidth, "玩家宽度不能超过场地宽度。");
            if (maxHorizontalSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHorizontalSpeed), maxHorizontalSpeed, "水平最大速度必须为正。");
            if (horizontalAccel <= 0f) throw new ArgumentOutOfRangeException(nameof(horizontalAccel), horizontalAccel, "水平加速度必须为正。");
            if (platformWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(platformWidth), platformWidth, "平台宽度必须为正。");
            if (2f * platformWidth > fieldWidth) throw new ArgumentOutOfRangeException(nameof(platformWidth), platformWidth, "平台宽度不能超过场地宽度的一半以上余量。");
            if (platformThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(platformThickness), platformThickness, "平台厚度必须为正。");
            if (platformGapMin <= 0f) throw new ArgumentOutOfRangeException(nameof(platformGapMin), platformGapMin, "平台最小垂直间距必须为正。");
            if (platformGapMax < platformGapMin) throw new ArgumentOutOfRangeException(nameof(platformGapMax), platformGapMax, "平台最大垂直间距不能小于最小间距。");
            if (gapGrowthPerMeter < 0f) throw new ArgumentOutOfRangeException(nameof(gapGrowthPerMeter), gapGrowthPerMeter, "间距随高度的增速不能为负。");
            if (minGapLimit < platformGapMin) throw new ArgumentOutOfRangeException(nameof(minGapLimit), minGapLimit, "最小间距上限不能低于初始最小间距。");
            if (maxGapLimit < platformGapMax) throw new ArgumentOutOfRangeException(nameof(maxGapLimit), maxGapLimit, "最大间距上限不能低于初始最大间距。");
            if (maxGapLimit < minGapLimit) throw new ArgumentOutOfRangeException(nameof(maxGapLimit), maxGapLimit, "最大间距上限不能小于最小间距上限。");
            if (maxGapLimit > MaxJumpHeightFor(gravity, jumpSpeed) - platformThickness * 0.5f)
                throw new ArgumentOutOfRangeException(nameof(maxGapLimit), maxGapLimit, "最大间距上限必须保证平台仍可跳上（不超过理论弹跳高度）。");
            if (fragileChance < 0f || movingChance < 0f || fragileChance + movingChance > 1f)
                throw new ArgumentOutOfRangeException(nameof(fragileChance), fragileChance, "易碎与移动平台概率之和必须落在 0 到 1 之间。");
            if (movingChance > 0f && 2f * (platformWidth * 0.5f + movingAmplitude) > fieldWidth)
                throw new ArgumentOutOfRangeException(nameof(movingAmplitude), movingAmplitude, "移动平台的往返范围必须完整留在场地内。");
            if (movingAngularSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(movingAngularSpeed), movingAngularSpeed, "移动平台角速度必须为正。");
            if (fallDeathWindow <= 0f) throw new ArgumentOutOfRangeException(nameof(fallDeathWindow), fallDeathWindow, "坠落判定窗口必须为正。");
            if (lookAheadMargin < 0f) throw new ArgumentOutOfRangeException(nameof(lookAheadMargin), lookAheadMargin, "平台生成前视余量不能为负。");
            if (scorePerMeter <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerMeter), scorePerMeter, "每米得分必须为正。");

            FieldWidth = fieldWidth;
            ViewHeight = viewHeight;
            CameraAnchor = cameraAnchor;
            Gravity = gravity;
            JumpSpeed = jumpSpeed;
            MaxFallSpeed = maxFallSpeed;
            FastFallMultiplier = fastFallMultiplier;
            PlayerHalfWidth = playerHalfWidth;
            PlayerHalfHeight = playerHalfHeight;
            MaxHorizontalSpeed = maxHorizontalSpeed;
            HorizontalAccel = horizontalAccel;
            WallMode = wallMode;
            PlatformWidth = platformWidth;
            PlatformThickness = platformThickness;
            PlatformGapMin = platformGapMin;
            PlatformGapMax = platformGapMax;
            GapGrowthPerMeter = gapGrowthPerMeter;
            MinGapLimit = minGapLimit;
            MaxGapLimit = maxGapLimit;
            FragileChance = fragileChance;
            MovingChance = movingChance;
            MovingAmplitude = movingAmplitude;
            MovingAngularSpeed = movingAngularSpeed;
            MovingPhaseStep = movingPhaseStep;
            FallDeathWindow = fallDeathWindow;
            LookAheadMargin = lookAheadMargin;
            ScorePerMeter = scorePerMeter;
        }

        /// <summary>场地逻辑宽度（X 向右，0 到该值为可活动范围）。</summary>
        public float FieldWidth { get; }

        /// <summary>镜头视野的逻辑高度（垂直可见范围）。</summary>
        public float ViewHeight { get; }

        /// <summary>玩家在屏幕纵向的锚点比例：上升时保持在自屏幕下缘起 1/3 高度附近。</summary>
        public float CameraAnchor { get; }

        /// <summary>重力加速度（每秒下落增速）。</summary>
        public float Gravity { get; }

        /// <summary>踩上可弹平台的弹跳速度（固定值，踩任何可弹平台即置顶）。</summary>
        public float JumpSpeed { get; }

        /// <summary>下落速度上限。</summary>
        public float MaxFallSpeed { get; }

        /// <summary>快落倍率：按住快落键时重力加速度放大的倍数。</summary>
        public float FastFallMultiplier { get; }

        public float PlayerHalfWidth { get; }

        public float PlayerHalfHeight { get; }

        /// <summary>水平移动的最大速度（米/秒）。</summary>
        public float MaxHorizontalSpeed { get; }

        /// <summary>水平速度向目标速度逼近的加速度（每秒变化量）。</summary>
        public float HorizontalAccel { get; }

        /// <summary>水平场界处理方式。</summary>
        public JumperWallMode WallMode { get; }

        public float PlatformWidth { get; }

        public float PlatformThickness { get; }

        /// <summary>相邻平台的初始最小垂直间距（中心到中心）。</summary>
        public float PlatformGapMin { get; }

        /// <summary>相邻平台的初始最大垂直间距（中心到中心）。</summary>
        public float PlatformGapMax { get; }

        /// <summary>垂直间距范围随高度每米的增速。</summary>
        public float GapGrowthPerMeter { get; }

        /// <summary>最小间距随高度增长的上限。</summary>
        public float MinGapLimit { get; }

        /// <summary>最大间距随高度增长的上限，必须不超过理论弹跳高度。</summary>
        public float MaxGapLimit { get; }

        /// <summary>生成易碎平台的概率，其余按概率生成移动平台与普通平台。</summary>
        public float FragileChance { get; }

        /// <summary>生成移动平台的概率。</summary>
        public float MovingChance { get; }

        /// <summary>移动平台往返的振幅（米）。</summary>
        public float MovingAmplitude { get; }

        /// <summary>移动平台往返的角速度（弧度/秒）。</summary>
        public float MovingAngularSpeed { get; }

        /// <summary>移动平台相位随生成序号的步进（弧度），保证确定性。</summary>
        public float MovingPhaseStep { get; }

        /// <summary>坠落判定窗口：玩家底部低于历史最高高度该距离即结束。</summary>
        public float FallDeathWindow { get; }

        /// <summary>平台生成的前视余量：生成到镜头上缘再往上的这段距离。</summary>
        public float LookAheadMargin { get; }

        /// <summary>每爬升 1 米的得分。</summary>
        public int ScorePerMeter { get; }

        /// <summary>平台半宽。</summary>
        public float PlatformHalfWidth => PlatformWidth * 0.5f;

        /// <summary>理论弹跳高度（米）：弹跳速度平方除以两倍重力。</summary>
        public float MaxJumpHeight => MaxJumpHeightFor(Gravity, JumpSpeed);

        /// <summary>镜头目标中心高度：让玩家处于自屏幕下缘 <see cref="CameraAnchor"/> 比例处，纯函数。</summary>
        public float CameraTargetY(float playerY) => playerY + ViewHeight * (0.5f - CameraAnchor);

        /// <summary>给定高度下的最小垂直间距：随高度线性微增并被 <see cref="MinGapLimit"/> 夹住。</summary>
        public float MinGapFor(float height)
        {
            float grown = PlatformGapMin + MathF.Max(0f, height) * GapGrowthPerMeter;
            return Math.Min(MinGapLimit, grown);
        }

        /// <summary>给定高度下的最大垂直间距：随高度线性微增并被 <see cref="MaxGapLimit"/> 夹住，保证始终可跳上。</summary>
        public float MaxGapFor(float height)
        {
            float grown = PlatformGapMax + MathF.Max(0f, height) * GapGrowthPerMeter;
            return Math.Min(MaxGapLimit, grown);
        }

        /// <summary>移动平台在 <paramref name="time"/> 秒时的横坐标：相位由生成序号决定，确定性纯函数。</summary>
        public float MovingPlatformX(float baseX, int index, float time) =>
            baseX + MovingAmplitude * MathF.Sin(index * MovingPhaseStep + MovingAngularSpeed * time);

        /// <summary>普通与易碎平台生成横坐标的下缘：平台完整留在场地内。</summary>
        public float StaticSpawnXMin => PlatformHalfWidth;

        /// <summary>普通与易碎平台生成横坐标的上缘。</summary>
        public float StaticSpawnXMax => FieldWidth - PlatformHalfWidth;

        /// <summary>移动平台生成横坐标下缘：基准横坐标加减振幅后仍完整留在场地内。</summary>
        public float MovingSpawnXMin => PlatformHalfWidth + MovingAmplitude;

        /// <summary>移动平台生成横坐标上缘。</summary>
        public float MovingSpawnXMax => FieldWidth - PlatformHalfWidth - MovingAmplitude;

        private static float MaxJumpHeightFor(float gravity, float jumpSpeed) => jumpSpeed * jumpSpeed / (2f * gravity);
    }
}
