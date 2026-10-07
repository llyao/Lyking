namespace Lyking.MemoryMatch.Core
{
    /// <summary>
    /// 记忆翻牌规则常量：4x4 共 16 张卡、8 种图案各两张，不配对后的翻回倒计时。
    /// 核心与表现层共同引用，保证数值一致。
    /// </summary>
    public static class MemoryMatchRules
    {
        /// <summary>棋盘行数。</summary>
        public const int Rows = 4;

        /// <summary>棋盘列数。</summary>
        public const int Columns = 4;

        /// <summary>卡牌总数：行数 × 列数。</summary>
        public const int CardCount = Rows * Columns;

        /// <summary>图案种数，每种恰好两张。</summary>
        public const int PatternCount = 8;

        /// <summary>不配对两张卡的翻回倒计时（秒）。</summary>
        public const float FlipBackSeconds = 1.0f;
    }
}
