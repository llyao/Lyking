namespace Lyking.Simon.Core
{
    /// <summary>
    /// 西蒙记忆规则常量：4 个色块、点亮时长与间隔、复述输入超时与序列封顶。
    /// 核心与表现层共同引用，保证数值一致。
    /// </summary>
    public static class SimonRules
    {
        /// <summary>色块总数：2x2 四色（红、绿、蓝、黄）。</summary>
        public const int PadCount = 4;

        /// <summary>序列预生成封顶长度：以注入种子的随机数逐项生成后不再增长。</summary>
        public const int MaxSequenceLength = 128;

        /// <summary>单个色块的点亮时长（秒）。</summary>
        public const float LitSeconds = 0.4f;

        /// <summary>相邻两次点亮之间的间隔（秒）。</summary>
        public const float GapSeconds = 0.2f;

        /// <summary>单个色块的完整节奏：点亮 + 间隔。</summary>
        public const float SegmentSeconds = LitSeconds + GapSeconds;

        /// <summary>复述阶段的输入超时（秒），超时未输满即失败。</summary>
        public const float InputTimeoutSeconds = 5.0f;
    }
}
