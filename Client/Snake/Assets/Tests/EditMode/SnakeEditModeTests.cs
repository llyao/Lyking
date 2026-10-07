using System;
using Lyking.Snake.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Snake.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SnakeEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建并直行推进()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 7);

            Assert.AreEqual(3, board.Length);
            Assert.AreEqual(SnakeDir.Right, board.CurrentDirection);
            Assert.IsFalse(board.IsFinished);
            Assert.IsTrue(board.Food.X >= 0 && board.Food.X < board.Width);
            Assert.IsTrue(board.Food.Y >= 0 && board.Food.Y < board.Height);

            var headBefore = board.Head;
            Assert.IsTrue(board.Tick());
            Assert.AreEqual(headBefore.X + 1, board.Head.X);
            Assert.AreEqual(headBefore.Y, board.Head.Y);
        }

        [Test]
        public void 反向与重复方向入队被忽略()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 7);

            Assert.IsFalse(board.EnqueueDirection(SnakeDir.Left));
            Assert.IsFalse(board.EnqueueDirection(SnakeDir.Right));
            Assert.IsTrue(board.EnqueueDirection(SnakeDir.Up));

            board.Tick();
            Assert.AreEqual(SnakeDir.Up, board.CurrentDirection);
        }

        [Test]
        public void 连续两步转向按队列依次生效()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 7);

            Assert.IsTrue(board.EnqueueDirection(SnakeDir.Up));
            Assert.IsTrue(board.EnqueueDirection(SnakeDir.Left));

            board.Tick();
            Assert.AreEqual(SnakeDir.Up, board.CurrentDirection);
            board.Tick();
            Assert.AreEqual(SnakeDir.Left, board.CurrentDirection);
        }

        [Test]
        public void 撞墙结束并写入事件日志()
        {
            var rules = new SnakeRules(width: 5, height: 5, initialLength: 1, baseStepSeconds: 0.2f, minStepSeconds: 0.1f, stepShrinkPerFood: 0.01f, scorePerFood: 10);
            var board = new SnakeBoard(rules, seed: 1);

            for (int i = 0; i < 10 && !board.IsFinished; i++) board.Tick();

            Assert.IsTrue(board.IsGameOver);
            Assert.AreEqual(SnakeEventType.WallCollision, board.Events[board.Events.Count - 1].Type);
        }
    }
}
