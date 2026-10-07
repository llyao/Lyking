using System.Linq;
using Lyking.FloodIt.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.FloodIt.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class FloodItEditModeTests
    {
        [Test]
        public void 生成后棋盘就绪且颜色合法()
        {
            var board = new FloodBoard(seed: 7);

            Assert.AreEqual(FloodItRules.Rows, board.Rows);
            Assert.AreEqual(FloodItRules.CellCount, board.CellCount);
            Assert.AreEqual(0, board.Steps);
            Assert.AreEqual(FloodItRules.MaxMoves, board.RemainingMoves);
            Assert.GreaterOrEqual(board.FloodedCount, 1);
            Assert.IsTrue(board.IsFlooded(0, 0));
            Assert.IsFalse(board.IsWon);
            Assert.IsFalse(board.IsLost);

            var colors = Enumerable.Range(0, board.Rows)
                .SelectMany(row => Enumerable.Range(0, board.Columns).Select(column => board.GetColor(row, column)));
            Assert.IsTrue(colors.All(color => color >= 0 && color < FloodItRules.ColorCount));
            Assert.AreEqual(FloodItEventType.Generated, board.Events[0].Type);
        }

        [Test]
        public void 选色扩张且剩余步数递减()
        {
            var board = new FloodBoard(seed: 7);
            int before = board.FloodedCount;
            int pick = SmallestValidColor(board);

            Assert.IsTrue(board.Choose(pick));
            Assert.AreEqual(1, board.Steps);
            Assert.AreEqual(FloodItRules.MaxMoves - 1, board.RemainingMoves);
            Assert.AreEqual(pick, board.GetColor(0, 0));
            Assert.AreEqual(FloodItEventType.Chosen, board.Events[^1].Type);
            Assert.GreaterOrEqual(board.Events[^1].ExpandedCount, 0);
            Assert.GreaterOrEqual(board.FloodedCount, before);
        }

        [Test]
        public void 同色选择被拒绝且不计步()
        {
            var board = new FloodBoard(seed: 7);
            int current = board.GetColor(0, 0);
            int floodedCount = board.FloodedCount;

            Assert.IsFalse(board.Choose(current));
            Assert.AreEqual(0, board.Steps);
            Assert.AreEqual(floodedCount, board.FloodedCount);
            Assert.AreEqual(FloodItEventType.Rejected, board.Events[^1].Type);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Choose(FloodItRules.ColorCount));
        }

        [Test]
        public void 重置与换题恢复初始状态()
        {
            var board = new FloodBoard(seed: 7);
            int initialFlooded = board.FloodedCount;
            Assert.IsTrue(board.Choose(SmallestValidColor(board)));
            Assert.AreEqual(1, board.Steps);

            board.Reset();
            Assert.AreEqual(0, board.Steps);
            Assert.AreEqual(initialFlooded, board.FloodedCount);
            Assert.AreEqual(FloodItEventType.Reset, board.Events[^1].Type);

            board.NewPuzzle(99);
            Assert.AreEqual(99, board.Seed);
            Assert.AreEqual(0, board.Steps);
            Assert.AreEqual(SnapshotOf(new FloodBoard(seed: 99)), SnapshotOf(board));
            Assert.AreEqual(FloodItEventType.Generated, board.Events[^1].Type);
        }

        private static int SmallestValidColor(FloodBoard board) => board.GetColor(0, 0) == 0 ? 1 : 0;

        private static string SnapshotOf(FloodBoard board)
        {
            var values = new System.Collections.Generic.List<string>();
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    values.Add(board.GetColor(row, column).ToString());
                }
            }

            return string.Join(";", values);
        }
    }
}
