using System;

namespace Lyking.Snake.Core
{
    /// <summary>
    /// 贪吃蛇规则常量：棋盘尺寸、初始长度、步进节奏与计分。
    /// 步进间隔随进食数线性缩短并夹在 <see cref="MinStepSeconds"/>，由 Core 提供给表现层驱动。
    /// </summary>
    public sealed class SnakeRules
    {
        public static readonly SnakeRules Default = new SnakeRules(
            width: 24,
            height: 17,
            initialLength: 3,
            baseStepSeconds: 0.14f,
            minStepSeconds: 0.06f,
            stepShrinkPerFood: 0.003f,
            scorePerFood: 10);

        public SnakeRules(
            int width,
            int height,
            int initialLength,
            float baseStepSeconds,
            float minStepSeconds,
            float stepShrinkPerFood,
            int scorePerFood)
        {
            if (width < 3) throw new ArgumentOutOfRangeException(nameof(width), width, "棋盘宽度至少 3。");
            if (height < 3) throw new ArgumentOutOfRangeException(nameof(height), height, "棋盘高度至少 3。");
            if (initialLength < 1) throw new ArgumentOutOfRangeException(nameof(initialLength), initialLength, "初始长度至少 1。");
            if (baseStepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(baseStepSeconds), baseStepSeconds, "步进间隔必须为正。");
            if (minStepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(minStepSeconds), minStepSeconds, "最小步进间隔必须为正。");
            if (stepShrinkPerFood < 0f) throw new ArgumentOutOfRangeException(nameof(stepShrinkPerFood), stepShrinkPerFood, "每食加速量不能为负。");
            if (scorePerFood <= 0) throw new ArgumentOutOfRangeException(nameof(scorePerFood), scorePerFood, "每食得分必须为正。");
            if (initialLength > width * height) throw new ArgumentOutOfRangeException(nameof(initialLength), initialLength, "初始长度不能超过棋盘格数。");
            if (initialLength > width - width / 2) throw new ArgumentOutOfRangeException(nameof(initialLength), initialLength, "初始蛇身必须从中心向两侧完整放入棋盘。");

            Width = width;
            Height = height;
            InitialLength = initialLength;
            BaseStepSeconds = baseStepSeconds;
            MinStepSeconds = minStepSeconds;
            StepShrinkPerFood = stepShrinkPerFood;
            ScorePerFood = scorePerFood;
        }

        public int Width { get; }

        public int Height { get; }

        public int InitialLength { get; }

        public float BaseStepSeconds { get; }

        public float MinStepSeconds { get; }

        public float StepShrinkPerFood { get; }

        public int ScorePerFood { get; }

        public int CellCount => Width * Height;

        /// <summary>进食 <paramref name="foodEaten"/> 次后的当前步进间隔（秒），确定性纯函数。</summary>
        public float StepSecondsFor(int foodEaten)
        {
            if (foodEaten < 0) throw new ArgumentOutOfRangeException(nameof(foodEaten), foodEaten, "进食数不能为负。");
            return Math.Max(MinStepSeconds, BaseStepSeconds - foodEaten * StepShrinkPerFood);
        }
    }
}
