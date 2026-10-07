using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.FlappyBird.Core;
using Xunit;

namespace Lyking.FlappyBird.Core.Tests
{
    /// <summary>Flappy 核心规则测试：确定性、重力轨迹、管道序列、得分、碰撞、事件封顶与重置。</summary>
    public class FlappyBirdGameTests
    {
        /// <summary>缺口贯穿整个场地高度：管道不会碰撞，专用于考察滚动、得分与事件日志。</summary>
        private static FlappyRules WideGapRules() => new FlappyRules(
            fieldWidth: 20f, fieldHeight: 12f, gravity: 20f, flapSpeed: 6f, maxFallSpeed: 10f,
            birdX: 5f, birdHalfWidth: 0.25f, birdHalfHeight: 0.25f, hoverHeight: 6f,
            pipeSpeed: 3.5f, pipeWidth: 1f, pipeSpacing: 7f, gapHeight: 12f,
            gapCenterMin: 6f, gapCenterMax: 6f, firstPipeDistance: 14f);

        /// <summary>重力减半：小鸟靠交替扇翅在悬停高度附近长期往复，用于考察撞管道。</summary>
        private static FlappyRules SlowGravityRules() => new FlappyRules(
            fieldWidth: 20f, fieldHeight: 12f, gravity: 8f, flapSpeed: 6f, maxFallSpeed: 10f,
            birdX: 5f, birdHalfWidth: 0.25f, birdHalfHeight: 0.25f, hoverHeight: 6f,
            pipeSpeed: 3.5f, pipeWidth: 1f, pipeSpacing: 7f, gapHeight: 3.5f,
            gapCenterMin: 3f, gapCenterMax: 9f, firstPipeDistance: 14f);

        /// <summary>首根管道立即生成：无需长期存活即可观察种子对缺口中心的决定作用。</summary>
        private static FlappyRules ImmediatePipeRules() => new FlappyRules(
            fieldWidth: 20f, fieldHeight: 12f, gravity: 20f, flapSpeed: 6f, maxFallSpeed: 10f,
            birdX: 5f, birdHalfWidth: 0.25f, birdHalfHeight: 0.25f, hoverHeight: 6f,
            pipeSpeed: 3.5f, pipeWidth: 1f, pipeSpacing: 7f, gapHeight: 3.5f,
            gapCenterMin: 3f, gapCenterMax: 9f, firstPipeDistance: 0f);

        [Fact]
        public void 构造后初始状态为就绪悬停()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 42);

