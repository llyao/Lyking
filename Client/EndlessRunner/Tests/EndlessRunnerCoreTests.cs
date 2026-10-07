using System;
using System.Linq;
using Lyking.EndlessRunner.Core;
using Xunit;

namespace Lyking.EndlessRunner.Core.Tests
{
    /// <summary>无尽跑酷核心规则测试：确定性、跳跃抛物线、二段跳、快落、障碍生成、碰撞、越障、计分与速度增长。</summary>
    public class EndlessRunnerGameTests
    {
        /// <summary>首障碍立即生成且速度恒定（每步滚动 5×0.25=1.25 米）：专用于考察生成、碰撞与越障。</summary>
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

        /// <summary>永不生成障碍：专用于考察跳跃、快落与事件封顶，不受碰撞干扰。</summary>
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

        [Fact]
        public void 构造后初始状态为落地待命()
        {
            var game = new EndlessRunnerGame(RunnerRules.Default, seed: 42);

            Assert.Equal(new Vec2(RunnerRules.Default.PlayerX, RunnerRules.Default.PlayerHalfHeight), game.PlayerPosition);
            Assert.True(game.IsGrounded);
            Assert.Equal(0f, game.VelocityY);
            Assert.Equal(0, game.JumpsUsed);
            Assert.False(game.IsFastFalling);
            Assert.Equal(0f, game.Distance);
            Assert.Equal(0, game.Score);
            Assert.Equal(RunnerRules.Default.BaseSpeed, game.Speed);
            Assert.Empty(game.Obstacles);
            Assert.False(game.IsFinished);
            Assert.Equal(0, game.StepIndex);
            Assert.Single(game.Events);
            Assert.Equal(RunnerEventType.Spawned, game.Events[0].Type);
        }

        [Fact]
        public void 静止待机时世界滚动并按换算计分()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 2);
            for (int i = 0; i < 8; i++) Assert.True(game.Advance(0.25f, RunnerInput.None));

