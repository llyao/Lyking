using System;
using System.Linq;
using Lyking.SlidingPuzzle.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.SlidingPuzzle.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SlidingPuzzleEditModeTests
    {
        private static readonly SlideDir[] AllDirs = { SlideDir.Up, SlideDir.Down, SlideDir.Left, SlideDir.Right };

        [Test]
        public void 打乱后滑块编号完整且未还原()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            board.Shuffle(9);

            Assert.AreEqual(PuzzleRules.MaxSize * PuzzleRules.MaxSize, board.TileCount);
            var tiles = board.Tiles;
            CollectionAssert.AreEquivalent(Enumerable.Range(0, tiles.Length).ToList(), tiles);
            Assert.AreEqual(0, board.Steps);
            Assert.IsFalse(board.IsSolved);
            CollectionAssert.Contains(board.Events.Select(e => e.Type).ToList(), SlidingPuzzleEventType.Shuffled);
        }

        [Test]
        public void 相邻滑块滑入空位且空位反向移动()
        {
            var board = new SlidingBoard(PuzzleRules.MinSize);
            board.Shuffle(9);

            foreach (var dir in AllDirs)
            {
                MoveBlankTo(board, 1, 1);
                int before = BlankIndexOf(board);
                int expectedRow = before / board.Size + (dir == SlideDir.Up ? 1 : dir == SlideDir.Down ? -1 : 0);
                int expectedColumn = before % board.Size + (dir == SlideDir.Left ? 1 : dir == SlideDir.Right ? -1 : 0);

                int steps = board.Steps;
                Assert.IsTrue(board.Move(dir), $"{dir} 在空位居中时应合法。");
                int after = BlankIndexOf(board);
                Assert.AreEqual(expectedRow, after / board.Size);
                Assert.AreEqual(expectedColumn, after % board.Size);
                Assert.AreEqual(steps + 1, board.Steps);
                Assert.AreEqual(SlidingPuzzleEventType.Slid, board.Events[board.Events.Count - 1].Type);
            }
        }

        [Test]
        public void 同行滑块一次滑入多格()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            board.Shuffle(9);
            MoveBlankTo(board, 1, 1);

            int steps = board.Steps;
            int clicked = 1 * board.Size + 3;
            Assert.AreEqual(2, board.SlideTile(clicked));
            Assert.AreEqual(steps + 2, board.Steps);
            Assert.AreEqual(0, board.Tiles[clicked]);
        }

        [Test]
        public void 尺寸切换重置回已解状态()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            board.Shuffle(9);
            Assert.IsFalse(board.IsSolved);

            board.Reset(PuzzleRules.MinSize);
            Assert.AreEqual(PuzzleRules.MinSize, board.Size);
            Assert.AreEqual(PuzzleRules.MinSize * PuzzleRules.MinSize, board.TileCount);
            Assert.IsTrue(board.IsSolved);
            Assert.AreEqual(0, board.Steps);
            Assert.IsFalse(board.Undo());

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reset(5));
        }

        private static int BlankIndexOf(SlidingBoard board)
        {
            var tiles = board.Tiles;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == 0) return i;
            }

            Assert.Fail("棋局必有空位。");
            return 0;
        }

        /// <summary>把空位逐步推到目标格：Move(Up) 使空位下移一行，Move(Left) 使空位右移一列，依此类推。</summary>
        private static void MoveBlankTo(SlidingBoard board, int row, int column)
        {
            int guard = 0;
            while (BlankIndexOf(board) != row * board.Size + column)
            {
                int blank = BlankIndexOf(board);
                int blankRow = blank / board.Size;
                int blankColumn = blank % board.Size;
                if (blankRow < row) Assert.IsTrue(board.Move(SlideDir.Up));
                else if (blankRow > row) Assert.IsTrue(board.Move(SlideDir.Down));
                else if (blankColumn < column) Assert.IsTrue(board.Move(SlideDir.Left));
                else if (blankColumn > column) Assert.IsTrue(board.Move(SlideDir.Right));

                if (++guard > board.TileCount) Assert.Fail("空位移动未收敛。");
            }
        }
    }
}
