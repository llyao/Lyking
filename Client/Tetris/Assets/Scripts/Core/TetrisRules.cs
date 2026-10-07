using System;

namespace Lyking.Tetris.Core
{
    /// <summary>
    /// 俄罗斯方块规则常量：井尺寸、消行计分表、升级节奏与重力步进间隔。
    /// 步进间隔随等级指数缩短并夹在 <see cref="MinStepSeconds"/>，由 Core 提供给表现层驱动。
    /// </summary>
    public sealed class TetrisRules
    {
        /// <summary>经典 10x20 井的默认规则。</summary>
        public static readonly TetrisRules Default = new TetrisRules(
            width: 10,
            height: 20,
            linesPerLevel: 10,
            baseStepSeconds: 0.8f,
            stepShrinkFactor: 0.85f,
            minStepSeconds: 0.1f,
            clearScores: new[] { 100, 300, 500, 800 });

        public TetrisRules(
            int width,
            int height,
            int linesPerLevel,
            float baseStepSeconds,
            float stepShrinkFactor,
            float minStepSeconds,
            int[] clearScores)
        {
            if (width < 4) throw new ArgumentOutOfRangeException(nameof(width), width, "井宽度至少 4，需容纳 I 方块。");
            if (height < 4) throw new ArgumentOutOfRangeException(nameof(height), height, "井高度至少 4。");
            if (linesPerLevel < 1) throw new ArgumentOutOfRangeException(nameof(linesPerLevel), linesPerLevel, "每级消行数至少 1。");
            if (baseStepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(baseStepSeconds), baseStepSeconds, "步进间隔必须为正。");
            if (stepShrinkFactor <= 0f || stepShrinkFactor > 1f) throw new ArgumentOutOfRangeException(nameof(stepShrinkFactor), stepShrinkFactor, "步进缩短系数必须落在 (0,1] 区间。");
            if (minStepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(minStepSeconds), minStepSeconds, "最小步进间隔必须为正。");
            if (clearScores is null) throw new ArgumentNullException(nameof(clearScores), "消行计分表不能为空。");
            if (clearScores.Length != 4) throw new ArgumentOutOfRangeException(nameof(clearScores), clearScores.Length, "消行计分表必须提供 1 到 4 行的分值。");
            foreach (var score in clearScores)
            {
                if (score <= 0) throw new ArgumentOutOfRangeException(nameof(clearScores), score, "消行分值必须为正。");
            }

            Width = width;
            Height = height;
            LinesPerLevel = linesPerLevel;
            BaseStepSeconds = baseStepSeconds;
            StepShrinkFactor = stepShrinkFactor;
            MinStepSeconds = minStepSeconds;
            ClearScores = (int[])clearScores.Clone();
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>每消多少行升一级。</summary>
        public int LinesPerLevel { get; }

        public float BaseStepSeconds { get; }

        public float StepShrinkFactor { get; }

        public float MinStepSeconds { get; }

        /// <summary>一次消 1 到 4 行的基础分值，实际得分再乘以等级。</summary>
        public int[] ClearScores { get; }

        public int CellCount => Width * Height;

        /// <summary>一次消 <paramref name="lines"/> 行在指定 <paramref name="level"/> 下的得分，确定性纯函数。</summary>
        public int ScoreForLines(int lines, int level)
        {
            if (lines < 1 || lines > ClearScores.Length) throw new ArgumentOutOfRangeException(nameof(lines), lines, "一次消行数必须在 1 到 4 之间。");
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "等级从 1 开始。");
            return ClearScores[lines - 1] * level;
        }

        /// <summary>等级 <paramref name="level"/> 下的重力步进间隔（秒），确定性纯函数。</summary>
        public float StepSecondsFor(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "等级从 1 开始。");
            return Math.Max(MinStepSeconds, (float)(BaseStepSeconds * Math.Pow(StepShrinkFactor, level - 1)));
        }
    }
}
