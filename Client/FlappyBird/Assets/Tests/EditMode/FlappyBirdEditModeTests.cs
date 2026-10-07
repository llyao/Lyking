using System.Linq;
using Lyking.FlappyBird.Core;
using NUnit.Framework;

namespace Lyking.FlappyBird.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class FlappyBirdEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建并就绪悬停()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 7);

            Assert.AreEqual(FlappyPhase.Ready, game.Phase);
            Assert.AreEqual(FlappyRules.Default.HoverHeight, game.Position.Y);
            Assert.AreEqual(0, game.Score);
            Assert.IsEmpty(game.Pipes);
            Assert.IsFalse(game.IsFinished);
            Assert.AreEqual(FlappyEventType.Spawned, game.Events[0].Type);
        }

        [Test]
        public void 首次点按起飞后重力逐步下坠()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 7);

            Assert.IsTrue(game.Advance(0.25f, flap: true));
            Assert.AreEqual(FlappyPhase.Running, game.Phase);
            Assert.AreEqual(6.25f, game.Position.Y);

            Assert.IsTrue(game.Advance(0.25f, flap: false));
            Assert.AreEqual(5.25f, game.Position.Y);
            Assert.AreEqual(-4f, game.Velocity.Y);
        }

        [Test]
        public void 管道按固定间距生成且同种子可复现()
        {
            var game = new FlappyBirdGame(WideGapRules(), seed: 11);
            for (int i = 0; i < 24; i++) Assert.IsTrue(game.Advance(0.25f, flap: true));

            Assert.AreEqual(2, game.Pipes.Count);
            Assert.AreEqual(13.5f, game.Pipes[0].Center.X);
            Assert.AreEqual(20.5f, game.Pipes[1].Center.X);

            var again = new FlappyBirdGame(WideGapRules(), seed: 11);
            for (int i = 0; i < 24; i++) again.Advance(0.25f, flap: true);
            Assert.AreEqual(game.Pipes[0].Center.Y, again.Pipes[0].Center.Y);
        }

        [Test]
        public void 触地死亡后重置恢复就绪并记录事件()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 7);
            game.Advance(0.25f, flap: true);
            for (int i = 0; i < 10 && !game.IsFinished; i++) game.Advance(0.25f, flap: false);

            Assert.IsTrue(game.IsFinished);
            Assert.AreEqual(FlappyEventType.HitGround, game.Events[game.Events.Count - 1].Type);

            game.Reset(5);
            Assert.AreEqual(FlappyPhase.Ready, game.Phase);
            Assert.AreEqual(FlappyRules.Default.HoverHeight, game.Position.Y);
            Assert.IsTrue(game.Events.Any(e => e.Type == FlappyEventType.Reset));
            Assert.IsTrue(game.Advance(0.25f, flap: true));
            Assert.AreEqual(FlappyPhase.Running, game.Phase);
        }

        /// <summary>缺口贯穿整个场地高度：管道不会碰撞，专用于考察滚动节奏。</summary>
        private static FlappyRules WideGapRules() => new FlappyRules(
            fieldWidth: 20f, fieldHeight: 12f, gravity: 20f, flapSpeed: 6f, maxFallSpeed: 10f,
            birdX: 5f, birdHalfWidth: 0.25f, birdHalfHeight: 0.25f, hoverHeight: 6f,
            pipeSpeed: 3.5f, pipeWidth: 1f, pipeSpacing: 7f, gapHeight: 12f,
            gapCenterMin: 6f, gapCenterMax: 6f, firstPipeDistance: 14f);
    }
}
