using Lyking.WaterSort.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.WaterSort.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class WaterSortEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建且初始布局合法()
        {
            var puzzle = new WaterSortPuzzle(0);

            Assert.AreEqual(5, puzzle.ColorCount);
            Assert.AreEqual(7, puzzle.TubeCount);
            Assert.AreEqual(4, puzzle.Capacity);
            Assert.AreEqual(0, puzzle.Moves);
            Assert.IsFalse(puzzle.IsSolved);
            Assert.AreEqual(-1, puzzle.SelectedTube);
            Assert.IsEmpty(puzzle.Tubes[puzzle.TubeCount - 1]);
            Assert.AreEqual(WaterSortEventType.NewPuzzle, puzzle.Events[puzzle.Events.Count - 1].Type);
        }

        [Test]
        public void 倒入空瓶成功且撤销恢复()
        {
            var puzzle = new WaterSortPuzzle(0);
            int emptyTube = puzzle.ColorCount;
            int before = puzzle.Tubes[0].Length;

            Assert.IsTrue(puzzle.Pour(0, emptyTube));
            Assert.Less(puzzle.Tubes[0].Length, before);
            Assert.AreEqual(1, puzzle.Moves);

            Assert.IsTrue(puzzle.Undo());
            Assert.AreEqual(before, puzzle.Tubes[0].Length);
            Assert.IsEmpty(puzzle.Tubes[emptyTube]);
            Assert.AreEqual(0, puzzle.Moves);
            Assert.IsFalse(puzzle.CanUndo);
        }

        [Test]
        public void 满瓶目标被拒绝且步数不变()
        {
            var puzzle = new WaterSortPuzzle(0);

            // 初始前 5 瓶全满，任意满瓶作为目标都被拒绝。
            Assert.IsFalse(puzzle.Pour(0, 1));
            Assert.AreEqual(0, puzzle.Moves);
            Assert.AreEqual(WaterSortEventType.PourRejected, puzzle.Events[puzzle.Events.Count - 1].Type);
        }

        [Test]
        public void 重置恢复初始布局并清空撤销()
        {
            var puzzle = new WaterSortPuzzle(0);
            var initial = puzzle.Tubes;

            Assert.IsTrue(puzzle.Pour(0, puzzle.ColorCount));
            Assert.AreEqual(1, puzzle.Moves);

            puzzle.Reset();

            Assert.AreEqual(initial, puzzle.Tubes);
            Assert.AreEqual(0, puzzle.Moves);
            Assert.IsFalse(puzzle.CanUndo);
            Assert.AreEqual(-1, puzzle.SelectedTube);
            Assert.AreEqual(WaterSortEventType.Reset, puzzle.Events[puzzle.Events.Count - 1].Type);
        }
    }
}
