using System;
using System.Linq;
using Lyking.DoodleJumper.Core;
using Xunit;

namespace Lyking.DoodleJumper.Core.Tests
{
    /// <summary>涂鸦跳跃核心规则测试：确定性、弹跳抛物线、碰撞方向、水平移动与场界、易碎与移动平台、镜头跟随、坠落死亡、计分与事件封顶。</summary>
    public class DoodleJumperGameTests
    {
        /// <summary>物理子步长，与表现层驱动一致。</summary>
        private const float SubStep = 1f / 120f;

        /// <summary>坠落窗口放大到永不死亡：专用于考察水平移动与场界，不受坠落结束干扰。</summary>
        private static JumperRules NoDeathRules(JumperWallMode wallMode) => new JumperRules(
            fieldWidth: 16f, viewHeight: 12f, cameraAnchor: 1f / 3f,
            gravity: 20f, jumpSpeed: 10f, maxFallSpeed: 20f, fastFallMultiplier: 3f,
            playerHalfWidth: 0.4f, playerHalfHeight: 0.5f,
            maxHorizontalSpeed: 4f, horizontalAccel: 24f, wallMode: wallMode,
            platformWidth: 2.4f, platformThickness: 0.4f,
            platformGapMin: 1.2f, platformGapMax: 2f, gapGrowthPerMeter: 0.02f,
            minGapLimit: 1.6f, maxGapLimit: 2.2f,
            fragileChance: 0.25f, movingChance: 0.25f,
            movingAmplitude: 2f, movingAngularSpeed: MathF.PI / 2f, movingPhaseStep: MathF.PI / 2f,
            fallDeathWindow: 1000000f, lookAheadMargin: 2f, scorePerMeter: 10);

        /// <summary>窄场地且一半平台易碎：玩家不移动也必然与所有平台横向重叠，专用于考察踩碎失效。</summary>
        private static JumperRules NarrowRules() => new JumperRules(
            fieldWidth: 5f, viewHeight: 12f, cameraAnchor: 1f / 3f,
            gravity: 20f, jumpSpeed: 10f, maxFallSpeed: 20f, fastFallMultiplier: 3f,
            playerHalfWidth: 0.4f, playerHalfHeight: 0.5f,
            maxHorizontalSpeed: 4f, horizontalAccel: 24f, wallMode: JumperWallMode.Clamp,
            platformWidth: 2.4f, platformThickness: 0.4f,
            platformGapMin: 1.2f, platformGapMax: 2f, gapGrowthPerMeter: 0.02f,
            minGapLimit: 1.6f, maxGapLimit: 2.2f,
            fragileChance: 0.5f, movingChance: 0f,
            movingAmplitude: 2f, movingAngularSpeed: MathF.PI / 2f, movingPhaseStep: MathF.PI / 2f,
            fallDeathWindow: 8f, lookAheadMargin: 2f, scorePerMeter: 10);

        /// <summary>全部生成移动平台：专用于考察踩上移动平台与位置随时间的确定性。</summary>
        private static JumperRules AllMovingRules() => new JumperRules(
            fieldWidth: 16f, viewHeight: 12f, cameraAnchor: 1f / 3f,
            gravity: 20f, jumpSpeed: 10f, maxFallSpeed: 20f, fastFallMultiplier: 3f,
            playerHalfWidth: 0.4f, playerHalfHeight: 0.5f,
            maxHorizontalSpeed: 4f, horizontalAccel: 24f, wallMode: JumperWallMode.Clamp,
            platformWidth: 2.4f, platformThickness: 0.4f,
            platformGapMin: 1.2f, platformGapMax: 2f, gapGrowthPerMeter: 0.02f,
            minGapLimit: 1.6f, maxGapLimit: 2.2f,
            fragileChance: 0f, movingChance: 1f,
            movingAmplitude: 2f, movingAngularSpeed: MathF.PI / 2f, movingPhaseStep: MathF.PI / 2f,
            fallDeathWindow: 8f, lookAheadMargin: 2f, scorePerMeter: 10);

