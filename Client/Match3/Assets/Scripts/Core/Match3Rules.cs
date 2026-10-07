namespace Lyking.Match3.Core
{
    /// <summary>
    /// 三消规则常量：8x8 网格 6 种宝石、每局 20 步的无尽模式计分表与连锁倍率。
    /// 核心与表现层共同引用，保证数值一致。
    /// </summary>
    public static class Match3Rules
    {
        /// <summary>棋盘行数。</summary>
        public const int Rows = 8;

        /// <summary>棋盘列数。</summary>
        public const int Columns = 8;

        /// <summary>格位总数：行数 × 列数。</summary>
        public const int CellCount = Rows * Columns;

        /// <summary>宝石种数。</summary>
        public const int GemKindCount = 6;

        /// <summary>每局总步数，用尽即结束并结算最终分数。</summary>
        public const int TotalMoves = 20;

        /// <summary>
        /// 单组消除得分：3 连 60、4 连 90、5 连及以上 120；
        /// 同波多组取和后再乘连锁倍率（第 n 波 ×n）。仅对 ≥3 的组长度调用。
        /// </summary>
        public static int ScoreForGroup(int groupLength) => groupLength switch
        {
            3 => 60,
            4 => 90,
            _ => 120,
        };
    }
}
