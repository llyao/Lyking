using Lyking.Sokoban.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Sokoban.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SokobanEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建并一推过关()
        {
            var puzzle = new SokobanPuzzle(SokobanRules.Default, 0);

            Assert.AreEqual(0, puzzle.LevelIndex);
            Assert.AreEqual(0, puzzle.MoveCount);
            Assert.AreEqual(0, puzzle.PushCount);
            Assert.IsFalse(puzzle.IsSolved);
            Assert.AreEqual(new GridPos(2, 2), puzzle.Player);

            Assert.IsTrue(puzzle.Move(SokobanDir.Right));
            Assert.IsTrue(puzzle.IsSolved);
            Assert.AreEqual(1, puzzle.PushCount);
        }

        [Test]
        public void 推墙被拒绝且步数不变()
        {
            var puzzle = new SokobanPuzzle(SokobanRules.Default, 0);

            Assert.IsTrue(puzzle.Move(SokobanDir.Up));
            Assert.IsFalse(puzzle.Move(SokobanDir.Up));

            Assert.AreEqual(1, puzzle.MoveCount);
            Assert.AreEqual(0, puzzle.PushCount);
            Assert.AreEqual(SokobanEventType.MoveRejected, puzzle.Events[puzzle.Events.Count - 1].Type);
        }

        [Test]
        public void 撤销恢复步数与箱子位置()
        {
            var puzzle = new SokobanPuzzle(SokobanRules.Default, 0);

            Assert.IsTrue(puzzle.Move(SokobanDir.Right));
            Assert.IsTrue(puzzle.IsSolved);
            Assert.AreEqual(1, puzzle.MoveCount);
            Assert.AreEqual(1, puzzle.PushCount);

            Assert.IsTrue(puzzle.Undo());
            Assert.IsFalse(puzzle.IsSolved);
            Assert.AreEqual(0, puzzle.MoveCount);
            Assert.AreEqual(0, puzzle.PushCount);
            Assert.IsTrue(puzzle.Boxes.Contains(new GridPos(3, 2)));

            Assert.IsFalse(puzzle.Undo());
        }

        [Test]
        public void 重置当前关恢复初始局面()
        {
            var puzzle = new SokobanPuzzle(SokobanRules.Default, 0);

            Assert.IsTrue(puzzle.Move(SokobanDir.Up));
            Assert.IsTrue(puzzle.Move(SokobanDir.Left));
            Assert.AreEqual(2, puzzle.MoveCount);

            puzzle.Reset();

            Assert.AreEqual(0, puzzle.MoveCount);
            Assert.AreEqual(0, puzzle.PushCount);
            Assert.AreEqual(new GridPos(2, 2), puzzle.Player);
            Assert.IsTrue(puzzle.Boxes.Contains(new GridPos(3, 2)));
            Assert.IsFalse(puzzle.IsSolved);
            Assert.IsFalse(puzzle.Undo());
        }
    }
}
