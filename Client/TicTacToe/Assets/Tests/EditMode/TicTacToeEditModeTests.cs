using System.Collections.Generic;
using Lyking.TicTacToe.Core;
using NUnit.Framework;

namespace Lyking.TicTacToe.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则与 AI 表现一致。</summary>
    public sealed class TicTacToeEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建并轮流落子()
        {
            var match = new TicTacToeMatch();

            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsTrue(match.PlayerMove(4));
            Assert.AreEqual(Cell.O, match.Board.CurrentTurn);
            Assert.IsTrue(match.AiMove());
            Assert.AreEqual(1, CountCells(match.Board.Cells, Cell.X));
            Assert.AreEqual(1, CountCells(match.Board.Cells, Cell.O));
            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsFalse(match.Board.IsFinished);
        }

        [Test]
        public void AI落点确定且能挡必败手()
        {
            var threat = new[] { Cell.X, Cell.X, Cell.None, Cell.None, Cell.O, Cell.None, Cell.None, Cell.None, Cell.None };
            var empty = new Cell[9];

            Assert.AreEqual(2, TicTacToeAi.BestMove(threat, Cell.O));
            Assert.AreEqual(2, TicTacToeAi.BestMove(threat, Cell.O));
            Assert.AreEqual(0, TicTacToeAi.BestMove(empty, Cell.X));
        }

        [Test]
        public void 终局后玩家与AI均无法落子()
        {
            var match = new TicTacToeMatch();
            玩家执败着让AI获胜(match);
            int moveCount = match.Board.MoveCount;

            Assert.IsFalse(match.PlayerMove(5));
            Assert.IsFalse(match.AiMove());
            Assert.AreEqual(moveCount, match.Board.MoveCount);
        }

        [Test]
        public void 重开一局清空棋盘且比分保留()
        {
            var match = new TicTacToeMatch();
            玩家执败着让AI获胜(match);
            Assert.AreEqual(1, match.AiWins);

            match.Restart();

            Assert.AreEqual(2, match.Round);
            Assert.AreEqual(0, match.Board.MoveCount);
            Assert.AreEqual(Cell.X, match.Board.CurrentTurn);
            Assert.IsFalse(match.Board.IsFinished);
            Assert.AreEqual(1, match.AiWins);
            Assert.AreEqual(TicTacToeEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        /// <summary>玩家连出败着（0、1、3），AI 应对后于第三手抓住制胜点获胜。</summary>
        private static void 玩家执败着让AI获胜(TicTacToeMatch match)
        {
            Assert.IsTrue(match.PlayerMove(0));
            Assert.IsTrue(match.AiMove());
            Assert.IsTrue(match.PlayerMove(1));
            Assert.IsTrue(match.AiMove());
            Assert.IsTrue(match.PlayerMove(3));
            Assert.IsTrue(match.AiMove());
            Assert.AreEqual(TicTacToeOutcome.OWins, match.Board.Outcome);
        }

        private static int CountCells(IReadOnlyList<Cell> cells, Cell cell)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell) count++;
            }

            return count;
        }
    }
}
