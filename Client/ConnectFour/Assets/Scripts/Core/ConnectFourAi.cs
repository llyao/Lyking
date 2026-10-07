using System.Collections.Generic;

namespace Lyking.ConnectFour.Core
{
    /// <summary>
    /// 四子连珠 AI：minimax 限定深度（默认 4）与窗口计分评估——统计各方向四连窗口内
    /// 己方二连/三连权重减去对手权重，终局给 ±大分并偏好更快取胜。
    /// 完全确定：并列最优时取最小列索引。
    /// </summary>
    public static class ConnectFourAi
    {
        private const int WinScore = 1000000;

        /// <summary>
        /// 返回 <paramref name="turn"/> 一方在给定局面下的最优落子列；
        /// 已分胜负、棋满或执子方非法时返回 -1。
        /// </summary>
        public static int BestColumn(Cell[,] cells, Cell turn)
        {
            if (turn == Cell.None) return -1;
            if (ConnectFourRules.FindWinner(cells) != Cell.None) return -1;
            if (ConnectFourRules.IsFull(cells)) return -1;

            var scratch = (Cell[,])cells.Clone();
            int stones = CountStones(scratch);
            int bestScore = turn == Cell.Red ? int.MinValue : int.MaxValue;
            int bestColumn = -1;
            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                int row = ConnectFourRules.LowestEmptyRow(scratch, col);
                if (row < 0) continue;

                scratch[col, row] = turn;
                int score = Search(scratch, Other(turn), ConnectFourRules.AiSearchDepth - 1, stones + 1);
                scratch[col, row] = Cell.None;

                // 严格比较保证并列最优时保留最先（最小列索引）落点。
                if (turn == Cell.Red ? score > bestScore : score < bestScore)
                {
                    bestScore = score;
                    bestColumn = col;
                }
            }

            return bestColumn;
        }

        /// <summary>以 <paramref name="turn"/> 为行棋方的极大极小搜索，评分固定从红方视角计算。</summary>
        private static int Search(Cell[,] cells, Cell turn, int depth, int stones)
        {
            var winner = ConnectFourRules.FindWinner(cells);
            if (winner == Cell.Red) return WinScore - stones;
            if (winner == Cell.Yellow) return -(WinScore - stones);
            if (depth == 0) return Evaluate(cells);
            if (ConnectFourRules.IsFull(cells)) return 0;

            int best = turn == Cell.Red ? int.MinValue : int.MaxValue;
            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                int row = ConnectFourRules.LowestEmptyRow(cells, col);
                if (row < 0) continue;

                cells[col, row] = turn;
                int score = Search(cells, Other(turn), depth - 1, stones + 1);
                cells[col, row] = Cell.None;

                if (turn == Cell.Red ? score > best : score < best) best = score;
            }

            return best;
        }

        /// <summary>从红方视角评估局面：全部四连窗口的纯色子数权重，红方加分、黄方减分。</summary>
        private static int Evaluate(Cell[,] cells)
        {
            int score = 0;
            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                for (int row = 0; row < ConnectFourRules.Rows; row++)
                {
                    // 四个方向各计一次窗口：横向、纵向与两条对角线。
                    score += WindowScore(cells, col, row, 1, 0);
                    score += WindowScore(cells, col, row, 0, 1);
                    score += WindowScore(cells, col, row, 1, 1);
                    score += WindowScore(cells, col, row, 1, -1);
                }
            }

            return score;
        }

        /// <summary>以 (col,row) 为起点、沿 (dc,dr) 的四格窗口：混色计 0 分，纯色按子数加权。</summary>
        private static int WindowScore(Cell[,] cells, int col, int row, int dc, int dr)
        {
            if (!ConnectFourRules.IsInside(col + dc * 3, row + dr * 3)) return 0;

            int red = 0;
            int yellow = 0;
            for (int i = 0; i < ConnectFourRules.ConnectLength; i++)
            {
                var cell = cells[col + dc * i, row + dr * i];
                if (cell == Cell.Red) red++;
                else if (cell == Cell.Yellow) yellow++;
            }

            if (red > 0 && yellow > 0) return 0;
            if (red > 0) return WindowWeight(red);
            return -WindowWeight(yellow);
        }

        /// <summary>窗口内纯色子数权重：二连、三连威胁逐级放大，四连由终局判定处理。</summary>
        private static int WindowWeight(int stones) => stones switch
        {
            1 => 1,
            2 => 8,
            3 => 64,
            _ => 0,
        };

        private static Cell Other(Cell turn) => turn == Cell.Red ? Cell.Yellow : Cell.Red;

        private static int CountStones(Cell[,] cells)
        {
            int count = 0;
            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                for (int row = 0; row < ConnectFourRules.Rows; row++)
                {
                    if (cells[col, row] != Cell.None) count++;
                }
            }

            return count;
        }
    }
}
