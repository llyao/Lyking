using System;

namespace Lyking.Gomoku.Core
{
    /// <summary>
    /// 五子棋 AI：不做全量搜索（15x15 过大），改用启发式评分——候选点限于已有棋子
    /// 邻域（半径 <see cref="GomokuRules.AiNeighborRadius"/>）内的空格，按
    /// 己方成五 100000 &gt; 阻对方成五 50000 &gt; 己方活四/冲四 10000 &gt; 阻对方四 5000 &gt;
    /// 己方活三 1000 &gt; 阻对方活三 500 &gt; 其他 1 + 位置中心偏好 逐档评分，
    /// 沿四方向数连续子与两端开放性识别棋型。完全确定：并列最优时取行列字典序最小落点。
    /// </summary>
    public static class GomokuAi
    {
        /// <summary>己方落此立即成五（含长连）的制胜分。</summary>
        public const int OwnFiveScore = 100000;

        /// <summary>该点被对方落子即成五，必须堵截的分。</summary>
        public const int BlockFiveScore = 50000;

        /// <summary>己方落此形成活四或冲四的分。</summary>
        public const int OwnFourScore = 10000;

        /// <summary>该点被对方落子形成四（活四或冲四），抢先占位的分。</summary>
        public const int BlockFourScore = 5000;

        /// <summary>己方落此形成活三的分。</summary>
        public const int OwnThreeScore = 1000;

        /// <summary>该点被对方落子形成活三，预先压制的分。</summary>
        public const int BlockThreeScore = 500;

        /// <summary>无棋型候选点的基础分，叠加中心偏好后参与比较。</summary>
        public const int BaseScore = 1;

        private static readonly int[] DirectionRows = { 0, 1, 1, 1 };
        private static readonly int[] DirectionCols = { 1, 1, 0, -1 };

        private static readonly CellPos InvalidMove = new CellPos(-1, -1);

        /// <summary>
        /// 返回 <paramref name="turn"/> 一方在给定局面下的最优落点；
        /// 已分胜负、棋满或执子方非法时返回 (-1, -1)，空盘无候选时直接落天元。
        /// </summary>
        public static CellPos BestMove(Cell[,] cells, Cell turn)
        {
            if (turn == Cell.None) return InvalidMove;
            if (GomokuRules.FindWinningLine(cells) != null) return InvalidMove;
            if (GomokuRules.IsFull(cells)) return InvalidMove;

            var best = InvalidMove;
            int bestScore = int.MinValue;
            for (int row = 0; row < GomokuRules.Size; row++)
            {
                for (int col = 0; col < GomokuRules.Size; col++)
                {
                    if (cells[row, col] != Cell.None) continue;
                    if (!HasStoneNearby(cells, row, col)) continue;

                    int score = ScorePoint(cells, row, col, turn);
                    // 严格比较保证并列最优时保留先扫到（行列字典序最小）的落点。
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new CellPos(row, col);
                    }
                }
            }

            return best.Row >= 0 ? best : new CellPos(GomokuRules.Center, GomokuRules.Center);
        }

        /// <summary>
        /// 评分函数：假设 <paramref name="self"/> 落在空格 (row, col) 的启发式分值。
        /// 纯函数，不修改棋盘；调用方需保证该点为空。
        /// </summary>
        public static int ScorePoint(Cell[,] cells, int row, int col, Cell self)
        {
            var other = Other(self);
            if (MaxRunAfterPlace(cells, row, col, self) >= GomokuRules.WinLength) return OwnFiveScore;
            if (MaxRunAfterPlace(cells, row, col, other) >= GomokuRules.WinLength) return BlockFiveScore;

            int score = CountPatternScore(cells, row, col, self, OwnFourScore, OwnThreeScore)
                      + CountPatternScore(cells, row, col, other, BlockFourScore, BlockThreeScore);
            // 无棋型的候选点按基础分加中心偏好评分，越靠近天元分越高。
            if (score == 0) score = BaseScore - CenterDistance(row, col);
            return score;
        }

        /// <summary>指定坐标的棋子邻域（切比雪夫距离）内是否有任意棋子。</summary>
        private static bool HasStoneNearby(Cell[,] cells, int row, int col)
        {
            for (int dr = -GomokuRules.AiNeighborRadius; dr <= GomokuRules.AiNeighborRadius; dr++)
            {
                for (int dc = -GomokuRules.AiNeighborRadius; dc <= GomokuRules.AiNeighborRadius; dc++)
                {
                    int nextRow = row + dr;
                    int nextCol = col + dc;
                    if (GomokuRules.IsInside(nextRow, nextCol) && cells[nextRow, nextCol] != Cell.None) return true;
                }
            }

            return false;
        }

        /// <summary>沿四方向统计棋型分：活四/冲四与活三分别计权，眠三与死四不计。</summary>
        private static int CountPatternScore(Cell[,] cells, int row, int col, Cell self, int fourScore, int threeScore)
        {
            int score = 0;
            for (int dir = 0; dir < DirectionRows.Length; dir++)
            {
                RunAfterPlace(cells, row, col, self, DirectionRows[dir], DirectionCols[dir], out int run, out int openEnds);
                if (run == GomokuRules.WinLength - 1 && openEnds >= 1) score += fourScore;
                else if (run == GomokuRules.WinLength - 2 && openEnds == 2) score += threeScore;
            }

            return score;
        }

        /// <summary>假设 self 落在 (row,col) 后四方向的最大连续子数。</summary>
        private static int MaxRunAfterPlace(Cell[,] cells, int row, int col, Cell self)
        {
            int max = 1;
            for (int dir = 0; dir < DirectionRows.Length; dir++)
            {
                RunAfterPlace(cells, row, col, self, DirectionRows[dir], DirectionCols[dir], out int run, out _);
                if (run > max) max = run;
            }

            return max;
        }

        /// <summary>假设 self 落在 (row,col)，统计该方向上的连续子数与两端开放数。</summary>
        private static void RunAfterPlace(Cell[,] cells, int row, int col, Cell self, int dr, int dc, out int run, out int openEnds)
        {
            run = 1;
            openEnds = 0;
            int nextRow = row + dr;
            int nextCol = col + dc;
            while (GomokuRules.IsInside(nextRow, nextCol) && cells[nextRow, nextCol] == self)
            {
                run++;
                nextRow += dr;
                nextCol += dc;
            }
            if (GomokuRules.IsInside(nextRow, nextCol) && cells[nextRow, nextCol] == Cell.None) openEnds++;

            nextRow = row - dr;
            nextCol = col - dc;
            while (GomokuRules.IsInside(nextRow, nextCol) && cells[nextRow, nextCol] == self)
            {
                run++;
                nextRow -= dr;
                nextCol -= dc;
            }
            if (GomokuRules.IsInside(nextRow, nextCol) && cells[nextRow, nextCol] == Cell.None) openEnds++;
        }

        /// <summary>坐标到天元的曼哈顿距离，用作中心偏好。</summary>
        private static int CenterDistance(int row, int col)
        {
            return Math.Abs(row - GomokuRules.Center) + Math.Abs(col - GomokuRules.Center);
        }

        private static Cell Other(Cell turn) => turn == Cell.Black ? Cell.White : Cell.Black;
    }
}
