namespace Lyking.FloodIt.Core
{
    /// <summary>
    /// 色彩淹没规则常量：14x14 六色棋盘，25 步内全盘同色即胜利。
    /// 核心与表现层共同引用，保证数值一致。
    /// </summary>
    public static class FloodItRules
    {
        /// <summary>允许的最小棋盘边长。</summary>
        public const int MinBoardSize = 4;

        /// <summary>允许的最大棋盘边长。</summary>
        public const int MaxBoardSize = 30;

        /// <summary>默认棋盘边长。</summary>
        public const int Size = 14;

        /// <summary>棋盘行数。</summary>
        public const int Rows = Size;

        /// <summary>棋盘列数。</summary>
        public const int Columns = Size;

        /// <summary>格子总数：行数 × 列数。</summary>
        public const int CellCount = Rows * Columns;

        /// <summary>颜色种数，格子颜色编号 0 到 ColorCount-1。</summary>
        public const int ColorCount = 6;

        /// <summary>最大步数：步数用尽仍未全盘同色即失败。</summary>
        public const int MaxMoves = 25;
    }
}