        [Fact]
        public void 构造后玩家立于起始平台并预生成上方平台()
        {
            var game = new JumperGame(JumperRules.Default, seed: 7);

            Assert.Equal(new Vec2(8f, 0.5f), game.PlayerPosition);
            Assert.Equal(JumperRules.Default.JumpSpeed, game.VelocityY);
            Assert.Equal(0f, game.VelocityX);
            Assert.Equal(0f, game.MaxHeight);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.StepIndex);
            Assert.Equal(0f, game.Time);
            // 镜头初始目标：玩家处于屏幕下缘起 1/3 高度处，即 0.5 + 12×(1/2−1/3) = 2.5。
            Assert.Equal(2.5f, game.CameraCenterY, 3);
            Assert.False(game.IsFinished);

            // 起始平台中心在高度负半个厚度处，顶面恰为高度 0；向上生成到镜头上缘加前视余量为止。
            var start = game.Platforms[0];
            Assert.Equal(0, start.Index);
            Assert.Equal(JumperPlatformType.Normal, start.Type);
            Assert.Equal(new Vec2(8f, -0.2f), start.Center);
            Assert.True(game.Platforms.Count >= 5);
            float ceiling = game.CameraTopY + JumperRules.Default.LookAheadMargin;
            Assert.True(game.Platforms[^1].Center.Y >= ceiling - 0.001f);
            Assert.True(game.Platforms[^2].Center.Y < ceiling);
            Assert.Single(game.Events);
            Assert.Equal(JumperEventType.Spawned, game.Events[0].Type);
        }

        [Fact]
        public void 弹跳抛物线按整数子步手算吻合()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 每步重力增量 20×0.25=5：跳后 vy=10-5=5，y=0.5+5×0.25=1.75，底部最高 1.25。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(1.75f, game.PlayerPosition.Y, 3);
            Assert.Equal(5f, game.VelocityY);
            Assert.Equal(1.25f, game.MaxHeight, 3);
            Assert.Equal(12, game.Score);
            Assert.Single(game.Events);

            // 第 2 步 vy=0 到达顶点，高度维持 1.75。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(1.75f, game.PlayerPosition.Y, 3);
            Assert.Equal(0f, game.VelocityY);

