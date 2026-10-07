using System;
using System.Linq;
using Lyking.Pong.Core;
using Xunit;

namespace Lyking.Pong.Core.Tests
{
    /// <summary>乒乓核心规则测试：确定性、手算反弹、AI 行为、得分与胜利流转。</summary>
    public class PongGameTests
    {
        private const float Epsilon = 0.03f;

        private static void AssertNear(float expected, float actual, float epsilon = Epsilon) =>
            Assert.True(MathF.Abs(expected - actual) <= epsilon, $"期望 {expected}，实际 {actual}。");

        /// <summary>AI 几乎不动的规则：让球穿过右挡板直接出界得分。</summary>
        private static PongRules WeakAiRules(int winScore = 7) => new PongRules(
            fieldWidth: 24f, fieldHeight: 14f,
            paddleWidth: 0.7f, paddleHeight: 2.8f, paddleSpeed: 10f, paddleOffsetX: 1f,
            aiPaddleSpeed: 0.01f, aiDeadZone: 0.35f,
            ballRadius: 0.35f, ballBaseSpeed: 9f, ballSpeedUpPerHit: 1.05f, maxBallSpeed: 18f,
            serveAngleRadians: 0.35f, winScore: winScore);

        /// <summary>高速度 AI 与低球速上限：两侧都能稳定回击，用于回合内计速到上限。</summary>
        private static PongRules RallyRules() => new PongRules(
            fieldWidth: 24f, fieldHeight: 14f,
            paddleWidth: 0.7f, paddleHeight: 2.8f, paddleSpeed: 10f, paddleOffsetX: 1f,
            aiPaddleSpeed: 20f, aiDeadZone: 0.35f,
            ballRadius: 0.35f, ballBaseSpeed: 9f, ballSpeedUpPerHit: 1.05f, maxBallSpeed: 10f,
            serveAngleRadians: 0.35f, winScore: 1000);

        /// <summary>除胜利分调大外全部默认：长跑触发事件封顶且不会终局。</summary>
        private static PongRules EndlessRules() => new PongRules(
            fieldWidth: 24f, fieldHeight: 14f,
            paddleWidth: 0.7f, paddleHeight: 2.8f, paddleSpeed: 10f, paddleOffsetX: 1f,
            aiPaddleSpeed: 8f, aiDeadZone: 0.35f,
            ballRadius: 0.35f, ballBaseSpeed: 9f, ballSpeedUpPerHit: 1.05f, maxBallSpeed: 18f,
            serveAngleRadians: 0.35f, winScore: 1000);

        [Fact]
        public void 构造后初始状态合法()
        {
            var game = new PongGame(PongRules.Default, seed: 42);

            Assert.Equal(0, game.LeftScore);
            Assert.Equal(0, game.RightScore);
            Assert.Equal(PongState.Running, game.State);
            Assert.False(game.IsFinished);
            AssertNear(12f, game.BallPosition.X, 0.001f);
            AssertNear(7f, game.BallPosition.Y, 0.001f);
            AssertNear(1f, game.LeftPaddlePosition.X, 0.001f);
            AssertNear(7f, game.LeftPaddlePosition.Y, 0.001f);
            AssertNear(23f, game.RightPaddlePosition.X, 0.001f);
            AssertNear(7f, game.RightPaddlePosition.Y, 0.001f);
            AssertNear(9f, game.BallSpeed, 0.001f);
            Assert.Equal(PongEventType.Spawned, game.Events[0].Type);
            Assert.Equal(PongEventType.Served, game.Events[^1].Type);
        }

        [Fact]
        public void 发球速度分量恒定且同种子完全一致()
        {
            var first = new PongGame(PongRules.Default, seed: 7);
            var second = new PongGame(PongRules.Default, seed: 7);

            // 发球角固定 0.35 弧度：垂直分量恒定 9*sin(0.35)，上下与左右朝向由种子随机
            AssertNear(9f * MathF.Sin(0.35f), MathF.Abs(first.BallVelocity.Y), 0.005f);
            AssertNear(9f * MathF.Cos(0.35f), MathF.Abs(first.BallVelocity.X), 0.005f);
            AssertNear(9f, first.BallVelocity.Length, 0.005f);

            Assert.Equal(first.BallVelocity, second.BallVelocity);
            Assert.Equal(first.BallPosition, second.BallPosition);
            Assert.Equal(first.Events.Count, second.Events.Count);
        }

