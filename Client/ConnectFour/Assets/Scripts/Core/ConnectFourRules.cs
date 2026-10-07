using System.Collections.Generic;

namespace Lyking.ConnectFour.Core
{
    /// <summary>棋盘坐标：列从左到右 0 起，行自底向上 0 起。</summary>
    public readonly struct CellPos
    {
        public CellPos(int col, int row)
        {
            Col = col;
            Row = row;
        }

        /// <summary>列索引（0 为最左列）。</summary>
        public int Col { get; }

        /// <summary>行索引（0 为最底层）。</summary>
        public int Row { get; }
    }

    /// <summary>
    /// 四子连珠规则常量：7 列 6 行棋盘、四连获胜与纯函数判定助手。
    /// 棋盘为 [列, 行] 布局，落子受重力约束：子总是落在该列最低空位。
    /// </summary>
    public static class ConnectFourRules
    {
        /// <summary>棋盘列数。</summary>
        public const int Columns = 7;

        /// <summary>棋盘行数。</summary>
        public const int Rows = 6;

        /// <summary>获胜所需连子数。</summary>
        public const int ConnectLength = 4;

        /// <summary>棋盘总格数。</summary>
        public const int CellCount = Columns * Rows;

        /// <summary>AI 的 minimax 搜索深度。</summary>
        public const int AiSearchDepth = 4;

        private static readonly int[] DirectionCols = { 1, 0, 1, 1 };
        private static readonly int[] DirectionRows = { 0, 1, 1, -1 };

        /// <summary>坐标是否落在棋盘内。</summary>
        public static bool IsInside(int col, int row)
        {
            return col >= 0 && col < Columns && row >= 0 && row < Rows;
        }

        /// <summary>指定列的最低空位行号；越界或满列返回 -1。</summary>
        public static int LowestEmptyRow(Cell[,] cells, int col)
        {
            if (col < 0 || col >= Columns) return -1;
            for (int row = 0; row < Rows; row++)
            {
                if (cells[col, row] == Cell.None) return row;
            }

            return -1;
        }

        /// <summary>扫描全盘返回已连成四连的一方；无四连返回 <see cref="Cell.None"/>。</summary>
        public static Cell FindWinner(Cell[,] cells)
        {
            var line = FindWinningLine(cells);
            return line == null ? Cell.None : cells[line[0].Col, line[0].Row];
        }

        /// <summary>
        /// 返回获胜四连的坐标（按连线方向排列，便于渲染高亮）；
        /// 无四连返回 null。扫描顺序固定，结果完全确定。
        /// </summary>
        public static IReadOnlyList<CellPos>? FindWinningLine(Cell[,] cells)
        {
            for (int col = 0; col < Columns; col++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    var owner = cells[col, row];
                    if (owner == Cell.None) continue;

                    for (int dir = 0; dir < DirectionCols.Length; dir++)
                    {
                        if (HasRunFrom(cells, owner, col, row, DirectionCols[dir], DirectionRows[dir]))
                        {
                            var line = new CellPos[ConnectLength];
                            for (int i = 0; i < ConnectLength; i++)
                            {
                                line[i] = new CellPos(col + DirectionCols[dir] * i, row + DirectionRows[dir] * i);
                            }

                            return line;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>棋盘无空格时为真，用于棋满无胜时的平局判定。</summary>
        public static bool IsFull(Cell[,] cells)
        {
            for (int col = 0; col < Columns; col++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    if (cells[col, row] == Cell.None) return false;
                }
            }

            return true;
        }

        /// <summary>以 (col,row) 为起点沿方向连成四子时为真。</summary>
        private static bool HasRunFrom(Cell[,] cells, Cell owner, int col, int row, int dc, int dr)
        {
            for (int i = 1; i < ConnectLength; i++)
            {
                int nextCol = col + dc * i;
                int nextRow = row + dr * i;
                if (!IsInside(nextCol, nextRow) || cells[nextCol, nextRow] != owner) return false;
            }

            return true;
        }
    }
}
