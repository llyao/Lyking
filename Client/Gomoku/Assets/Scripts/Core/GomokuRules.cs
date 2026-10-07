using System.Collections.Generic;

namespace Lyking.Gomoku.Core
{
    /// <summary>棋盘坐标：行自上而下 0 起，列从左到右 0 起。</summary>
    public readonly struct CellPos
    {
        public CellPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        /// <summary>行索引（0 为最上行）。</summary>
        public int Row { get; }

        /// <summary>列索引（0 为最左列）。</summary>
        public int Col { get; }

        public override string ToString() => $"({Row}, {Col})";
    }

    /// <summary>
    /// 五子棋规则常量：15x15 棋盘、四方向五连判定（长连同样按胜处理）与纯函数判定助手。
    /// 坐标为 [行, 列] 布局，行 0 为最上行，列 0 为最左列。
    /// </summary>
    public static class GomokuRules
    {
        /// <summary>棋盘边长：每行每列各 15 格。</summary>
        public const int Size = 15;

        /// <summary>棋盘总格数。</summary>
        public const int CellCount = Size * Size;

        /// <summary>获胜所需连子数；六连及以上的长连同样判胜（简化规则、无禁手）。</summary>
        public const int WinLength = 5;

        /// <summary>AI 候选点搜索的棋子邻域半径（切比雪夫距离）。</summary>
        public const int AiNeighborRadius = 2;

        /// <summary>天元（棋盘中心）的行列索引。</summary>
        public const int Center = Size / 2;

        private static readonly int[] DirectionRows = { 0, 1, 1, 1 };
        private static readonly int[] DirectionCols = { 1, 1, 0, -1 };

        /// <summary>坐标是否落在棋盘内。</summary>
        public static bool IsInside(int row, int col)
        {
            return row >= 0 && row < Size && col >= 0 && col < Size;
        }

        /// <summary>棋盘无空格时为真，用于棋满无胜时的平局判定。</summary>
        public static bool IsFull(Cell[,] cells)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (cells[row, col] == Cell.None) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 返回获胜连线的坐标（按连线方向排列，便于渲染高亮）；无五连返回 null。
        /// 扫描顺序固定，长连返回完整连线，结果完全确定。
        /// </summary>
        public static IReadOnlyList<CellPos>? FindWinningLine(Cell[,] cells)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    var owner = cells[row, col];
                    if (owner == Cell.None) continue;

                    for (int dir = 0; dir < DirectionRows.Length; dir++)
                    {
                        int run = CountRun(cells, owner, row, col, DirectionRows[dir], DirectionCols[dir]);
                        if (run >= WinLength)
                        {
                            var line = new CellPos[run];
                            for (int i = 0; i < run; i++)
                            {
                                line[i] = new CellPos(row + DirectionRows[dir] * i, col + DirectionCols[dir] * i);
                            }

                            return line;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>以 (row,col) 为起点沿方向的同色连子数（至少为 1）。</summary>
        private static int CountRun(Cell[,] cells, Cell owner, int row, int col, int dr, int dc)
        {
            int run = 1;
            int nextRow = row + dr;
            int nextCol = col + dc;
            while (IsInside(nextRow, nextCol) && cells[nextRow, nextCol] == owner)
            {
                run++;
                nextRow += dr;
                nextCol += dc;
            }

            return run;
        }
    }
}
