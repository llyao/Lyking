using Lyking.Gomoku.Core;
using NUnit.Framework;

namespace Lyking.Gomoku.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则与 AI 表现一致。</summary>
    public sealed class GomokuEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建并轮流落子()
        {
            var match = new GomokuMatch();

            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsTrue(match.PlayerMove(7, 7));
            Assert.AreEqual(Cell.White, match.Board.CurrentTurn);
            Assert.IsTrue(match.AiMove());
            Assert.AreEqual(1, CountStones(match.Board.Cells, Cell.Black));
            Assert.AreEqual(1, CountStones(match.Board.Cells, Cell.White));
            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsFalse(match.Board.IsFinished);
        }

        [Test]
        public void AI落点确定且能挡即胜手()
        {
            // 白子 (7,4)-(7,7) 冲四，黑方两次决策都必须堵 (7,3)；空盘两次决策必须一致落天元。
            var threat = BuildBoard(new[]
            {
                (7, 4, Cell.White), (7, 5, Cell.White), (7, 6, Cell.White), (7, 7, Cell.White), (7, 8, Cell.Black),
            });
            var empty = new Cell[GomokuRules.Size, GomokuRules.Size];

            Assert.AreEqual(new CellPos(7, 3), GomokuAi.BestMove(threat, Cell.Black));
            Assert.AreEqual(new CellPos(7, 3), GomokuAi.BestMove(threat, Cell.Black));
            Assert.AreEqual(GomokuAi.BestMove(empty, Cell.Black), GomokuAi.BestMove(empty, Cell.Black));
        }

        [Test]
        public void 终局后玩家与AI均无法落子()
        {
            var match = new GomokuMatch();
            玩家连成纵向五连获胜(match);
            int moveCount = match.Board.MoveCount;

            Assert.IsFalse(match.PlayerMove(0, 1));
            Assert.IsFalse(match.AiMove());
            Assert.AreEqual(moveCount, match.Board.MoveCount);
        }

        [Test]
        public void 重开一局清空棋盘且比分保留()
        {
            var match = new GomokuMatch();
            玩家连成纵向五连获胜(match);
            Assert.AreEqual(1, match.PlayerWins);

            match.Restart();

            Assert.AreEqual(2, match.Round);
            Assert.AreEqual(0, match.Board.MoveCount);
            Assert.AreEqual(Cell.Black, match.Board.CurrentTurn);
            Assert.IsFalse(match.Board.IsFinished);
            Assert.AreEqual(1, match.PlayerWins);
            Assert.AreEqual(GomokuEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        /// <summary>白方用核心棋盘直接应答（0 行分散落子），玩家在第 7 列连成纵向五连。</summary>
        private static void 玩家连成纵向五连获胜(GomokuMatch match)
        {
            Assert.IsTrue(match.PlayerMove(3, 7));
            Assert.IsTrue(match.Board.Play(0, 0));
            Assert.IsTrue(match.PlayerMove(4, 7));
            Assert.IsTrue(match.Board.Play(0, 1));
            Assert.IsTrue(match.PlayerMove(5, 7));
            Assert.IsTrue(match.Board.Play(0, 2));
            Assert.IsTrue(match.PlayerMove(6, 7));
            Assert.IsTrue(match.Board.Play(0, 3));
            Assert.IsTrue(match.PlayerMove(7, 7));
            Assert.AreEqual(GomokuOutcome.BlackWins, match.Board.Outcome);
        }

        /// <summary>按 [行,列] 坐标批量放置棋子，构造指定局面。</summary>
        private static Cell[,] BuildBoard((int Row, int Col, Cell Cell)[] stones)
        {
            var cells = new Cell[GomokuRules.Size, GomokuRules.Size];
            foreach (var stone in stones) cells[stone.Row, stone.Col] = stone.Cell;
            return cells;
        }

        private static int CountStones(Cell[,] cells, Cell cell)
        {
            int count = 0;
            for (int row = 0; row < cells.GetLength(0); row++)
            {
                for (int col = 0; col < cells.GetLength(1); col++)
                {
                    if (cells[row, col] == cell) count++;
                }
            }

            return count;
        }
    }
}