        [Fact]
        public void 反射角纯函数手算断言()
        {
            AssertNear(0f, PongGame.ReflectAngle(0f), 0.0001f);
            AssertNear(MathF.PI / 6f, PongGame.ReflectAngle(0.5f), 0.0001f);
            AssertNear(MathF.PI / 3f, PongGame.ReflectAngle(1f), 0.0001f);
            AssertNear(-MathF.PI / 12f, PongGame.ReflectAngle(-0.25f), 0.0001f);
            // 越界偏移被夹到 ±60°
            AssertNear(MathF.PI / 3f, PongGame.ReflectAngle(2f), 0.0001f);
            AssertNear(-MathF.PI / 3f, PongGame.ReflectAngle(-5f), 0.0001f);
        }

        [Fact]
        public void AI目标纯函数追踪来球或回中()
        {
            // 球朝右挡板飞来（X 分量为正）时追踪球心高度
            Assert.Equal(5f, PongGame.AiTargetY(5f, 3f, 7f));
            // 球远离（X 分量为负或零）时回中等待
            Assert.Equal(7f, PongGame.AiTargetY(5f, -3f, 7f));
            Assert.Equal(7f, PongGame.AiTargetY(5f, 0f, 7f));
        }

        [Fact]
        public void 大步长按固定子步切分推进()
        {
            var game = new PongGame(PongRules.Default, seed: 7);
            Assert.True(game.ResetServe(0.05f, towardRight: true));

            game.Step(0.5f, 0f);

            Assert.True(game.StepIndex >= 60, "0.5 秒应至少推进 60 个 1/120 秒子步。");
            // 0.5 秒内球尚未触及挡板与墙，位移可手算：初速 (9cos0.05, 9sin0.05)
            AssertNear(12f + 9f * MathF.Cos(0.05f) * 0.5f, game.BallPosition.X, 0.01f);
            AssertNear(7f + 9f * MathF.Sin(0.05f) * 0.5f, game.BallPosition.Y, 0.01f);
        }

        [Fact]
        public void 上下墙反弹翻转垂直分量且保持模长()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            // 陡角度发球：垂直分量 9*sin(0.9) ≈ 7.05，约 0.94 秒抵达顶墙，早于挡板
            Assert.True(game.ResetServe(0.9f, towardRight: true));
            game.Step(1.2f, 0f);

            AssertNear(-9f * MathF.Sin(0.9f), game.BallVelocity.Y, 0.01f);
            AssertNear(9f * MathF.Cos(0.9f), game.BallVelocity.X, 0.01f);
            AssertNear(9f, game.BallVelocity.Length, 0.01f);
            // 顶墙在 y = 14 - 0.35 = 13.65，反弹后回落 0.25 秒：y ≈ 13.65 - 7.05*0.25 ≈ 11.89
            AssertNear(11.89f, game.BallPosition.Y, 0.05f);
            AssertNear(18.71f, game.BallPosition.X, 0.05f);
            var wall = game.Events.Single(e => e.Type == PongEventType.WallBounced);
            Assert.Equal(1, wall.Value);
        }

        [Fact]
        public void 左挡板反弹按命中偏移偏转且每回合加速()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            // 近水平发球向左：1.15 秒（138 个子步）抵达左挡板，命中时球心 y ≈ 7 - 0.45*1.15 ≈ 6.48，
            // 命中偏移 ≈ (6.48 - 7) / 1.75 ≈ -0.296，反射角 ≈ -0.296 * 60° ≈ -0.3095 弧度
            Assert.True(game.ResetServe(-0.05f, towardRight: false));
            game.Step(1.15f, 0f);

