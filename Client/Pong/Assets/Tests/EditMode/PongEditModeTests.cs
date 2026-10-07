using System;
using System.Linq;
using Lyking.Pong.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Pong.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class PongEditModeTests
    {
        private static void AssertNear(float expected, float actual, float epsilon = 0.03f) =>
            Assert.IsTrue(Mathf.Abs(expected - actual) <= epsilon, $"期望 {expected}，实际 {actual}。");

        [Test]
        public void 对局在引擎外可创建且初始发球就绪()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            Assert.AreEqual(0, game.LeftScore);
            Assert.AreEqual(0, game.RightScore);
            Assert.AreEqual(PongState.Running, game.State);
            Assert.IsFalse(game.IsFinished);
            AssertNear(12f, game.BallPosition.X, 0.001f);
            AssertNear(7f, game.BallPosition.Y, 0.001f);
            AssertNear(9f, game.BallSpeed, 0.001f);
            Assert.AreEqual(PongEventType.Spawned, game.Events[0].Type);
            Assert.AreEqual(PongEventType.Served, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 左挡板反弹按命中偏移偏转且加速()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            Assert.IsTrue(game.ResetServe(-0.05f, towardRight: false));
            game.Step(1.15f, 0f);

            Assert.Greater(game.BallVelocity.X, 0f);
            AssertNear(9.45f, game.BallSpeed, 0.001f);
            Assert.AreEqual(PongEventType.PaddleBounced, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 上下墙反弹翻转垂直分量()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            Assert.IsTrue(game.ResetServe(0.9f, towardRight: true));
            game.Step(1.2f, 0f);

            Assert.Less(game.BallVelocity.Y, 0f);
            AssertNear(9f * Mathf.Sin(0.9f), Mathf.Abs(game.BallVelocity.Y), 0.01f);
            Assert.IsTrue(game.Events.Any(e => e.Type == PongEventType.WallBounced));
        }

        [Test]
        public void 得分后发球朝失分方()
        {
            // AI 几乎不动：球穿过右挡板直接出界，左方得分
            var rules = new PongRules(
                fieldWidth: 24f, fieldHeight: 14f,
                paddleWidth: 0.7f, paddleHeight: 2.8f, paddleSpeed: 10f, paddleOffsetX: 1f,
                aiPaddleSpeed: 0.01f, aiDeadZone: 0.35f,
                ballRadius: 0.35f, ballBaseSpeed: 9f, ballSpeedUpPerHit: 1.05f, maxBallSpeed: 18f,
                serveAngleRadians: 0.35f, winScore: 7);
            var game = new PongGame(rules, seed: 7);

            Assert.IsTrue(game.ResetServe(0.35f, towardRight: true));
            game.Step(2f, 0f);

            Assert.AreEqual(1, game.LeftScore);
            Assert.AreEqual(0, game.RightScore);
            Assert.Greater(game.BallVelocity.X, 0f);
            Assert.AreEqual(PongState.Running, game.State);
        }
    }
}
