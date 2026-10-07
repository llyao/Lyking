using System;
using System.Linq;
using Lyking.Breakout.Core;
using Xunit;

namespace Lyking.Breakout.Core.Tests
{
    /// <summary>打砖块核心规则测试：确定性、手算轨迹、挡板反弹、砖块计分、生命与关卡流转。</summary>
    public class BreakoutGameTests
    {
        private const float Epsilon = 0.02f;

        private static void AssertNear(float expected, float actual, float epsilon = Epsilon) =>
            Assert.True(MathF.Abs(expected - actual) <= epsilon, $"期望 {expected}，实际 {actual}。");

        /// <summary>轨迹测试用规则：只在场地顶部留一块砖（[8.9,11.1]x[13.2,14.0]），其余区域供手算轨迹使用。</summary>
        private static BreakoutRules TrajectoryRules() => new BreakoutRules(
            fieldWidth: 20f, fieldHeight: 15f,
            paddleWidth: 4f, paddleHeight: 0.6f, paddleSpeed: 12f, paddleCenterY: 1.2f,
            ballRadius: 0.35f, ballBaseSpeed: 8f, ballSpeedPerLevel: 1.2f, maxBallSpeed: 16f, minVerticalSpeed: 2.2f,
            brickColumns: 1, brickRows: 1, brickTopY: 14f, brickWidth: 2.2f, brickHeight: 0.8f,
            brickGapX: 0.15f, brickGapY: 0.2f, lives: 3, topRowDurability: 1, rowScores: new[] { 5 });

        /// <summary>单砖居中规则：砖块 [8.9,11.1]x[12.2,13.0]，中轴 x=10 竖直发球必命中，用于过关测试。</summary>
        private static BreakoutRules SingleBrickRules() => new BreakoutRules(
            fieldWidth: 20f, fieldHeight: 15f,
            paddleWidth: 4f, paddleHeight: 0.6f, paddleSpeed: 12f, paddleCenterY: 1.2f,
            ballRadius: 0.35f, ballBaseSpeed: 8f, ballSpeedPerLevel: 1.2f, maxBallSpeed: 16f, minVerticalSpeed: 2.2f,
            brickColumns: 1, brickRows: 1, brickTopY: 13f, brickWidth: 2.2f, brickHeight: 0.8f,
            brickGapX: 0.15f, brickGapY: 0.2f, lives: 3, topRowDurability: 1, rowScores: new[] { 7 });

        /// <summary>把挡板从场地中心 10 左移到 8.2，竖直球道与轨迹规则单砖（左缘 8.9）保持 0.7 间距。</summary>
        private static BreakoutGame NewTrajectoryGame()
        {
            var game = new BreakoutGame(TrajectoryRules(), seed: 7);
            game.MovePaddle(-1f);
            game.Step(0.15f);
            game.MovePaddle(0f);
            AssertNear(8.2f, game.PaddlePosition.X);
            return game;
        }

        /// <summary>发球后让挡板移向远端让开球道，使竖直下落的球落底失命。</summary>
        private static void LoseBall(BreakoutGame game)
        {
            Assert.True(game.Launch(0f));
            game.Step(1f);
            game.MovePaddle(game.PaddlePosition.X >= 10f ? -1f : 1f);
            game.Step(1f);
            game.MovePaddle(0f);
            game.Step(2f);
        }

        [Fact]
        public void 构造后初始状态合法()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 42);

