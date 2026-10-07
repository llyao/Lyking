using System.Linq;
using Lyking.EndlessRunner.Core;
using NUnit.Framework;

namespace Lyking.EndlessRunner.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class EndlessRunnerEditModeTests
    {
        /// <summary>首障碍立即生成且速度恒定：专用于考察生成节奏与碰撞。</summary>
        private static RunnerRules ImmediateObstacleRules() => new RunnerRules(
            fieldWidth: 20f, fieldHeight: 10f, groundHeight: 0f, playerX: 4f,
            playerHalfWidth: 0.4f, playerHalfHeight: 0.5f,
            gravity: 20f, jumpSpeed: 8f, doubleJumpSpeed: 5f, maxJumps: 2,
            maxFallSpeed: 12f, fastFallMultiplier: 3f,
            shortObstacleWidth: 0.8f, shortObstacleHeight: 0.6f,
            tallObstacleWidth: 0.8f, tallObstacleHeight: 1.2f,
            baseSpeed: 5f, maxSpeed: 9f, speedGainPerMeter: 0f,
            minSpacing: 4f, maxSpacing: 8f, tallObstacleChance: 0.5f,
            firstObstacleDistance: 0f, scorePerMeter: 10);

        /// <summary>永不生成障碍：专用于考察跳跃轨迹。</summary>
        private static RunnerRules NoObstacleRules() => new RunnerRules(
            fieldWidth: 20f, fieldHeight: 10f, groundHeight: 0f, playerX: 4f,
            playerHalfWidth: 0.4f, playerHalfHeight: 0.5f,
            gravity: 20f, jumpSpeed: 8f, doubleJumpSpeed: 5f, maxJumps: 2,
            maxFallSpeed: 12f, fastFallMultiplier: 3f,
            shortObstacleWidth: 0.8f, shortObstacleHeight: 0.6f,
            tallObstacleWidth: 0.8f, tallObstacleHeight: 1.2f,
            baseSpeed: 5f, maxSpeed: 9f, speedGainPerMeter: 0f,
            minSpacing: 4f, maxSpacing: 8f, tallObstacleChance: 0.5f,
            firstObstacleDistance: 100000f, scorePerMeter: 10);

        [Test]
        public void 核心在引擎边界内可创建并落地待命()
        {
            var game = new EndlessRunnerGame(RunnerRules.Default, seed: 7);

            Assert.AreEqual(RunnerRules.Default.PlayerX, game.PlayerPosition.X);
            Assert.AreEqual(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.IsTrue(game.IsGrounded);
            Assert.AreEqual(0, game.Score);
            Assert.IsEmpty(game.Obstacles);
            Assert.IsFalse(game.IsFinished);
            Assert.AreEqual(RunnerRules.Default.BaseSpeed, game.Speed);
            Assert.AreEqual(RunnerEventType.Spawned, game.Events[0].Type);
        }

        [Test]
        public void 跳跃二段跳与落地轨迹表现一致()
        {
            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 7);

            Assert.IsTrue(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.AreEqual(1.25f, game.PlayerPosition.Y, 0.001f);
            Assert.AreEqual(1, game.JumpsUsed);
            Assert.AreEqual(RunnerEventType.Jumped, game.Events[game.Events.Count - 1].Type);

            // 空中二段跳把速度置顶为 5：净速度 5-5=0，高度维持 1.25。
            Assert.IsTrue(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.AreEqual(1.25f, game.PlayerPosition.Y, 0.001f);
            Assert.AreEqual(2, game.JumpsUsed);
            Assert.AreEqual(RunnerEventType.DoubleJumped, game.Events[game.Events.Count - 1].Type);

            // 第 3 步触地截停，跳段清零。
            Assert.IsTrue(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.AreEqual(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.AreEqual(0, game.JumpsUsed);
            Assert.IsTrue(game.IsGrounded);
            Assert.AreEqual(RunnerEventType.Landed, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 障碍按固定种子生成且同种子可复现()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 7);
            for (int i = 0; i < 6; i++) Assert.IsTrue(game.Advance(0.25f, new RunnerInput(false, false)));

            Assert.AreEqual(2, game.Obstacles.Count);
            Assert.AreEqual(14.15f, game.Obstacles[0].Center.X, 0.001f);
            Assert.AreEqual(20.4f, game.Obstacles[1].Center.X, 0.001f);
            Assert.IsTrue(game.Obstacles[0].IsTall);
            Assert.IsFalse(game.Obstacles[1].IsTall);

            // 同种子重跑生成序列逐位一致。
            var again = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 7);
            for (int i = 0; i < 6; i++) again.Advance(0.25f, new RunnerInput(false, false));
            Assert.AreEqual(game.Obstacles[0].Center.X, again.Obstacles[0].Center.X);
            Assert.AreEqual(game.Obstacles[1].Center.X, again.Obstacles[1].Center.X);
            Assert.AreEqual(game.Obstacles[0].IsTall, again.Obstacles[0].IsTall);
        }

        [Test]
        public void 撞障碍结束后重置恢复落地待命()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 1);
            for (int i = 0; i < 20 && !game.IsFinished; i++) game.Advance(0.25f, new RunnerInput(false, false));

            Assert.IsTrue(game.IsFinished);
            Assert.AreEqual(RunnerEventType.HitObstacle, game.Events[game.Events.Count - 1].Type);

            game.Reset(5);
            Assert.AreEqual(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.IsTrue(game.IsGrounded);
            Assert.AreEqual(0f, game.Distance);
            Assert.IsEmpty(game.Obstacles);
            Assert.IsTrue(game.Events.Any(e => e.Type == RunnerEventType.Reset));
            Assert.IsTrue(game.Advance(0.25f, new RunnerInput(false, false)));
            Assert.Greater(game.Distance, 0f);
        }
    }
}
