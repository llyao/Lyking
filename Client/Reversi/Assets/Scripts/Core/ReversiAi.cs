using System.Collections.Generic;

namespace Lyking.Reversi.Core
{
    /// <summary>
    /// 黑白棋 AI：深度 3 的极大极小搜索，叶子局面按角/边加权子差评估（角 100、边 10、其余 1）。
    /// 评分固定从黑方视角计算（正值利于黑方），黑方取极大、白方取极小。
    /// 完全确定：并列最优时取行列字典序最小（行优先扫描最先遇到）的落点。
    /// </summary>
    public static class ReversiAi
    {
        /// <summary>
        /// 返回 <paramref name="board"/> 中 <paramref name="turn"/> 一方的最优落点（行优先索引）；
        /// 执子方非法或无合法步时返回 -1。
        /// </summary>
        public static int BestMove(ReversiBoard board, Cell turn)
        {
            var cells = new Cell[ReversiRules.Size, ReversiRules.Size];
            for (int row = 0; row < ReversiRules.Size; row++)
            {
                for (int col = 0; col < ReversiRules.Size; col++) cells[row, col] = board[row, col];
            }

            return BestMove(cells, turn);
        }

        /// <summary>纯函数重载：直接在给定盘面上搜索（测试与复盘用）。</summary>
        public static int BestMove(Cell[,] cells, Cell turn)
        {
            if (turn == Cell.None) return -1;

            var moves = ReversiRules.CollectMoves(cells, turn);
            if (moves.Count == 0) return -1;

            int bestIndex = -1;
            int bestScore = turn == Cell.Black ? int.MinValue : int.MaxValue;
            for (int i = 0; i < moves.Count; i++)
            {
                var next = (Cell[,])cells.Clone();
                ReversiRules.ApplyMove(next, moves[i], turn);
                int score = Search(next, ReversiRules.OpponentOf(turn), ReversiRules.SearchDepth - 1);

                // 严格比较保证并列最优时保留行列字典序最小的落点。
                if (turn == Cell.Black ? score > bestScore : score < bestScore)
                {
                    bestScore = score;
                    bestIndex = ReversiRules.FlatIndex(moves[i].Row, moves[i].Col);
                }
            }

            return bestIndex;
        }

        /// <summary>以 <paramref name="turn"/> 为行棋方的极大极小搜索，评分固定从黑方视角计算。</summary>
        private static int Search(Cell[,] cells, Cell turn, int depth)
        {
            var moves = ReversiRules.CollectMoves(cells, turn);
            if (moves.Count == 0)
            {
                // 己方无步：对方同样无步即终局，按加权子差定值；否则对方继续行棋（己方过一手）。
                var opponent = ReversiRules.OpponentOf(turn);
                return ReversiRules.HasAnyMove(cells, opponent)
                    ? Search(cells, opponent, depth - 1)
                    : ReversiRules.Evaluate(cells);
            }

            if (depth <= 0) return ReversiRules.Evaluate(cells);

            int best = turn == Cell.Black ? int.MinValue : int.MaxValue;
            for (int i = 0; i < moves.Count; i++)
            {
                var next = (Cell[,])cells.Clone();
                ReversiRules.ApplyMove(next, moves[i], turn);
                int score = Search(next, ReversiRules.OpponentOf(turn), depth - 1);

                if (turn == Cell.Black ? score > best : score < best) best = score;
            }

            return best;
        }
    }
}