            // 第 3 步 vy=-5，底部落回 0 触发弹起：速度置顶为 10、底部贴回平台顶，分数不变。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(0.5f, game.PlayerPosition.Y, 3);
            Assert.Equal(10f, game.VelocityY);
            Assert.Equal(1.25f, game.MaxHeight, 3);
            Assert.Equal(12, game.Score);
            Assert.Equal(2, game.Events.Count);
            Assert.Equal(JumperEventType.Bounced, game.Events[^1].Type);
            Assert.Equal(0, game.Events[^1].PlatformIndex);
        }

        [Fact]
        public void 下落时才碰撞且上升直接穿透平台()
        {
            // 种子 14 的一号平台顶 1.2322 低于弹跳顶点 1.25 且与玩家横向上重叠。
            var game = new JumperGame(JumperRules.Default, seed: 14);
            var platform1 = game.Platforms.First(p => p.Index == 1);
            Assert.Equal(JumperPlatformType.Normal, platform1.Type);
            float top1 = platform1.Center.Y + JumperRules.Default.PlatformThickness * 0.5f;
            Assert.Equal(1.232f, top1, 3);

            // 前两步上升穿过平台顶所在平面：不产生任何碰撞事件。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Single(game.Events);
            Assert.Equal(1.25f, game.MaxHeight, 3);

            // 第 3 步下落底部穿过平台顶：弹起并把底部贴回平台顶。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(JumperEventType.Bounced, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].PlatformIndex);
            Assert.Equal(top1, game.PlayerPosition.Y - 0.5f, 3);
            Assert.Equal(10f, game.VelocityY);

            // 之后在平台上继续弹跳：顶点抬高到 1.2322+1.25=2.4822，分数换算为 24。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(2.482f, game.MaxHeight, 3);
            Assert.Equal(24, game.Score);
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(JumperEventType.Bounced, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].PlatformIndex);
        }

        [Fact]
        public void 水平移动在场界夹边停住()
        {
            var game = new JumperGame(NoDeathRules(JumperWallMode.Clamp), seed: 1);

            // 加速度 24×0.25=6 不小于最大速度 4：本步即达满速，每步位移 4×0.25=1。
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(4f, game.VelocityX, 3);
            Assert.Equal(9f, game.PlayerPosition.X, 3);
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(10f, game.PlayerPosition.X, 3);

            // 右缘为 16-0.4=15.6：8 步累计 8+8=16 被夹住，之后按住不动保持贴边。
            for (int i = 0; i < 6; i++) Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(15.6f, game.PlayerPosition.X, 3);
            for (int i = 0; i < 3; i++) Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(15.6f, game.PlayerPosition.X, 3);

            // 反向后先减速再左移：左缘 0.4 同样夹住。
            for (int i = 0; i < 20; i++) Assert.True(game.Advance(0.25f, new JumperInput(-1f, false)));
            Assert.Equal(0.4f, game.PlayerPosition.X, 3);
            Assert.Equal(-4f, game.VelocityX, 3);
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 短子步下水平加速度渐进提速()
        {
            var game = new JumperGame(NoDeathRules(JumperWallMode.Clamp), seed: 1);

            // 每步速度增量 24×0.1=2.4：第 1 步未达满速，第 2 步起封顶为 4。
            Assert.True(game.Advance(0.1f, new JumperInput(1f, false)));
            Assert.Equal(2.4f, game.VelocityX, 3);
            Assert.Equal(8.24f, game.PlayerPosition.X, 3);

            Assert.True(game.Advance(0.1f, new JumperInput(1f, false)));
            Assert.Equal(4f, game.VelocityX, 3);
            Assert.Equal(8.64f, game.PlayerPosition.X, 3);

            Assert.True(game.Advance(0.1f, new JumperInput(1f, false)));
            Assert.Equal(9.04f, game.PlayerPosition.X, 3);
        }

        [Fact]
        public void 手动指定环绕模式后跨过场缘回到另一侧()
        {
            var game = new JumperGame(NoDeathRules(JumperWallMode.Wrap), seed: 1);

            // 每步右移 1：第 8 步到达 16 越出右缘，环绕回 0。
            for (int i = 0; i < 7; i++) Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(15f, game.PlayerPosition.X, 3);
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(0f, game.PlayerPosition.X, 3);
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(1f, game.PlayerPosition.X, 3);

            // 反向越出左缘同样环绕：减速半步到 0.5，再整步到 -0.5 环绕为 15.5。
            Assert.True(game.Advance(0.25f, new JumperInput(-1f, false)));
            Assert.Equal(0.5f, game.PlayerPosition.X, 3);
            Assert.True(game.Advance(0.25f, new JumperInput(-1f, false)));
            Assert.Equal(15.5f, game.PlayerPosition.X, 3);
        }

        [Fact]
        public void 易碎平台踩上即碎不弹起()
        {
            var game = new JumperGame(NarrowRules(), seed: 1);
            Assert.Equal(JumperPlatformType.Fragile, game.Platforms.First(p => p.Index == 1).Type);

            // 窄场内玩家与所有平台横向重叠：首个上升顶点后下落踩上一号平台即碎。
            var tops = new Dictionary<int, float>();
            for (int i = 1; i <= 600; i++)
            {
                foreach (var platform in game.Platforms)
                    tops[platform.Index] = platform.Center.Y + NarrowRules().PlatformThickness * 0.5f;
                game.Advance(SubStep, JumperInput.None);
                if (game.Events[^1].Type == JumperEventType.FragileBroken) break;
            }

            Assert.Equal(JumperEventType.FragileBroken, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].PlatformIndex);
            float topBefore = tops[1];
            Assert.Equal(1.399f, topBefore, 3);
            // 不弹起：竖直速度仍向下，底部已落到平台顶之下，平台从列表中消失。
            Assert.Equal(-6.5f, game.VelocityY, 1);
            Assert.True(game.PlayerPosition.Y - 0.5f <= topBefore + 0.001f);
            Assert.DoesNotContain(game.Platforms, p => p.Index == 1);

            // 继续下落会落至更低的普通平台弹起，棋局不结束。
            bool caught = false;
            for (int i = 0; i < 600 && !caught; i++)
            {
                game.Advance(SubStep, JumperInput.None);
                caught = game.Events[^1].Type == JumperEventType.Bounced;
            }

            Assert.True(caught);
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 移动平台往返相位由生成序号确定()
        {
            var rules = JumperRules.Default;

            // 0 号相位为 0：X 在基准两侧以振幅 2、周期 4 秒往返。
            Assert.Equal(8f, rules.MovingPlatformX(8f, 0, 0f), 3);
            Assert.Equal(10f, rules.MovingPlatformX(8f, 0, 1f), 3);
            Assert.Equal(8f, rules.MovingPlatformX(8f, 0, 2f), 3);
            Assert.Equal(6f, rules.MovingPlatformX(8f, 0, 3f), 3);
            Assert.Equal(8f, rules.MovingPlatformX(8f, 0, 4f), 3);

            // 1 号相位前移 π/2：初始即处于右端 BaseX+2。
            Assert.Equal(10f, rules.MovingPlatformX(8f, 1, 0f), 3);
            Assert.Equal(8f, rules.MovingPlatformX(8f, 1, 1f), 3);
        }

        [Fact]
        public void 踩到移动平台弹起且平台位置随时间确定()
        {
            var rules = AllMovingRules();
            var game = new JumperGame(rules, seed: 1);

            // 全移动规则下 1 号平台相位 π/2：生成时位于基准横坐标加振幅的右端点。
            var platform1 = game.Platforms.First(p => p.Index == 1);
            Assert.Equal(JumperPlatformType.Moving, platform1.Type);
            Assert.Equal(platform1.BaseX + rules.MovingAmplitude, platform1.Center.X, 3);

            // 玩家原地弹跳，1 号平台在首个下落段横移进入玩家范围被踩上。
            for (int i = 1; i <= 360; i++)
            {
                game.Advance(SubStep, JumperInput.None);
                if (game.Events[^1].Type == JumperEventType.BouncedOnMoving) break;
            }

            Assert.Equal(JumperEventType.BouncedOnMoving, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].PlatformIndex);
            Assert.False(game.IsFinished);

            // 弹起后速度置顶、底部贴回平台顶，且平台横坐标与确定性纯函数逐位一致。
            platform1 = game.Platforms.First(p => p.Index == 1);
            Assert.Equal(rules.JumpSpeed, game.VelocityY, 3);
            Assert.Equal(platform1.Center.Y + rules.PlatformThickness * 0.5f, game.PlayerPosition.Y - 0.5f, 3);
            Assert.Equal(rules.MovingPlatformX(platform1.BaseX, 1, game.Time), platform1.Center.X, 3);
            Assert.True(game.MaxHeight > 2.4f);
        }

        [Fact]
        public void 平台生成固定种子手算间距高度与类型()
        {
            var rules = JumperRules.Default;
            var game = new JumperGame(rules, seed: 7);

            // 种子 7 的随机序列依次为间距 0.383220、种类 0.871256、横坐标 0.660939……
            // 一号平台：间距 1.2+0.383220×0.8=1.506576，普通（0.871256≥0.5），
            // X=1.2+0.660939×13.6=10.188771，Y=-0.2+1.506576=1.306576。
            var p1 = game.Platforms[1];
            Assert.Equal(JumperPlatformType.Normal, p1.Type);
            Assert.Equal(10.189f, p1.Center.X, 3);
            Assert.Equal(1.307f, p1.Center.Y, 3);

            // 二号平台：间距 1.226131+0.052262×0.8=1.267941（以当前高度微增下限），
            // 移动（0.366433 落在易碎与移动区间），X=3.2+0.676276×9.6=9.691227。
            var p2 = game.Platforms[2];
            Assert.Equal(JumperPlatformType.Moving, p2.Type);
            Assert.Equal(9.691f, p2.Center.X, 3);
            Assert.Equal(2.575f, p2.Center.Y, 3);

            // 三号平台为普通（种类随机值不小于 0.5），间距与高度逐级递增。
            var p3 = game.Platforms[3];
            Assert.Equal(JumperPlatformType.Normal, p3.Type);
            Assert.Equal(3.860f, p3.Center.Y, 3);

            // 垂直方向严格递增，相邻间距始终落在当前高度的间距范围内。
            for (int i = 1; i < game.Platforms.Count; i++)
            {
                float previous = game.Platforms[i - 1].Center.Y;
                float gap = game.Platforms[i].Center.Y - previous;
                Assert.True(gap >= rules.MinGapFor(previous) - 0.001f);
                Assert.True(gap <= rules.MaxGapFor(previous) + 0.001f);
            }
        }

        [Fact]
        public void 镜头只升不降且玩家保持屏幕下三分之一附近()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 上升时镜头跟随目标：中心 = 玩家高度 + 12×(1/2−1/3) = 玩家高度 + 2。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(game.PlayerPosition.Y + 2f, game.CameraCenterY, 3);
            Assert.Equal(3.75f, game.CameraCenterY, 3);

            // 玩家落回起始平台后目标降为 2.5，镜头保持 3.75 不回退。
            float cameraAfterRise = game.CameraCenterY;
            for (int i = 0; i < 2; i++) Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(0.5f, game.PlayerPosition.Y, 3);
            Assert.Equal(cameraAfterRise, game.CameraCenterY, 3);

            // 长时间原地弹跳镜头单调不降。
            float camera = game.CameraCenterY;
            for (int i = 0; i < 12; i++)
            {
                Assert.True(game.Advance(0.25f, JumperInput.None));
                Assert.True(game.CameraCenterY >= camera - 0.0001f);
                camera = game.CameraCenterY;
            }
        }

        [Fact]
        public void 快落放大重力令每步落回弹起()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 重力放大 3 倍：每步 vy=10-20×0.25×3=-5，底部立即穿回平台顶弹起，无法升高。
            for (int i = 0; i < 10; i++)
            {
                Assert.True(game.Advance(0.25f, new JumperInput(0f, true)));
                Assert.Equal(0.5f, game.PlayerPosition.Y, 3);
                Assert.Equal(JumperEventType.Bounced, game.Events[^1].Type);
                Assert.Equal(0, game.Events[^1].PlatformIndex);
            }

            Assert.Equal(10, game.Events.Count - 1);
            Assert.Equal(0f, game.MaxHeight, 3);
            Assert.Equal(0, game.Score);
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 坠落出窗口判定死亡且结束后推进无效()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 前两步向右走出起始平台（右缘 9.2，玩家左缘两步后达 9.6），期间弹跳顶点抬高判定线。
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.True(game.Advance(0.25f, new JumperInput(1f, false)));
            Assert.Equal(1.25f, game.MaxHeight, 3);

            // 自由落体：底部 0→-1.25→-3.75→-7.5→-12.5（vy 触及上限 -20），
            // 第 6 步底部 -12.5 低于判定线 1.25-8=-6.75，坠落结束且本步返回 false。
            for (int i = 3; i <= 5; i++) Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.False(game.IsFinished);
            Assert.False(game.Advance(0.25f, JumperInput.None));

            Assert.True(game.IsGameOver);
            Assert.Equal(6, game.StepIndex);
            Assert.Equal(-10.75f, game.PlayerPosition.Y, 3);
            Assert.Equal(-20f, game.VelocityY, 3);
            Assert.Equal(10f, game.PlayerPosition.X, 3);
            Assert.Equal(3.75f, game.CameraCenterY, 3);
            Assert.Equal(2, game.Events.Count);
            Assert.Equal(JumperEventType.FellOut, game.Events[^1].Type);

            // 结束后推进无效：状态完整冻结。
            Assert.False(game.Advance(0.25f, new JumperInput(1f, true)));
            Assert.Equal(6, game.StepIndex);
            Assert.Equal(-10.75f, game.PlayerPosition.Y, 3);
        }

        [Fact]
        public void 平台间距随高度微增且不超过可跳上限()
        {
            var rules = JumperRules.Default;

            // 间距范围随高度线性微增：0 米时 1.2~2.0，10 米时 1.4~2.2，之后被上限夹住。
            Assert.Equal(1.2f, rules.MinGapFor(0f), 3);
            Assert.Equal(2f, rules.MaxGapFor(0f), 3);
            Assert.Equal(1.4f, rules.MinGapFor(10f), 3);
            Assert.Equal(2.2f, rules.MaxGapFor(10f), 3);
            Assert.Equal(1.6f, rules.MinGapFor(1000f), 3);
            Assert.Equal(2.2f, rules.MaxGapFor(1000f), 3);

            // 高度无上限而间距有界：最大间距加半个厚度不超过理论弹跳高度 10²/(2×20)=2.5，平台始终可跳上。
            Assert.Equal(2.5f, rules.MaxJumpHeight, 3);
            Assert.True(rules.MaxGapFor(float.MaxValue) + rules.PlatformThickness * 0.5f <= rules.MaxJumpHeight + 0.0001f);
        }

        [Fact]
        public void 相同种子模拟逐位一致且不同种子平台不同()
        {
            static long Simulate(int seed)
            {
                var game = new JumperGame(JumperRules.Default, seed);
                for (int i = 1; i <= 30; i++)
                    game.Advance(0.25f, new JumperInput(i % 4 < 2 ? 1f : -1f, false));
                return HashCode.Combine(game.Score, game.StepIndex, game.MaxHeight, game.PlayerPosition,
                    game.IsGameOver, string.Join(';', game.Platforms));
            }

            Assert.Equal(Simulate(7), Simulate(7));

            // 种子 7 的一号平台为普通，种子 11 的一号平台为易碎且横坐标不同。
            var first = new JumperGame(JumperRules.Default, seed: 7);
            var second = new JumperGame(JumperRules.Default, seed: 11);
            Assert.Equal(JumperPlatformType.Normal, first.Platforms[1].Type);
            Assert.Equal(JumperPlatformType.Fragile, second.Platforms[1].Type);
            Assert.NotEqual(first.Platforms[1].Center.X, second.Platforms[1].Center.X);
        }

        [Fact]
        public void 事件日志封顶裁剪保留最近记录()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);

            // 原地弹跳每 3 步一次弹起：400 步产生 Spawned 加 133 次弹起共 134 条，封顶保留最近 128 条。
            for (int i = 0; i < 400; i++) Assert.True(game.Advance(0.25f, JumperInput.None));

            Assert.Equal(128, game.Events.Count);
            Assert.Equal(JumperEventType.Bounced, game.Events[0].Type);
            Assert.Equal(18, game.Events[0].Step);
            Assert.Equal(JumperEventType.Bounced, game.Events[^1].Type);
            Assert.Equal(399, game.Events[^1].Step);
        }

        [Fact]
        public void 重置后状态复位等价于重建()
        {
            var game = new JumperGame(JumperRules.Default, seed: 1);
            game.Advance(0.25f, new JumperInput(1f, false));
            game.Advance(0.25f, new JumperInput(1f, false));
            for (int i = 3; i <= 20 && !game.IsFinished; i++) game.Advance(0.25f, JumperInput.None);
            Assert.True(game.IsFinished);

            game.Reset(9);
            var fresh = new JumperGame(JumperRules.Default, seed: 9);
            Assert.Equal(fresh.PlayerPosition, game.PlayerPosition);
            Assert.Equal(fresh.VelocityY, game.VelocityY);
            Assert.Equal(fresh.VelocityX, game.VelocityX);
            Assert.Equal(fresh.CameraCenterY, game.CameraCenterY, 3);
            Assert.Equal(fresh.MaxHeight, game.MaxHeight, 3);
            Assert.Equal(fresh.Score, game.Score);
            Assert.Equal(fresh.StepIndex, game.StepIndex);
            Assert.Equal(fresh.Time, game.Time, 3);
            Assert.Equal(fresh.Platforms.Count, game.Platforms.Count);
            Assert.True(game.Platforms.Zip(fresh.Platforms).All(pair => pair.First.ToString() == pair.Second.ToString()));
            Assert.False(game.IsFinished);
            Assert.Single(game.Events);
            Assert.Equal(JumperEventType.Reset, game.Events[0].Type);

            // 重置后可正常推进：首个上升段抬升高度并按换算计分。
            Assert.True(game.Advance(0.25f, JumperInput.None));
            Assert.Equal(1.25f, game.MaxHeight, 3);
            Assert.Equal(12, game.Score);
        }
    }
}
