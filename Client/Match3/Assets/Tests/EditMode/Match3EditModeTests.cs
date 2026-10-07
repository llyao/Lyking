using System.Linq;
using Lyking.Match3.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Match3.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class Match3EditModeTests
    {
        [Test]
        public void 初始棋盘就绪且无现成三连()
        {
            var board = new Match3Board(seed: 7);

            Assert.AreEqual(Match3Rules.CellCount, board.CellCount);
            Assert.AreEqual(0, board.Moves);
            Assert.AreEqual(0, board.Score);
            Assert.AreEqual(Match3Rules.TotalMoves, board.MovesLeft);
            Assert.IsFalse(board.IsFinished);
            Assert.IsTrue(board.HasValidMove);
            Assert.IsFalse(HasAnyRun(board.Gems), "初始棋盘不应有现成三连。");
            Assert.AreEqual(Match3EventType.Dealt, board.Events[0].Type);
        }

        [Test]
        public void 交换形成三连即消除计分()
        {
            var board = new Match3Board(ThreeInRowAt(), seed: 7);

            Assert.IsTrue(board.Swap(1, 9));
            Assert.AreEqual(1, board.Moves);
            Assert.AreEqual(60, board.Score);
            Assert.AreEqual(1, board.ChainLevel);
            Assert.Contains(Match3EventType.Matched, board.Events.Select(e => e.Type).ToList());
            Assert.Contains(Match3EventType.Refilled, board.Events.Select(e => e.Type).ToList());
        }

        [Test]
        public void 无效交换回退且不计步()
        {
            var initial = BackgroundBoard();
            var board = new Match3Board(initial, seed: 7);

            Assert.IsFalse(board.Swap(0, 1));
            Assert.AreEqual(0, board.Moves);
            Assert.AreEqual(0, board.Score);
            CollectionAssert.AreEqual(Flatten(initial), Flatten(board.Gems), "无效交换应回退原状。");
            Assert.AreEqual(Match3EventType.SwapRejected, board.Events[^1].Type);
        }

        [Test]
        public void 死局棋盘重排后可行()
        {
            var board = new Match3Board(DeadBoard(), seed: 1);
            Assert.IsFalse(board.HasValidMove);

            Assert.IsTrue(board.Shuffle());
            Assert.IsTrue(board.HasValidMove);
            Assert.IsFalse(HasAnyRun(board.Gems), "重排后不应有现成三连。");
            Assert.AreEqual(Match3EventType.Shuffled, board.Events[^1].Type);
        }

        /// <summary>生成无三连的背景棋盘：bg(r,c) = (3r+c) % 6。</summary>
        private static int[,] BackgroundBoard()
        {
            var gems = new int[Match3Rules.Rows, Match3Rules.Columns];
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    gems[row, column] = (3 * row + column) % Match3Rules.GemKindCount;
                }
            }

            return gems;
        }

        /// <summary>行 0 = [2,5,2,3,...] 且 (1,1)=2：交换 (0,1) 与 (1,1) 补全横向三连。</summary>
        private static int[,] ThreeInRowAt()
        {
            var gems = BackgroundBoard();
            gems[0, 0] = 2;
            gems[0, 2] = 2;
            gems[1, 1] = 2;
            return gems;
        }

        /// <summary>(2r+3c)%6 的棋盘任何相邻交换都不会产生三连。</summary>
        private static int[,] DeadBoard()
        {
            var gems = new int[Match3Rules.Rows, Match3Rules.Columns];
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    gems[row, column] = (2 * row + 3 * column) % Match3Rules.GemKindCount;
                }
            }

            return gems;
        }

        private static bool HasAnyRun(int[,] gems)
        {
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column + 2 < Match3Rules.Columns; column++)
                {
                    if (gems[row, column] == gems[row, column + 1] && gems[row, column] == gems[row, column + 2]) return true;
                }
            }

            for (int column = 0; column < Match3Rules.Columns; column++)
            {
                for (int row = 0; row + 2 < Match3Rules.Rows; row++)
                {
                    if (gems[row, column] == gems[row + 1, column] && gems[row, column] == gems[row + 2, column]) return true;
                }
            }

            return false;
        }

        private static System.Collections.Generic.IEnumerable<int> Flatten(int[,] gems) => gems.Cast<int>();
    }
}