            AssertNear(9.45f * MathF.Cos(-0.3095f), game.BallVelocity.X, 0.03f);
            AssertNear(9.45f * MathF.Sin(-0.3095f), game.BallVelocity.Y, 0.03f);
            AssertNear(9.45f, game.BallSpeed, 0.001f);
            Assert.True(game.BallVelocity.X > 0f, "左挡板反弹后球应朝右飞行。");
            AssertNear(1.701f, game.BallPosition.X, 0.005f);
            var bounce = game.Events.Single(e => e.Type == PongEventType.PaddleBounced);
            Assert.Equal(1, bounce.Value);
        }

        [Fact]
        public void AI追踪来球并成功回击()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            Assert.True(game.ResetServe(0.05f, towardRight: true));
            game.Step(1.2f, 0f);

            // AI 以限速 8 向上追踪来球，球抵达右挡板时被反弹向左并加速
            Assert.True(game.RightPaddlePosition.Y > 7f, "AI 应向上追踪来球。");
            Assert.True(game.BallVelocity.X < 0f, "AI 回击后球应朝左飞行。");
            AssertNear(9.45f, game.BallSpeed, 0.001f);
            var bounce = game.Events.Single(e => e.Type == PongEventType.PaddleBounced);
            Assert.Equal(-1, bounce.Value);
        }

        [Fact]
        public void AI在球远离时回中并受死区抑制()
        {
            var game = new PongGame(PongRules.Default, seed: 7);

            Assert.True(game.ResetServe(0.05f, towardRight: true));
            game.Step(4f, 0f);

            // 球被 AI 回击后朝左远离，AI 目标回中并在 0.35 死区内停止
            AssertNear(7f, game.RightPaddlePosition.Y, 0.36f);
            Assert.Equal(PongState.Running, game.State);
        }

        [Fact]
        public void 球出界对方得分并发球朝失分方()
        {
            var game = new PongGame(WeakAiRules(), seed: 7);

            // 球朝右飞出右边界：左方得分，随后发球朝失分方（右侧）
            Assert.True(game.ResetServe(0.35f, towardRight: true));
            game.Step(2f, 0f);

            Assert.Equal(1, game.LeftScore);
            Assert.Equal(0, game.RightScore);
            Assert.True(game.BallVelocity.X > 0f, "发球应朝失分方（右侧）。");
            AssertNear(9f * MathF.Sin(0.35f), MathF.Abs(game.BallVelocity.Y), 0.005f);
            AssertNear(9f, game.BallSpeed, 0.001f);
            var scored = game.Events.Single(e => e.Type == PongEventType.Scored);
            Assert.Equal(1, scored.Value);
            Assert.Equal(PongEventType.Served, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].Value);
        }

        [Fact]
        public void 先到胜利分进入胜利状态并锁定()
        {
            var game = new PongGame(WeakAiRules(winScore: 2), seed: 7);

            Assert.True(game.ResetServe(0.35f, towardRight: true));
            for (int i = 0; i < 100 && !game.IsFinished; i++) game.Step(0.25f, 0f);

            Assert.Equal(PongState.LeftWon, game.State);
            Assert.True(game.IsFinished);
            Assert.Equal(2, game.LeftScore);
            Assert.Equal(0, game.RightScore);
            Assert.Equal(1, game.Events.Single(e => e.Type == PongEventType.Won).Value);

            // 终局后推进与重发均被拒绝
            var positionBefore = game.BallPosition;
            var stepBefore = game.StepIndex;
            Assert.False(game.ResetServe(0f, towardRight: true));
            game.Step(1f, 1f);
            Assert.Equal(positionBefore, game.BallPosition);
            Assert.Equal(stepBefore, game.StepIndex);
        }

        [Fact]
        public void 球速每回合加速且有上限()
        {
            var game = new PongGame(RallyRules(), seed: 11);

            // 测试内用简单追踪器驱动左挡板，两侧都能回击形成不死回合，球速按 +5% 递增至 10 上限
            for (int i = 0; i < 600 && game.BallSpeed < 10f; i++)
            {
                var move = Math.Clamp((game.BallPosition.Y - game.LeftPaddlePosition.Y) * 2f, -1f, 1f);
                game.Step(0.05f, move);
            }

            Assert.Equal(10f, game.BallSpeed);
            Assert.True(game.Events.Count(e => e.Type == PongEventType.PaddleBounced) >= 3, "至少三次挡板反弹。");
        }

        [Fact]
        public void 事件日志超过上限被裁剪()
        {
            var game = new PongGame(EndlessRules(), seed: 7);

            game.Step(600f, 0f);

            Assert.Equal(128, game.Events.Count);
            for (int i = 1; i < game.Events.Count; i++)
            {
                Assert.True(game.Events[i].Step >= game.Events[i - 1].Step, "事件必须按子步序号递增。");
            }

            Assert.True(game.Events[^1].Step > game.Events[0].Step, "裁剪后最早的完整事件应已被丢弃。");
        }
    }
}
