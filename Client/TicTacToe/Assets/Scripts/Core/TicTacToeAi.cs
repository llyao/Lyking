using System.Collections.Generic;

namespace Lyking.TicTacToe.Core
{
    /// <summary>
    /// 井字棋 AI：minimax 全量搜索，深度折扣评分（胜 +10-深度、负 -10+深度、平 0），
    /// 快速取胜优先于拖延败局。完全确定：并列最优时取最小索引落点。
    /// </summary>
    public static class TicTacToeAi
    {
        /// <summary>
        /// 返回 <paramref name="turn"/> 一方在给定局面下的最优落点索引；
        /// 已分胜负、棋满或执子方非法时返回 -1。
        /// </summary>
        public static int BestMove(IReadOnlyList<Cell> cells, Cell turn)
        {
            if (turn == Cell.None) return -1;
            if (TicTacToeRules.FindWinner(cells) != Cell.None) return -1;

            var scratch = new Cell[cells.Count];
            for (int i = 0; i < cells.Count; i++) scratch[i] = cells[i];

            int stones = CountStones(scratch);
            int bestIndex = -1;
            int bestScore = turn == Cell.X ? int.MinValue : int.MaxValue;
            for (int i = 0; i < scratch.Length; i++)
            {
                if (scratch[i] != Cell.None) continue;

                scratch[i] = turn;
                int score = Search(scratch, Other(turn), stones + 1);
                scratch[i] = Cell.None;

                // 严格比较保证并列最优时保留最先（最小索引）落点。
                if (turn == Cell.X ? score > bestScore : score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>以 <paramref name="turn"/> 为行棋方的极大极小搜索，评分固定从 X 方视角计算。</summary>
        private static int Search(Cell[] cells, Cell turn, int stones)
        {
            var winner = TicTacToeRules.FindWinner(cells);
            if (winner == Cell.X) return 10 - stones;
            if (winner == Cell.O) return -(10 - stones);
            if (stones == TicTacToeRules.CellCount) return 0;

            int best = turn == Cell.X ? int.MinValue : int.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != Cell.None) continue;

                cells[i] = turn;
                int score = Search(cells, Other(turn), stones + 1);
                cells[i] = Cell.None;

                if (turn == Cell.X ? score > best : score < best) best = score;
            }

            return best;
        }

        private static Cell Other(Cell turn) => turn == Cell.X ? Cell.O : Cell.X;

        private static int CountStones(Cell[] cells)
        {
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != Cell.None) count++;
            }

            return count;
        }
    }
}
