using System.Collections.Generic;
using Lyking.ConnectFour.Core;
using NUnit.Framework;

namespace Lyking.ConnectFour.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则与 AI 表现一致。</summary>
    public sealed class ConnectFourEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建并轮流落子()
        {
            var match = new ConnectFourMatch();

            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsTrue(match.PlayerDrop(3));
            Assert.AreEqual(Cell.Yellow, match.Board.CurrentTurn);
            Assert.IsTrue(match.AiDrop());
            Assert.AreEqual(1, CountStones(match.Board.Cells, Cell.Red));
            Assert.AreEqual(1, CountStones(match.Board.Cells, Cell.Yellow));
            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsFalse(match.Board.IsFinished);
        }

        [Test]
        public void AI落点确定且能挡必败手()
        {
            // 红方在 3 列已堆三子，黄方必须立刻封堵；空盘两次决策必须一致。
            var threat = BuildBoard(new[]
            {
                (3, 0, Cell.Red), (3, 1, Cell.Red), (3, 2, Cell.Red),
            });
            var empty = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];

            Assert.AreEqual(3, ConnectFourAi.BestColumn(threat, Cell.Yellow));
            Assert.AreEqual(3, ConnectFourAi.BestColumn(threat, Cell.Yellow));
            Assert.AreEqual(ConnectFourAi.BestColumn(empty, Cell.Red),
                ConnectFourAi.BestColumn(empty, Cell.Red));
        }

        [Test]
        public void 终局后玩家与AI均无法落子()
        {
            var match = new ConnectFourMatch();
            玩家连成纵向四子获胜(match);
            int moveCount = match.Board.MoveCount;

            Assert.IsFalse(match.PlayerDrop(1));
            Assert.IsFalse(match.AiDrop());
            Assert.AreEqual(moveCount, match.Board.MoveCount);
        }

        [Test]
        public void 重开一局清空棋盘且比分保留()
        {
            var match = new ConnectFourMatch();
            玩家连成纵向四子获胜(match);
            Assert.AreEqual(1, match.PlayerWins);

            match.Restart();

            Assert.AreEqual(2, match.Round);
            Assert.AreEqual(0, match.Board.MoveCount);
            Assert.AreEqual(Cell.Red, match.Board.CurrentTurn);
            Assert.IsFalse(match.Board.IsFinished);
            Assert.AreEqual(1, match.PlayerWins);
            Assert.AreEqual(ConnectFourEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        /// <summary>黄子被直接驱动去 6 列堆叠，玩家在 0 列连成纵向四子获胜。</summary>
        private static void 玩家连成纵向四子获胜(ConnectFourMatch match)
        {
            Assert.IsTrue(match.PlayerDrop(0));
            Assert.IsTrue(match.Board.Drop(6));
            Assert.IsTrue(match.PlayerDrop(0));
            Assert.IsTrue(match.Board.Drop(6));
            Assert.IsTrue(match.PlayerDrop(0));
            Assert.IsTrue(match.Board.Drop(6));
            Assert.IsTrue(match.PlayerDrop(0));
            Assert.AreEqual(ConnectFourOutcome.RedWins, match.Board.Outcome);
        }

        /// <summary>按 [列,行] 坐标批量放置棋子，构造指定局面。</summary>
        private static Cell[,] BuildBoard((int Col, int Row, Cell Cell)[] stones)
        {
            var cells = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];
            foreach (var stone in stones) cells[stone.Col, stone.Row] = stone.Cell;
            return cells;
        }

        private static int CountStones(Cell[,] cells, Cell cell)
        {
            int count = 0;
            for (int col = 0; col < cells.GetLength(0); col++)
            {
                for (int row = 0; row < cells.GetLength(1); row++)
                {
                    if (cells[col, row] == cell) count++;
                }
            }

            return count;
        }
    }
}