            Assert.Equal(0, game.Score);
            Assert.Equal(3, game.Lives);
            Assert.Equal(1, game.Level);
            Assert.False(game.IsGameOver);
            Assert.False(game.BallLaunched);
            AssertNear(10f, game.PaddlePosition.X);
            AssertNear(1.2f, game.PaddlePosition.Y);
            // 球贴在挡板上方：挡板顶 1.5 + 球半径 0.35
            AssertNear(10f, game.BallPosition.X);
            AssertNear(1.85f, game.BallPosition.Y);
            Assert.Equal(8f, game.BallSpeed);
            Assert.Equal(40, game.Bricks.Count);
            Assert.Equal(40, game.AliveBrickCount);
            // 顶行耐久 2，其余行 1；分值自顶向下 20/15/10/10/5
            Assert.Equal(2, game.Bricks.Single(b => b.Row == 0 && b.Column == 0).Durability);
            Assert.Equal(1, game.Bricks.Single(b => b.Row == 2 && b.Column == 3).Durability);
            Assert.Equal(20, game.Rules.ScoreForBrick(0));
            Assert.Equal(5, game.Rules.ScoreForBrick(4));
            Assert.Equal(BreakoutEventType.Spawned, game.Events[0].Type);
        }

        [Fact]
        public void 挡板移动被场地边界夹住()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 42);

            game.MovePaddle(1f);
            game.Step(2f);
            AssertNear(18f, game.PaddlePosition.X); // 场宽 20 - 挡板半宽 2

            game.MovePaddle(-1f);
            game.Step(3f);
            AssertNear(2f, game.PaddlePosition.X);

            // 待发球的球贴板随挡板移动
            AssertNear(2f, game.BallPosition.X);
            AssertNear(1.85f, game.BallPosition.Y);
        }

        [Fact]
        public void 竖直发球轨迹可手算()
        {
            var game = NewTrajectoryGame();

            Assert.True(game.Launch(0f));
            Assert.True(game.BallLaunched);
            AssertNear(0f, game.BallVelocity.X);
            AssertNear(8f, game.BallVelocity.Y);

            game.Step(0.5f);
            // 初高 1.85 + 8 * 0.5 = 5.85，尚未触及砖块区（底缘 13.2）
            AssertNear(8.2f, game.BallPosition.X);
            AssertNear(5.85f, game.BallPosition.Y);
        }

        [Fact]
        public void 指定角度发球初速可手算()
        {
            var game = NewTrajectoryGame();

            Assert.True(game.Launch(0.3f));
            // v = 8 * (sin 0.3, cos 0.3) ≈ (2.364, 7.643)，半秒后位移 (1.182, 3.821)
            AssertNear(8f * MathF.Sin(0.3f), game.BallVelocity.X);
            AssertNear(8f * MathF.Cos(0.3f), game.BallVelocity.Y);
            AssertNear(8f, game.BallVelocity.Length, 0.001f);

            game.Step(0.5f);
            AssertNear(8.2f + 4f * MathF.Sin(0.3f), game.BallPosition.X);
            AssertNear(1.85f + 4f * MathF.Cos(0.3f), game.BallPosition.Y);
        }

        [Fact]
        public void 顶墙反弹后垂直速度反向()
        {
            var game = NewTrajectoryGame();

            Assert.True(game.Launch(0f));
            game.Step(2f);
            // 顶墙在 y = 15 - 0.35 = 14.65：约 1.6 秒抵达反弹，之后下落约 3.2（触发子步有 1 步浮点误差）
            AssertNear(11.45f, game.BallPosition.Y, 0.1f);
            Assert.True(game.BallVelocity.Y < 0f, "顶墙反弹后应转为下落。");
            Assert.Contains(BreakoutEventType.WallBounced, game.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 侧墙反弹后水平速度反向()
        {
            var game = NewTrajectoryGame();

            Assert.True(game.Launch(MathF.PI / 4f));
            game.Step(2.1f);
            // 45° 发球：右墙 x = 19.65 约 2.02 秒抵达反弹，之后水平速度反向
            Assert.InRange(game.BallPosition.X, 19f, 19.65f);
            Assert.True(game.BallVelocity.X < 0f, "侧墙反弹后水平速度应反向。");
            Assert.True(game.BallVelocity.Y > 0f, "反弹只改变法向分量。");
            AssertNear(8f, game.BallVelocity.Length, 0.005f);
            Assert.Contains(BreakoutEventType.WallBounced, game.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 挡板反弹按命中偏移成比例偏转且保持恒定模长()
        {
            var game = NewTrajectoryGame();

            Assert.True(game.Launch(0f));
            game.Step(1f);
            // 挡板左移 2.0：命中偏移 = 2.0 / (2 + 0.35) ≈ 0.851，反射角约 51°
            game.MovePaddle(-1f);
            game.Step(1f / 6f);
            game.MovePaddle(0f);

            // 先等球越过顶点转为下落，再等到挡板反弹转上升（上升段 vy 本身大于 0，不能直接判断）
            for (int i = 0; i < 48 && game.BallVelocity.Y > 0f; i++) game.Step(0.25f);
            for (int i = 0; i < 48 && game.BallVelocity.Y < 0f; i++) game.Step(0.25f);
            Assert.True(game.BallVelocity.Y > 0f, "球应在挡板上反弹转为上升。");
            var offset = Math.Clamp((8.2f - game.PaddlePosition.X) / 2.35f, -1f, 1f);
            var angle = offset * (MathF.PI / 3f);
            AssertNear(8f * MathF.Sin(angle), game.BallVelocity.X, 0.03f);
            AssertNear(8f * MathF.Cos(angle), game.BallVelocity.Y, 0.03f);
            AssertNear(8f, game.BallVelocity.Length, 0.005f);
            Assert.Contains(BreakoutEventType.PaddleBounced, game.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 击毁砖块加分并记录事件()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            // 挡板移到 13，正对第 6 列砖（x ∈ [12.425, 14.625]）
            game.MovePaddle(1f);
            game.Step(0.25f);
            game.MovePaddle(0f);
            Assert.True(game.Launch(0f));

            game.Step(1f);
            // 底行砖（第 4 行，分值 5）在 y = 8.2，球心 0.75 秒后触达并被击破
            Assert.Equal(5, game.Score);
            Assert.Equal(39, game.AliveBrickCount);
            Assert.True(game.BallVelocity.Y < 0f, "击砖后应反射下落。");

            var destroyed = game.Events.Single(e => e.Type == BreakoutEventType.BrickDestroyed);
            Assert.Equal(5, destroyed.Value);
        }

        [Fact]
        public void 顶行耐久砖需两次击破且首次受击不加分()
        {
            var game = new BreakoutGame(BreakoutRules.Default, seed: 7);

            game.MovePaddle(1f);
            game.Step(0.25f);
            game.MovePaddle(0f);
            Assert.True(game.Launch(0f));

            // 竖直球道反复击穿第 6 列：行分值 5+10+10+15+20 = 60
            for (int i = 0; i < 400 && game.Score < 60; i++) game.Step(0.5f);

            Assert.Equal(60, game.Score);
            Assert.Equal(35, game.AliveBrickCount); // 第 6 列 5 块砖全部击破（顶行 2 次击打计 1 块）
            Assert.Equal(0, game.Bricks.Single(b => b.Row == 0 && b.Column == 5).Durability);
            // 仅顶行耐久砖产生一次受击事件；若首击加分则总分应为 80
            Assert.Equal(1, game.Events.Count(e => e.Type == BreakoutEventType.BrickDamaged));
            Assert.Equal(5, game.Events.Count(e => e.Type == BreakoutEventType.BrickDestroyed));
            Assert.Equal(1, game.Events.Single(e => e.Type == BreakoutEventType.BrickDamaged).Value);
        }

        [Fact]
        public void 球落底失命后重新贴板等待发球()
        {
            var game = NewTrajectoryGame();

            LoseBall(game);

            Assert.Equal(2, game.Lives);
            Assert.False(game.BallLaunched);
            Assert.Equal(BreakoutEventType.LifeLost, game.Events[^1].Type);
            Assert.Equal(2, game.Events[^1].Value);
            // 球重新贴在挡板上方等待发射
            AssertNear(game.PaddlePosition.X, game.BallPosition.X);
            AssertNear(1.85f, game.BallPosition.Y);
        }

        [Fact]
        public void 生命耗尽后游戏结束且拒绝继续操作()
        {
            var game = NewTrajectoryGame();

            LoseBall(game);
            LoseBall(game);
            LoseBall(game);

            Assert.Equal(0, game.Lives);
            Assert.True(game.IsGameOver);
            Assert.Equal(BreakoutEventType.GameOver, game.Events[^1].Type);
            Assert.Equal(2, game.Events.Count(e => e.Type == BreakoutEventType.LifeLost));

            Assert.False(game.Launch());
            Assert.False(game.Launch(0.4f));
            var stepBefore = game.StepIndex;
            var ballBefore = game.BallPosition;
            game.Step(1f);
            game.MovePaddle(1f);
            Assert.Equal(stepBefore, game.StepIndex);
            Assert.Equal(ballBefore, game.BallPosition);
        }

        [Fact]
        public void 清空砖块进入下一关且球速提升()
        {
            var game = new BreakoutGame(SingleBrickRules(), seed: 7);

            // 单砖居中 [8.9,11.1]x[12.2,13.0]，中轴竖直发球 1.25 秒命中击破
            Assert.True(game.Launch(0f));
            game.Step(1.5f);

            Assert.Equal(7, game.Score);
            Assert.Equal(2, game.Level);
            Assert.Equal(1, game.AliveBrickCount);
            Assert.False(game.BallLaunched);
            AssertNear(9.2f, game.BallSpeed); // 8 + 每关 1.2
            Assert.Equal(2, game.Events.Single(e => e.Type == BreakoutEventType.LevelCleared).Value);
            Assert.Equal(1, game.Bricks[0].Durability);
            Assert.Equal(7, game.Bricks[0].Score);

            // 新一关以提升后的球速重新发球
            Assert.True(game.Launch(0f));
            AssertNear(9.2f, game.BallVelocity.Length, 0.005f);
        }

        [Fact]
        public void 相同种子发球与模拟完全一致()
        {
            static void Play(BreakoutGame game)
            {
                game.Launch();
                game.Step(3f);
            }

            var first = new BreakoutGame(BreakoutRules.Default, seed: 7);
            var second = new BreakoutGame(BreakoutRules.Default, seed: 7);
            Play(first);
            Play(second);

            Assert.Equal(first.BallVelocity, second.BallVelocity);
            Assert.Equal(first.BallPosition, second.BallPosition);
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.StepIndex, second.StepIndex);
        }

        [Fact]
        public void 事件日志超过上限被裁剪()
        {
            var game = NewTrajectoryGame();

            // 竖直球道在顶墙与挡板间循环反弹，每 3.2 秒产生 2 条事件，长跑触发封顶裁剪
            Assert.True(game.Launch(0f));
            game.Step(400f);

            Assert.Equal(128, game.Events.Count);
            for (int i = 1; i < game.Events.Count; i++)
            {
                Assert.True(game.Events[i].Step >= game.Events[i - 1].Step, "事件必须按子步序号递增。");
            }

            Assert.True(game.Events[^1].Step > game.Events[0].Step, "裁剪后最早的完整事件应已被丢弃。");
        }
    }
}
