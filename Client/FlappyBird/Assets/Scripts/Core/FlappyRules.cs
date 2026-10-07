using System;

namespace Lyking.FlappyBird.Core
{
    /// <summary>
    /// Flappy 规则常量：场地尺寸、重力、扇翅与下落上限、小鸟尺寸、悬停高度、
    /// 管道参数与缺口垂直范围。全部为确定性纯数据，由 Core 提供给表现层驱动与绘制。
    /// </summary>
    public sealed class FlappyRules
    {
        public static readonly FlappyRules Default = new FlappyRules(
            fieldWidth: 20f,
            fieldHeight: 12f,
            gravity: 20f,
            flapSpeed: 6f,
            maxFallSpeed: 10f,
            birdX: 5f,
            birdHalfWidth: 0.25f,
            birdHalfHeight: 0.25f,
            hoverHeight: 6f,
            pipeSpeed: 3.5f,
            pipeWidth: 1f,
            pipeSpacing: 7f,
            gapHeight: 3.5f,
            gapCenterMin: 3f,
            gapCenterMax: 9f,
            firstPipeDistance: 14f);

        public FlappyRules(
            float fieldWidth,
            float fieldHeight,
            float gravity,
            float flapSpeed,
            float maxFallSpeed,
            float birdX,
            float birdHalfWidth,
            float birdHalfHeight,
            float hoverHeight,
            float pipeSpeed,
            float pipeWidth,
            float pipeSpacing,
            float gapHeight,
            float gapCenterMin,
            float gapCenterMax,
            float firstPipeDistance)
        {
            if (fieldWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldWidth), fieldWidth, "场地宽度必须为正。");
            if (fieldHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldHeight), fieldHeight, "场地高度必须为正。");
            if (gravity <= 0f) throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "重力加速度必须为正。");
            if (flapSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(flapSpeed), flapSpeed, "扇翅上升速度必须为正。");
            if (maxFallSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(maxFallSpeed), maxFallSpeed, "下落速度上限必须为正。");
            if (birdHalfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(birdHalfWidth), birdHalfWidth, "小鸟半宽必须为正。");
            if (birdHalfHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(birdHalfHeight), birdHalfHeight, "小鸟半高必须为正。");
            if (birdX < birdHalfWidth || birdX > fieldWidth - birdHalfWidth) throw new ArgumentOutOfRangeException(nameof(birdX), birdX, "小鸟横坐标必须让小鸟完整留在场地内。");
            if (hoverHeight < birdHalfHeight || hoverHeight > fieldHeight - birdHalfHeight) throw new ArgumentOutOfRangeException(nameof(hoverHeight), hoverHeight, "悬停高度必须让小鸟完整留在场地内。");
            if (pipeSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(pipeSpeed), pipeSpeed, "管道水平速度必须为正。");
            if (pipeWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(pipeWidth), pipeWidth, "管道宽度必须为正。");
            if (pipeSpacing <= 0f) throw new ArgumentOutOfRangeException(nameof(pipeSpacing), pipeSpacing, "管道水平间距必须为正。");
            if (gapHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(gapHeight), gapHeight, "缺口高度必须为正。");
            if (gapCenterMin > gapCenterMax) throw new ArgumentOutOfRangeException(nameof(gapCenterMin), gapCenterMin, "缺口中心下限不能超过上限。");
            if (gapCenterMin - gapHeight * 0.5f < 0f || gapCenterMax + gapHeight * 0.5f > fieldHeight) throw new ArgumentOutOfRangeException(nameof(gapHeight), gapHeight, "缺口整体必须落在场地高度内。");
            if (firstPipeDistance < 0f) throw new ArgumentOutOfRangeException(nameof(firstPipeDistance), firstPipeDistance, "首根管道的滚动距离不能为负。");

            FieldWidth = fieldWidth;
            FieldHeight = fieldHeight;
            Gravity = gravity;
            FlapSpeed = flapSpeed;
            MaxFallSpeed = maxFallSpeed;
            BirdX = birdX;
            BirdHalfWidth = birdHalfWidth;
            BirdHalfHeight = birdHalfHeight;
            HoverHeight = hoverHeight;
            PipeSpeed = pipeSpeed;
            PipeWidth = pipeWidth;
            PipeSpacing = pipeSpacing;
            GapHeight = gapHeight;
            GapCenterMin = gapCenterMin;
            GapCenterMax = gapCenterMax;
            FirstPipeDistance = firstPipeDistance;
        }

        /// <summary>场地宽度（逻辑坐标，X 向右）。</summary>
        public float FieldWidth { get; }

        /// <summary>场地高度（逻辑坐标，Y 向上，0 为地面）。</summary>
        public float FieldHeight { get; }

        /// <summary>重力加速度（每秒下落增速）。</summary>
        public float Gravity { get; }

        /// <summary>扇翅上升速度：置顶竖直速度而非叠加。</summary>
        public float FlapSpeed { get; }

        /// <summary>下落速度上限。</summary>
        public float MaxFallSpeed { get; }

        /// <summary>小鸟固定的横坐标。</summary>
        public float BirdX { get; }

        public float BirdHalfWidth { get; }

        public float BirdHalfHeight { get; }

        /// <summary>就绪阶段的悬停高度。</summary>
        public float HoverHeight { get; }

        /// <summary>管道水平速度（世界向左滚动）。</summary>
        public float PipeSpeed { get; }

        /// <summary>管道宽度。</summary>
        public float PipeWidth { get; }

        /// <summary>相邻管道的水平间距。</summary>
        public float PipeSpacing { get; }

        /// <summary>缺口高度。</summary>
        public float GapHeight { get; }

        /// <summary>缺口中心 Y 的下限。</summary>
        public float GapCenterMin { get; }

        /// <summary>缺口中心 Y 的上限。</summary>
        public float GapCenterMax { get; }

        /// <summary>开局到首根管道出现需要的世界滚动距离。</summary>
        public float FirstPipeDistance { get; }

        /// <summary>缺口中心 Y 的取值跨度，供随机生成使用。</summary>
        public float GapCenterRange => GapCenterMax - GapCenterMin;
    }
}
