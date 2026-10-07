using System.Collections.Generic;
using Lyking.Reversi.Core;
using Lyking.Reversi.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Reversi.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局与演示引导在 Unity 程序集边界内可用，关键规则与 AI 表现一致。</summary>
    public sealed class ReversiEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建并按规则轮转()
        {
            var match = new ReversiMatch();

            Assert.IsTrue(match.IsPlayerTurn);
            Assert.IsTrue(match.PlayerMove(2, 3));   // d3 翻 d4
            Assert.AreEqual((4, 1), match.Board.Counts);
            Assert.AreEqual(Cell.White, match.Board.CurrentTurn);
            Assert.IsTrue(match.AiMove());
            Assert.AreEqual(2, match.Board.MoveCount);
            Assert.IsFalse(match.Board.IsFinished);
            Assert.IsTrue(match.IsPlayerTurn);
        }

        [Test]
        public void AI决策确定且会抢占角点()
        {
            var cells = new Cell[ReversiRules.Size, ReversiRules.Size];
            cells[0, 1] = Cell.White;
            cells[1, 1] = Cell.White;
            cells[0, 2] = Cell.Black;
            cells[2, 2] = Cell.Black;
            cells[4, 3] = Cell.White;
            cells[4, 4] = Cell.White;
            cells[4, 2] = Cell.Black;
            cells[5, 5] = Cell.Black;

            int decision = ReversiAi.BestMove(cells, Cell.Black);

            Assert.AreEqual(0, decision);   // a1 角点
            Assert.AreEqual(decision, ReversiAi.BestMove(cells, Cell.Black));
            Assert.AreEqual(ReversiRules.FlatIndex(2, 3), ReversiAi.BestMove(new ReversiBoard(), Cell.Black));
        }

        [Test]
        public void 终局后拒绝落子且重开保留比分()
        {
            // 白子仅 d4、c5 两枚：黑落 c4 一手翻尽白子，双方无步直接终局。
            var cells = new Cell[ReversiRules.Size, ReversiRules.Size];
            cells[3, 3] = Cell.White;
            cells[4, 2] = Cell.White;
            cells[3, 4] = Cell.Black;
            cells[5, 2] = Cell.Black;
            var match = new ReversiMatch(new ReversiBoard(cells, Cell.Black));

            Assert.IsTrue(match.PlayerMove(3, 2));
            Assert.IsTrue(match.Board.IsFinished);
            Assert.AreEqual(ReversiOutcome.BlackWins, match.Board.Outcome);
            Assert.AreEqual(1, match.PlayerWins);
            int moveCount = match.Board.MoveCount;

            Assert.IsFalse(match.PlayerMove(0, 0));
            Assert.IsFalse(match.AiMove());
            Assert.AreEqual(moveCount, match.Board.MoveCount);

            match.Restart();

            Assert.AreEqual(2, match.Round);
            Assert.AreEqual((2, 2), match.Board.Counts);
            Assert.AreEqual(Cell.Black, match.Board.CurrentTurn);
            Assert.IsFalse(match.Board.IsFinished);
            Assert.AreEqual(1, match.PlayerWins);
            Assert.AreEqual(ReversiEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        [Test]
        public void 引导光标夹取边界且回车落子走核心规则()
        {
            var bootstrap = new GameObject("ReversiBootstrap").AddComponent<ReversiDemoBootstrap>();
            try
            {
                bootstrap.StartGame();
                Assert.IsTrue(bootstrap.HasMatch);

                bootstrap.MoveCursor(-9, -9);
                Assert.AreEqual(0, bootstrap.CursorRow);
                Assert.AreEqual(0, bootstrap.CursorCol);
                bootstrap.MoveCursor(9, 9);
                Assert.AreEqual(ReversiRules.Size - 1, bootstrap.CursorRow);
                Assert.AreEqual(ReversiRules.Size - 1, bootstrap.CursorCol);

                bootstrap.MoveCursor(-5, -4);   // 光标移到 d3
                Assert.AreEqual(2, bootstrap.CursorRow);
                Assert.AreEqual(3, bootstrap.CursorCol);

                bootstrap.PlayAtCursor();
                Assert.AreEqual(Cell.Black, bootstrap.Match.Board[2, 3]);
                Assert.AreEqual(1, bootstrap.Match.Board.MoveCount);

                // 再次回车落在已占格：由核心拒绝，手数不变。
                bootstrap.PlayAtCursor();
                Assert.AreEqual(1, bootstrap.Match.Board.MoveCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bootstrap.gameObject);
            }
        }
    }
}