            // 恒速 5 米/秒 × 0.25 秒 × 8 步 = 10 米；分数 = 10 × 每米 10 分 = 100。
            Assert.Equal(10f, game.Distance);
            Assert.Equal(100, game.Score);
            Assert.Equal(5f, game.Speed);
            Assert.Equal(8, game.StepIndex);
        }

        [Fact]
        public void 跳跃抛物线按整数子步手算吻合()
        {
            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 42);

            // 每步重力增量 20×0.25=5：跳后 vy=8-5=3，y=0.5+3×0.25=1.25。
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.Equal(1.25, game.PlayerPosition.Y, 3);
            Assert.Equal(3f, game.VelocityY);
            Assert.Equal(1, game.JumpsUsed);
            Assert.False(game.IsGrounded);
            Assert.Equal(RunnerEventType.Jumped, game.Events[^1].Type);

            // 第 2 步 vy=3-5=-2，y=1.25-0.5=0.75。
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Equal(0.75, game.PlayerPosition.Y, 3);
            Assert.Equal(-2f, game.VelocityY);

            // 第 3 步 vy=-7，y=-1 触地截停为 0.5：站稳、跳段清零，地面不是致死物。
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Equal(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.Equal(0f, game.VelocityY);
            Assert.True(game.IsGrounded);
            Assert.Equal(0, game.JumpsUsed);
            Assert.Equal(RunnerEventType.Landed, game.Events[^1].Type);
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 空中二段跳仅一次且第三跳被忽略()
        {
            Assert.True(RunnerRules.Default.DoubleJumpSpeed < RunnerRules.Default.JumpSpeed);

            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 42);
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));

            // 第 2 步二段跳：速度置顶为 5 再减 5 得 0，y 维持 1.25（不加二段跳时会落到 0.75）。
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.Equal(1.25, game.PlayerPosition.Y, 3);
            Assert.Equal(0f, game.VelocityY);
            Assert.Equal(2, game.JumpsUsed);
            Assert.Equal(RunnerEventType.DoubleJumped, game.Events[^1].Type);

            // 第 3 步跳段用尽：按下沿被忽略，不产生新事件并自由落体触地。
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.Equal(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.Equal(0, game.JumpsUsed);
            Assert.Equal(1, game.Events.Count(e => e.Type == RunnerEventType.DoubleJumped));
            Assert.Equal(RunnerEventType.Landed, game.Events[^1].Type);
        }

        [Fact]
        public void 落地重置跳段后可再次起跳()
        {
            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 42);
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.True(game.IsGrounded);
            Assert.Equal(0, game.JumpsUsed);

            // 落地后跳段已重置，可再次消耗首跳。
            Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.Equal(1.25, game.PlayerPosition.Y, 3);
            Assert.Equal(1, game.JumpsUsed);
            Assert.Equal(RunnerEventType.Jumped, game.Events[^1].Type);
        }

        [Fact]
        public void 快落把重力按倍率放大()
        {
            // 两局同种子走到同一分叉点（跳 + 二段跳后 y=1.4、vy=3），第 3 步起 A 快落、B 不按。
            var fast = new EndlessRunnerGame(NoObstacleRules(), seed: 42);
            var normal = new EndlessRunnerGame(NoObstacleRules(), seed: 42);
            for (int i = 0; i < 2; i++)
            {
                fast.Advance(0.1f, new RunnerInput(true, false));
                normal.Advance(0.1f, new RunnerInput(true, false));
            }

            // 快落重力 60×0.1=6/步：vy=3-6=-3，y=1.4-0.3=1.1；正常 20×0.1=2/步：vy=1，y=1.5。
            Assert.True(fast.Advance(0.1f, new RunnerInput(false, true)));
            Assert.True(normal.Advance(0.1f, RunnerInput.None));
            Assert.Equal(-3f, fast.VelocityY);
            Assert.Equal(1.1, fast.PlayerPosition.Y, 3);
            Assert.True(fast.IsFastFalling);
            Assert.Equal(RunnerEventType.FastFallStarted, fast.Events[^1].Type);
            Assert.Equal(1f, normal.VelocityY);
            Assert.Equal(1.5, normal.PlayerPosition.Y, 3);
            Assert.False(normal.IsFastFalling);

            // 快落第 4 步 vy=-9、y=0.2 触地截停：落地同时结束快落状态。
            Assert.True(fast.Advance(0.1f, new RunnerInput(false, true)));
            Assert.Equal(RunnerRules.Default.PlayerHalfHeight, fast.PlayerPosition.Y);
            Assert.Equal(0f, fast.VelocityY);
            Assert.True(fast.IsGrounded);
            Assert.False(fast.IsFastFalling);
            Assert.Equal(RunnerEventType.Landed, fast.Events[^1].Type);
        }

        [Fact]
        public void 地面按住快落键无效()
        {
            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 42);
            for (int i = 0; i < 5; i++) Assert.True(game.Advance(0.25f, new RunnerInput(false, true)));

            Assert.Equal(RunnerRules.Default.PlayerHalfHeight, game.PlayerPosition.Y);
            Assert.False(game.IsFastFalling);
            Assert.DoesNotContain(game.Events, e => e.Type == RunnerEventType.FastFallStarted);
            // 世界照常滚动：5 步 × 1.25 = 6.25 米。
            Assert.Equal(6.25, game.Distance, 3);
        }

        [Fact]
        public void 障碍高矮与间距由固定种子手算吻合()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 7);

            // 第 1 步滚动 1.25 后触发生成：d0=0.383220 < 0.5 → 高障碍，中心 (20.4, 0.6)。
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Single(game.Obstacles);
            Assert.Equal(20.4, game.Obstacles[0].Center.X, 3);
            Assert.Equal(0.6, game.Obstacles[0].Center.Y, 3);
            Assert.True(game.Obstacles[0].IsTall);

            // 间距 d1=0.871256 → 4+0.871256×4=7.485024，第 6 步累计 7.5 ≥ 7.485024 时生成第二个；
            // d2=0.660939 ≥ 0.5 → 矮障碍 (20.4, 0.3)，首障碍已左移 1.25×5=6.25 至 14.15。
            for (int i = 0; i < 4; i++) Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Single(game.Obstacles);
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Equal(2, game.Obstacles.Count);
            Assert.Equal(14.15, game.Obstacles[0].Center.X, 3);
            Assert.Equal(20.4, game.Obstacles[1].Center.X, 3);
            Assert.Equal(0.3, game.Obstacles[1].Center.Y, 3);
            Assert.False(game.Obstacles[1].IsTall);

            // 间距 d3=0.052262 → 4.209048，第 10 步生成第三个；d4=0.366433 < 0.5 → 高障碍。
            for (int i = 0; i < 4; i++) Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Equal(3, game.Obstacles.Count);
            Assert.True(game.Obstacles[2].IsTall);
            Assert.Equal(20.4, game.Obstacles[2].Center.X, 3);
            Assert.Equal(0.6, game.Obstacles[2].Center.Y, 3);
        }

        [Fact]
        public void 撞上障碍立即结束且之后推进无效()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 1);
            for (int i = 0; i < 13; i++) Assert.True(game.Advance(0.25f, RunnerInput.None));
            // 玩家右缘 4.4：第 13 步障碍左缘 5.4-0.4=5.0 未重叠。
            Assert.False(game.IsFinished);
            Assert.Equal(5.4, game.Obstacles[0].Center.X, 3);

            // 第 14 步障碍 X=4.15、左缘 3.75 < 4.4，与站立玩家重叠即死亡。
            Assert.False(game.Advance(0.25f, RunnerInput.None));
            Assert.True(game.IsGameOver);
            Assert.Equal(RunnerEventType.HitObstacle, game.Events[^1].Type);
            Assert.Equal(14, game.StepIndex);
            Assert.Equal(4.15, game.Obstacles[0].Center.X, 3);

            // 结束后推进无效：状态完整冻结。
            Assert.False(game.Advance(0.25f, new RunnerInput(true, false)));
            Assert.Equal(14, game.StepIndex);
            Assert.Equal(4.15, game.Obstacles[0].Center.X, 3);
        }

        [Fact]
        public void 跃过矮障碍记录越过事件()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 2);

            // 步长 0.1、恒速 5：首障碍（矮，顶 0.6）在第 33~35 步与玩家横 overlap，
            // 第 32 步起跳后玩家底部高度 1.0/1.2/1.2 全程在其上方。
            for (int i = 1; i <= 36; i++)
            {
                bool alive = game.Advance(0.1f, i == 32 ? new RunnerInput(true, false) : RunnerInput.None);
                if (i < 36) Assert.True(alive);
            }

            Assert.False(game.IsFinished);
            Assert.True(game.Obstacles[0].Passed);
            Assert.Equal(RunnerEventType.ClearedObstacle, game.Events[^1].Type);
            Assert.Equal(1, game.Events.Count(e => e.Type == RunnerEventType.ClearedObstacle));
        }

        [Fact]
        public void 速度随距离线性提升并被上限夹住()
        {
            var rules = RunnerRules.Default;
            Assert.Equal(5f, rules.SpeedFor(0f));
            Assert.Equal(6f, rules.SpeedFor(100f));
            Assert.Equal(9f, rules.SpeedFor(400f));
            Assert.Equal(9f, rules.SpeedFor(1000f));

            // 默认规则每米提速 0.01：8 步后距离超过恒速参照 10 米，实时速度等于规则纯函数。
            var game = new EndlessRunnerGame(rules, seed: 1);
            for (int i = 0; i < 8; i++) Assert.True(game.Advance(0.25f, RunnerInput.None));

            Assert.True(game.Distance > 10f);
            Assert.True(game.Speed > rules.BaseSpeed);
            Assert.Equal(rules.SpeedFor(game.Distance), game.Speed);
        }

        [Fact]
        public void 相同种子两次模拟完全一致且不同种子高矮不同()
        {
            static int Simulate(int seed)
            {
                var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed);
                for (int i = 1; i <= 30; i++) game.Advance(0.25f, i % 5 == 0 ? new RunnerInput(true, false) : RunnerInput.None);
                return HashCode.Combine(game.Score, game.StepIndex, game.Distance, game.PlayerPosition,
                    game.IsGameOver, string.Join(';', game.Obstacles));
            }

            Assert.Equal(Simulate(7), Simulate(7));

            // 种子 2 首随机 d0=0.771094 ≥ 0.5 → 矮；种子 7 首随机 d0=0.383220 < 0.5 → 高。
            var shortFirst = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 2);
            var tallFirst = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 7);
            shortFirst.Advance(0.25f, RunnerInput.None);
            tallFirst.Advance(0.25f, RunnerInput.None);
            Assert.False(shortFirst.Obstacles[0].IsTall);
            Assert.True(tallFirst.Obstacles[0].IsTall);
        }

        [Fact]
        public void 事件日志封顶裁剪保留最近记录()
        {
            var game = new EndlessRunnerGame(NoObstacleRules(), seed: 42);

            // 连跳每 3 步产生 Jumped/DoubleJumped/Landed：200 步加 Spawned 共 201 条，封顶保留最近 128 条。
            for (int i = 0; i < 200; i++) Assert.True(game.Advance(0.25f, new RunnerInput(true, false)));

            Assert.Equal(128, game.Events.Count);
            Assert.Equal(RunnerEventType.Jumped, game.Events[0].Type);
            Assert.Equal(73, game.Events[0].Step);
            Assert.Equal(RunnerEventType.DoubleJumped, game.Events[^1].Type);
            Assert.Equal(200, game.Events[^1].Step);
        }

        [Fact]
        public void 结束后重置恢复初始状态()
        {
            var game = new EndlessRunnerGame(ImmediateObstacleRules(), seed: 1);
            for (int i = 0; i < 20 && !game.IsFinished; i++) game.Advance(0.25f, RunnerInput.None);
            Assert.True(game.IsFinished);

            game.Reset(9);
            Assert.Equal(new Vec2(ImmediateObstacleRules().PlayerX, ImmediateObstacleRules().PlayerHalfHeight), game.PlayerPosition);
            Assert.True(game.IsGrounded);
            Assert.Equal(0, game.JumpsUsed);
            Assert.False(game.IsFastFalling);
            Assert.Equal(0f, game.Distance);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.StepIndex);
            Assert.Empty(game.Obstacles);
            Assert.False(game.IsFinished);
            Assert.Single(game.Events);
            Assert.Equal(RunnerEventType.Reset, game.Events[0].Type);

            // 重置后可正常推进：世界重新滚动并计分。
            Assert.True(game.Advance(0.25f, RunnerInput.None));
            Assert.Equal(1.25, game.Distance, 3);
            Assert.Equal(12, game.Score);
        }
    }
}
