using System.Linq;
using Lyking.Breakout.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Breakout.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class BreakoutEditModeTests
    {
        [Test]
        public void 对局在引擎外可创建且砖阵完整()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            Assert.AreEqual(40, game.Bricks.Count);
            Assert.AreEqual(40, game.AliveBrickCount);
            Assert.AreEqual(3, game.Lives);
            Assert.AreEqual(1, game.Level);
            Assert.AreEqual(0, game.Score);
            Assert.IsFalse(game.IsGameOver);
            Assert.IsFalse(game.BallLaunched);
            Assert.IsTrue(game.Bricks.First(b => b.Row == 0).Durability == BreakoutRules.Default.TopRowDurability);
            Assert.AreEqual(BreakoutEventType.Spawned, game.Events[0].Type);
        }

        [Test]
        public void 发球后固定子步推进球上升()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            Assert.IsTrue(game.Launch(0f));
            var yBefore = game.BallPosition.Y;
            game.Step(0.5f);

            Assert.Greater(game.BallPosition.Y, yBefore + 3f);
            Assert.IsTrue(game.BallLaunched);
            Assert.Greater(game.StepIndex, 0);
        }

        [Test]
        public void 击毁砖块加分并记录事件()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            game.MovePaddle(1f);
            game.Step(0.25f); // 挡板到 13，正对第 6 列砖
            game.MovePaddle(0f);
            Assert.IsTrue(game.Launch(0f));

            for (int i = 0; i < 40 && game.Score == 0; i++) game.Step(0.5f);

            Assert.AreEqual(5, game.Score);
            Assert.AreEqual(39, game.AliveBrickCount);
            Assert.IsTrue(game.Events.Any(e => e.Type == BreakoutEventType.BrickDestroyed));
        }

        [Test]
        public void 失球后球重新贴板等待发射()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            game.MovePaddle(1f);
            game.Step(0.25f);
            game.MovePaddle(0f);
            Assert.IsTrue(game.Launch(0f));
            game.Step(1f); // 球升空
            game.MovePaddle(-1f);
            game.Step(3f); // 挡板让开球道，球落底失命
            game.MovePaddle(0f);

            Assert.AreEqual(2, game.Lives);
            Assert.IsFalse(game.BallLaunched);
            Assert.AreEqual(BreakoutEventType.LifeLost, game.Events[game.Events.Count - 1].Type);
            Assert.AreEqual(game.PaddlePosition.X, game.BallPosition.X);
        }
    }
}