            Assert.Equal(FlappyPhase.Ready, game.Phase);
            Assert.Equal(new Vec2(5f, 6f), game.Position);
            Assert.Equal(default, game.Velocity);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.StepIndex);
            Assert.Empty(game.Pipes);
            Assert.False(game.IsFinished);
            Assert.Single(game.Events);
            Assert.Equal(FlappyEventType.Spawned, game.Events[0].Type);
        }

        [Fact]
        public void 就绪阶段推进不改变任何状态()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 42);
            for (int i = 0; i < 10; i++) Assert.True(game.Advance(0.25f, flap: false));

            Assert.Equal(FlappyPhase.Ready, game.Phase);
            Assert.Equal(new Vec2(5f, 6f), game.Position);
            Assert.Equal(0, game.StepIndex);
            Assert.Empty(game.Pipes);
            Assert.Single(game.Events);
        }

        [Fact]
        public void 首次点按起飞并置顶上升速度()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 42);

            Assert.True(game.Advance(0.25f, flap: true));

            Assert.Equal(FlappyPhase.Running, game.Phase);
            // 扇翅把速度置顶为 +6，同一步内重力再减 20*0.25=5，位移 1*0.25=0.25。
            Assert.Equal(1f, game.Velocity.Y);
            Assert.Equal(6.25f, game.Position.Y);
            Assert.Equal(1, game.StepIndex);
            Assert.Equal(FlappyEventType.Flapped, game.Events[^1].Type);
        }

        [Fact]
        public void 重力下落轨迹按整数步长手算吻合()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 42);
            Assert.True(game.Advance(0.25f, flap: true));

            // 无点按：每步速度 -5，位移 = 速度*0.25；第 4 步速度被下限夹到 -10。
            Assert.True(game.Advance(0.25f, flap: false));
            Assert.Equal(-4f, game.Velocity.Y);
            Assert.Equal(5.25f, game.Position.Y);

            Assert.True(game.Advance(0.25f, flap: false));
            Assert.Equal(-9f, game.Velocity.Y);
            Assert.Equal(3f, game.Position.Y);

            Assert.True(game.Advance(0.25f, flap: false));
            Assert.Equal(-10f, game.Velocity.Y);
            Assert.Equal(0.5f, game.Position.Y);

            // 第 5 步触地：本步返回 false，位置停在地面之上。
            Assert.False(game.Advance(0.25f, flap: false));
            Assert.True(game.IsFinished);
            Assert.Equal(FlappyEventType.HitGround, game.Events[^1].Type);
            Assert.Equal(0.25f, game.Position.Y);
        }

        [Fact]
        public void 连续点按抵达顶棚速度清零且存活()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 42);
            // 每步净上升 0.25：第 23 步恰好顶到 12-0.25 的顶棚。
            for (int i = 0; i < 23; i++) Assert.True(game.Advance(0.25f, flap: true));

            Assert.Equal(11.75f, game.Position.Y);
            Assert.Equal(0f, game.Velocity.Y);
            Assert.False(game.IsFinished);

            // 继续点按仍贴着顶棚悬停，不判死。
            Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(11.75f, game.Position.Y);
            Assert.Equal(0f, game.Velocity.Y);
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 管道按固定间距序列生成并随时间左移()
        {
            var game = new FlappyBirdGame(WideGapRules(), seed: 7);

            // 滚动 0.875/步：首根管道在累计 14 个单位（第 16 步）时出现在 X=20.5。
            for (int i = 0; i < 15; i++) Assert.True(game.Advance(0.25f, flap: true));
            Assert.Empty(game.Pipes);

            Assert.True(game.Advance(0.25f, flap: true));
            Assert.Single(game.Pipes);
            Assert.Equal(20.5f, game.Pipes[0].Center.X);

            // 第 24 步补第二根，间距恰为 7。
            for (int i = 0; i < 8; i++) Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(2, game.Pipes.Count);
            Assert.Equal(13.5f, game.Pipes[0].Center.X);
            Assert.Equal(20.5f, game.Pipes[1].Center.X);
            Assert.Equal(7f, game.Pipes[1].Center.X - game.Pipes[0].Center.X);

            // 第 39 步：三根管道等距左移，尚未越出左边界。
            for (int i = 0; i < 15; i++) Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(3, game.Pipes.Count);
            Assert.Equal(0.375f, game.Pipes[0].Center.X);
            Assert.Equal(7.375f, game.Pipes[1].Center.X);
            Assert.Equal(14.375f, game.Pipes[2].Center.X);
        }

        [Fact]
        public void 穿过管道缺口中心得分并记录事件()
        {
            var game = new FlappyBirdGame(WideGapRules(), seed: 3);
            for (int i = 0; i < 33; i++) Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(0, game.Score);

            // 第 34 步首根管道中心 X=4.75 越过小鸟横坐标 5，得 1 分。
            Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(1, game.Score);
            Assert.Equal(FlappyEventType.Scored, game.Events[^1].Type);
            Assert.Equal(1, game.Events.Count(e => e.Type == FlappyEventType.Scored));

            // 管道间距 7 对应 8 步，第 42 步第二根越线再得 1 分。
            for (int i = 0; i < 8; i++) Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(2, game.Score);
            Assert.Equal(2, game.Events.Count(e => e.Type == FlappyEventType.Scored));
        }

        [Fact]
        public void 撞上管道立即结束且不得分()
        {
            // 重力 8、步长 0.5 时交替扇翅让小鸟在悬停高度往复（每步升降各 1）；
            // 选一个首根管道缺口中心偏低的种子：小鸟必然撞在管道上沿，且越过分值线前死亡。
            FlappyBirdGame? game = null;
            for (int seed = 1; seed <= 100 && game is null; seed++)
            {
                var candidate = new FlappyBirdGame(SlowGravityRules(), seed);
                for (int i = 0; i < 8 && !candidate.IsFinished; i++) candidate.Advance(0.5f, flap: i % 2 == 0);
                if (candidate.IsFinished) continue;
                if (candidate.Pipes.Count > 0 && candidate.Pipes[0].Center.Y < 4.5f) game = candidate;
            }

            Assert.NotNull(game);
            for (int i = 0; i < 60 && !game!.IsFinished; i++) game.Advance(0.5f, flap: i % 2 == 0);

            Assert.True(game.IsFinished);
            Assert.Equal(FlappyEventType.HitPipe, game.Events[^1].Type);
            Assert.Equal(0, game.Score);
            Assert.True(game.Pipes[0].Center.Y < 4.5f);
        }

        [Fact]
        public void 相同种子两次模拟完全一致且不同种子缺口不同()
        {
            static int Simulate(int seed)
            {
                var game = new FlappyBirdGame(WideGapRules(), seed);
                for (int i = 0; i < 60; i++) game.Advance(0.25f, flap: true);
                return HashCode.Combine(game.Score, game.StepIndex, game.Position, string.Join(';', game.Pipes));
            }

            Assert.Equal(Simulate(123), Simulate(123));

            var first = new FlappyBirdGame(ImmediatePipeRules(), seed: 1);
            var second = new FlappyBirdGame(ImmediatePipeRules(), seed: 2);
            Assert.True(first.Advance(0.25f, flap: true));
            Assert.True(second.Advance(0.25f, flap: true));
            Assert.NotEqual(first.Pipes[0].Center.Y, second.Pipes[0].Center.Y);
        }

        [Fact]
        public void 事件日志封顶裁剪保留最近记录()
        {
            var game = new FlappyBirdGame(WideGapRules(), seed: 5);
            for (int i = 0; i < 200; i++) Assert.True(game.Advance(0.25f, flap: true));

            Assert.Equal(128, game.Events.Count);
            Assert.Equal(FlappyEventType.Flapped, game.Events[0].Type);
            Assert.Equal(FlappyEventType.Flapped, game.Events[^1].Type);
        }

        [Fact]
        public void 结束后推进无效且重置恢复初始状态()
        {
            var game = new FlappyBirdGame(FlappyRules.Default, seed: 9);
            Assert.True(game.Advance(0.25f, flap: true));
            for (int i = 0; i < 10 && !game.IsFinished; i++) game.Advance(0.25f, flap: false);
            Assert.True(game.IsFinished);
            int stepIndex = game.StepIndex;

            Assert.False(game.Advance(0.25f, flap: true));
            Assert.Equal(stepIndex, game.StepIndex);

            game.Reset(99);
            Assert.Equal(FlappyPhase.Ready, game.Phase);
            Assert.Equal(new Vec2(5f, 6f), game.Position);
            Assert.Equal(default, game.Velocity);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.StepIndex);
            Assert.Empty(game.Pipes);
            Assert.Single(game.Events);
            Assert.Equal(FlappyEventType.Reset, game.Events[0].Type);

            Assert.True(game.Advance(0.25f, flap: true));
            Assert.Equal(FlappyPhase.Running, game.Phase);
        }

        [Fact]
        public void 缺口中心由种子决定且始终落在规则范围内()
        {
            var centers = new List<float>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var game = new FlappyBirdGame(ImmediatePipeRules(), seed);
                Assert.True(game.Advance(0.25f, flap: true));
                Assert.Single(game.Pipes);
                Assert.Equal(20.5f, game.Pipes[0].Center.X);
                float center = game.Pipes[0].Center.Y;
                Assert.InRange(center, game.Rules.GapCenterMin, game.Rules.GapCenterMax);

                var again = new FlappyBirdGame(ImmediatePipeRules(), seed);
                again.Advance(0.25f, flap: true);
                Assert.Equal(center, again.Pipes[0].Center.Y);
                centers.Add(center);
            }

            Assert.True(centers.Distinct().Count() >= 2, "不同种子应能产生不同的缺口中心。");
        }
    }
}
