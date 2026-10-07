using System.Collections.Generic;

namespace Lyking.TicTacToe.Core
{
    /// <summary>
    /// 井字棋规则常量：3x3 棋盘、8 条胜利线与纯函数判定助手。
    /// 格子索引为行优先布局（0 为左上格，从左到右、从上到下编号）。
    /// </summary>
    public static class TicTacToeRules
    {
        /// <summary>棋盘边长：每行每列各 3 格。</summary>
        public const int Size = 3;

        /// <summary>棋盘总格数。</summary>
        public const int CellCount = Size * Size;

        private static readonly IReadOnlyList<int>[] LineBuffer =
        {
            new[] { 0, 1, 2 },
            new[] { 3, 4, 5 },
            new[] { 6, 7, 8 },
            new[] { 0, 3, 6 },
            new[] { 1, 4, 7 },
            new[] { 2, 5, 8 },
            new[] { 0, 4, 8 },
            new[] { 2, 4, 6 },
        };

        /// <summary>全部 8 条胜利线：三行、三列与两条对角线。</summary>
        public static IReadOnlyList<IReadOnlyList<int>> WinLines { get; } = LineBuffer;

        /// <summary>一条线被同方占满时返回该方，否则返回 <see cref="Cell.None"/>。</summary>
        public static Cell LineOwner(IReadOnlyList<Cell> cells, IReadOnlyList<int> line)
        {
            var first = cells[line[0]];
            if (first == Cell.None) return Cell.None;
            return cells[line[1]] == first && cells[line[2]] == first ? first : Cell.None;
        }

        /// <summary>扫描全部胜利线，返回已连成三连的一方；无三连返回 <see cref="Cell.None"/>。</summary>
        public static Cell FindWinner(IReadOnlyList<Cell> cells)
        {
            for (int i = 0; i < WinLines.Count; i++)
            {
                var owner = LineOwner(cells, WinLines[i]);
                if (owner != Cell.None) return owner;
            }

            return Cell.None;
        }

        /// <summary>棋盘无空格时为真，用于棋满无胜时的平局判定。</summary>
        public static bool IsFull(IReadOnlyList<Cell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == Cell.None) return false;
            }

            return true;
        }
    }
}
