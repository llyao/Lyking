using System;
using System.Linq;
using Lyking.Frogger.Core;
using Xunit;

namespace Lyking.Frogger.Core.Tests
{
    /// <summary>青蛙过河核心规则测试：跳跃边界、车道种子手算、撞车、溺水、骑乘、进位、过关、生命与确定性。</summary>
    public class FroggerBoardTests
    {
        /// <summary>构造定制规则：可覆盖每条车道/河道的实体配置，便于构造确定性场景。</summary>
        private static FroggerRules CustomRules(
            int maxLives = 3,
            int[]? carCounts = null,
            float[]? carSpeeds = null,
            float[]? carLengths = null,
            int[]? logCounts = null,
            float[]? logSpeeds = null,
            float[]? logLengths = null) => new FroggerRules(
                width: 13,
                maxLives: maxLives,
                goalCount: 5,
                scorePerGoal: 200,
                roundSpeedGain: 0.15f,
                frogHalfWidth: 0.35f,
                carCounts: carCounts,
                carSpeeds: carSpeeds,
                carLengths: carLengths,
                logCounts: logCounts,
                logSpeeds: logSpeeds,
                logLengths: logLengths);

        /// <summary>通关测试规则：马路无车、河道木排满铺（相邻间隙小于蛙宽，任何整列落点都在木排上）且慢速滚动。</summary>
        private static FroggerRules CrossRules() => CustomRules(
            carCounts: new[] { 0, 0, 0, 0 },
            logCounts: new[] { 6, 6, 6, 6 },
            logSpeeds: new[] { 0.5f, 0.5f, 0.5f, 0.5f },
            logLengths: new[] { 2f, 2f, 2f, 2f });

        /// <summary>静止木排规则：河道各一条不动的短木排，便于精确控制落水与骑乘场景。</summary>
        private static FroggerRules StaticLogRules(
            int maxLives = 3,
            float logSpeed = 0f,
            float logLength = 2f) => CustomRules(
                maxLives: maxLives,
                carCounts: new[] { 0, 0, 0, 0 },
                logCounts: new[] { 1, 1, 1, 1 },
                logSpeeds: new[] { logSpeed, 0f, 0f, 0f },
                logLengths: new[] { logLength, logLength, logLength, logLength });

        /// <summary>按核心构造顺序复算种子生成的方向位与相位：1 个方向位 + 每条车道各 1 个相位。</summary>
        private static (bool Flip, float[] Phases) DeriveLaneSetup(int seed)
        {
            var random = new Random(seed);
            bool flip = random.Next(2) == 1;
            var phases = new float[8];
            for (int i = 0; i < 8; i++) phases[i] = (float)random.NextDouble() * 13f;
            return (flip, phases);
        }

        /// <summary>复算第 <paramref name="lane"/> 条车道的方向：与核心同为逐行交替、种子决定整体起点。</summary>
        private static int DirectionFor(int lane, bool flip) => ((lane % 2 == 0) != flip) ? 1 : -1;

        /// <summary>场宽 13 的轨道环绕距离。</summary>
        private static float CircDist(float left, float right)
        {
            float direct = MathF.Abs(left - right);
            return MathF.Min(direct, 13f - direct);
        }

        private static float Wrap13(float x)
        {
            float wrapped = x - MathF.Floor(x / 13f) * 13f;
            return wrapped >= 13f ? 0f : wrapped;
        }

        /// <summary>沿行进方向从 <paramref name="from"/> 到 <paramref name="to"/> 的环绕距离。</summary>
        private static float TravelDistance(float from, float to, int direction)
        {
            float raw = direction > 0 ? to - from : from - to;
            return Wrap13(raw);
        }

        private static int FindCarHitSeed()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var (flip, phases) = DeriveLaneSetup(seed);
                float distance = TravelDistance(phases[0], 6f, DirectionFor(0, flip));
                if (distance > 1.2f && distance < 4.5f) return seed;
            }

            throw new InvalidOperationException("未找到可用于撞车测试的种子。");
        }

        private static int FindDrownSeed()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var (_, phases) = DeriveLaneSetup(seed);
                if (CircDist(phases[4], 6f) > 1.4f) return seed;
            }

            throw new InvalidOperationException("未找到可用于溺水测试的种子。");
        }

        private static int FindRideSeed()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var (flip, phases) = DeriveLaneSetup(seed);
                if (!flip && CircDist(phases[4], 6f) < 1.4f) return seed;
            }

            throw new InvalidOperationException("未找到可用于骑乘测试的种子。");
        }

        private static void ClimbToRiverTop(FroggerBoard board)
        {
            for (int row = 0; row < 11; row++) board.Step(0.02f, FroggerDir.Up);
            Assert.Equal(11, board.FrogRow);
        }

        private static void ClimbToGoal(FroggerBoard board)
        {
            for (int row = 0; row < 12; row++) board.Step(0.02f, FroggerDir.Up);
        }

        private static void RideToColumn(FroggerBoard board, int targetColumn)
        {
            for (int i = 0; i < 400 && board.FrogColumn != targetColumn; i++) board.Step(0.05f, null);
            Assert.Equal(targetColumn, board.FrogColumn);
        }

        private static void WalkToColumn(FroggerBoard board, int targetColumn)
        {
            for (int i = 0; i < 200 && board.FrogColumn != targetColumn; i++)
            {
                board.Step(0.02f, board.FrogColumn < targetColumn ? FroggerDir.Right : FroggerDir.Left);
            }

            Assert.Equal(targetColumn, board.FrogColumn);
        }

        private static System.Collections.Generic.List<FroggerEventType> Types(FroggerBoard board) =>
            board.Events.Select(e => e.Type).ToList();

        [Fact]
        public void 构造后初始状态合法()
        {
            var board = new FroggerBoard(FroggerRules.Default, seed: 42);

            Assert.Equal(3, board.Lives);
            Assert.Equal(1, board.Round);
            Assert.Equal(0, board.Score);
            Assert.Equal(0, board.GoalsFilled);
            Assert.Equal(0f, board.Elapsed, 4);
            Assert.False(board.IsFinished);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(FroggerRules.Default.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);
            Assert.Equal(FroggerEventType.Spawned, board.Events[0].Type);
            Assert.Equal(8, board.Lanes.Count);

            // 马路 4 条在前、河道 4 条在后，行号自下而上，方向逐行交替
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(FroggerRules.StartRowCount + i, board.Lanes[i].Row);
                Assert.Equal(FroggerRules.Default.RiverStartRow + i, board.Lanes[4 + i].Row);
                Assert.Equal(FroggerLaneKind.Road, board.Lanes[i].Kind);
                Assert.Equal(FroggerLaneKind.River, board.Lanes[4 + i].Kind);
                if (i > 0) Assert.Equal(-board.Lanes[i - 1].Direction, board.Lanes[i].Direction);
            }

            Assert.Equal(-board.Lanes[3].Direction, board.Lanes[4].Direction);
        }

        [Fact]
        public void 越界跳跃被拒绝并保持原格()
        {
            var board = new FroggerBoard(FroggerRules.Default, seed: 7);

            // 底部边界：起点行再向下跳被拒绝
            board.Step(0f, FroggerDir.Down);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(FroggerEventType.HopRejected, board.Events[^1].Type);

            // 左右边界：走到第 0 列与第 12 列后再向外跳被拒绝
            for (int i = 0; i < 6; i++) board.Step(0f, FroggerDir.Left);
            Assert.Equal(0, board.FrogColumn);
            for (int i = 0; i < 12; i++) board.Step(0f, FroggerDir.Right);
            Assert.Equal(12, board.FrogColumn);
            board.Step(0f, FroggerDir.Right);
            Assert.Equal(12, board.FrogColumn);
            Assert.Equal(FroggerEventType.HopRejected, board.Events[^1].Type);

            Assert.Equal(18, board.Events.Count(e => e.Type == FroggerEventType.Hopped));
            Assert.Equal(2, board.Events.Count(e => e.Type == FroggerEventType.HopRejected));
        }

        [Fact]
        public void 对岸非荷花位跳跃被拒绝()
        {
            var board = new FroggerBoard(CrossRules(), seed: 5);
            ClimbToRiverTop(board);

            // 沿顶层河道随排漂到相邻的非荷花列（7 或 5），再向上跳应被原地拒绝
            var topLane = board.Lanes[7];
            RideToColumn(board, topLane.Direction > 0 ? 7 : 5);
            board.Step(0f, FroggerDir.Up);

            Assert.Equal(FroggerEventType.HopRejected, board.Events[^1].Type);
            Assert.Equal(11, board.FrogRow);
            Assert.True(board.IsRidingLog);
            Assert.Equal(0, board.Score);
        }

        [Fact]
        public void 车辆按固定种子出生并按固定速度循环移动()
        {
            var rules = CustomRules(
                carCounts: new[] { 2, 0, 0, 0 },
                carSpeeds: new[] { 2f, 0f, 0f, 0f },
                carLengths: new[] { 1f, 1f, 1f, 1f },
                logCounts: new[] { 1, 1, 1, 1 },
                logSpeeds: new[] { 0f, 0f, 0f, 0f },
                logLengths: new[] { 2f, 2f, 2f, 2f });
            var board = new FroggerBoard(rules, seed: 9);
            var (flip, phases) = DeriveLaneSetup(9);

            // 手算：车道 0 两辆车等距 6.5 格排布，首车相位即种子相位；空车道无实体
            Assert.Equal(Wrap13(phases[0]), board.Lanes[0].Positions[0], 3);
            Assert.Equal(Wrap13(phases[0] + 6.5f), board.Lanes[0].Positions[1], 3);
            Assert.Equal(DirectionFor(0, flip), board.Lanes[0].Direction);
            Assert.Equal(0, board.Lanes[1].EntityCount);
            Assert.Equal(Wrap13(phases[4]), board.Lanes[4].Positions[0], 3);

            float before0 = board.Lanes[0].Positions[0];
            float before1 = board.Lanes[0].Positions[1];
            board.Step(1f, null);

            // 手算：1 秒内每辆车沿方向移动 2 格并环绕
            Assert.True(MathF.Abs(board.Lanes[0].TrackDistance(before0, board.Lanes[0].Positions[0]) - 2f) < 0.05f);
            Assert.True(MathF.Abs(board.Lanes[0].TrackDistance(before1, board.Lanes[0].Positions[1]) - 2f) < 0.05f);

            // 静止河道保持原位
            Assert.Equal(Wrap13(phases[4]), board.Lanes[4].Positions[0], 3);
        }

        [Fact]
        public void 青蛙在马路上被车撞失命回起点()
        {
            var rules = CustomRules(
                carCounts: new[] { 1, 0, 0, 0 },
                carSpeeds: new[] { 1f, 0f, 0f, 0f },
                carLengths: new[] { 1f, 1f, 1f, 1f },
                logCounts: new[] { 1, 1, 1, 1 },
                logSpeeds: new[] { 0f, 0f, 0f, 0f },
                logLengths: new[] { 2f, 2f, 2f, 2f });
            int seed = FindCarHitSeed();
            var board = new FroggerBoard(rules, seed);
            var (flip, phases) = DeriveLaneSetup(seed);
            int direction = DirectionFor(0, flip);

            // 手算：相撞范围 = 车半长 0.5 + 青蛙半宽 0.35 = 0.85 格，车沿行进方向逼近第 6 列
            float distance = TravelDistance(phases[0], 6f, direction);
            float safeTime = (distance - 0.85f) / 1f - 0.05f;

            board.Step(0f, FroggerDir.Up);
            Assert.Equal(1, board.FrogRow);
            Assert.False(board.IsFinished);

            board.Step(MathF.Max(0f, safeTime), null);
            Assert.False(board.IsFinished);
            board.Step(0.3f, null);

            Assert.False(board.IsFinished);
            Assert.Equal(2, board.Lives);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(rules.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);
            Assert.Contains(FroggerEventType.HitByCar, Types(board));
            Assert.Contains(FroggerEventType.LifeLost, Types(board));
        }

        [Fact]
        public void 河道落水即溺水失命()
        {
            var rules = StaticLogRules();
            int seed = FindDrownSeed();
            var board = new FroggerBoard(rules, seed);
            var (_, phases) = DeriveLaneSetup(seed);

            // 手算：河道第一行唯一的木排与起点列 6 的环绕距离超过相扣范围 1.0 + 0.35 = 1.35 格
            Assert.True(CircDist(phases[4], 6f) > 1.4f);

            for (int row = 0; row < 8; row++) board.Step(0f, FroggerDir.Up);

            Assert.Equal(2, board.Lives);
            Assert.False(board.IsFinished);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(rules.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);
            Assert.Contains(FroggerEventType.Drowned, Types(board));
            Assert.Contains(FroggerEventType.LifeLost, Types(board));
            Assert.DoesNotContain(FroggerEventType.GameOver, Types(board));
        }

        [Fact]
        public void 落上木排随木排平移()
        {
            var rules = StaticLogRules(logSpeed: 1f, logLength: 3f);
            int seed = FindRideSeed();
            var board = new FroggerBoard(rules, seed);
            var (_, phases) = DeriveLaneSetup(seed);

            // 手算：跳上时木排（半长 1.5 + 半宽 0.35）与列 6 相交，且该行向右滚动
            Assert.True(CircDist(phases[4], 6f) < 1.4f);
            Assert.Equal(1, board.Lanes[4].Direction);

            for (int row = 0; row < 8; row++) board.Step(0f, FroggerDir.Up);

            Assert.Equal(8, board.FrogRow);
            Assert.True(board.IsRidingLog);
            Assert.Contains(FroggerEventType.RodeLog, Types(board));

            board.Step(0.5f, null);

            // 手算：0.5 秒内青蛙随排平移约 1 格（120Hz 子步取整误差以内）
            Assert.True(MathF.Abs(board.FrogX - 6.5f) < 0.05f, $"实际横坐标 {board.FrogX}");
            Assert.Equal(8, board.FrogRow);
            Assert.True(board.IsRidingLog);
            // 木排自身同步前进，青蛙仍在其上
            Assert.True(board.Lanes[4].TrackDistance(board.Lanes[4].Positions[0], board.FrogX) < 1.85f);
        }

        [Fact]
        public void 被木排带出场界失命()
        {
            var rules = StaticLogRules(logSpeed: 1f, logLength: 3f);
            var board = new FroggerBoard(rules, FindRideSeed());

            for (int row = 0; row < 8; row++) board.Step(0f, FroggerDir.Up);
            Assert.True(board.IsRidingLog);

            // 以 1 格/秒向右漂移，约 6.6 秒后被带出右边界
            for (int i = 0; i < 100 && !board.IsFinished; i++) board.Step(0.5f, null);

            Assert.False(board.IsGameOver);
            Assert.Equal(2, board.Lives);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(rules.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);
            Assert.Contains(FroggerEventType.CarriedOut, Types(board));
            Assert.Contains(FroggerEventType.LifeLost, Types(board));
            Assert.DoesNotContain(FroggerEventType.GameOver, Types(board));
        }

        [Fact]
        public void 到达荷花位进位计分并回起点()
        {
            var board = new FroggerBoard(CrossRules(), seed: 5);
            ClimbToGoal(board);

            Assert.Equal(200, board.Score);
            Assert.Equal(1, board.GoalsFilled);
            Assert.True(board.IsGoalFilled(2));
            Assert.False(board.IsGoalFilled(0));
            Assert.Equal(3, board.Lives);
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(FroggerRules.Default.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);

            // 进位事件带荷花位次：列 6 是第 2 号荷花位
            var reached = Assert.Single(board.Events.Where(e => e.Type == FroggerEventType.GoalReached).ToList());
            Assert.Equal(2, reached.Index);
        }

        [Fact]
        public void 已占用荷花位再次到达被拒绝()
        {
            var board = new FroggerBoard(CrossRules(), seed: 5);
            ClimbToGoal(board);
            ClimbToGoal(board);

            Assert.Equal(1, board.GoalsFilled);
            Assert.Equal(200, board.Score);
            Assert.Equal(11, board.FrogRow);
            Assert.True(board.IsRidingLog);
            Assert.Equal(FroggerEventType.HopRejected, board.Events[^1].Type);
        }

        [Fact]
        public void 五格进度全满过关进入下一轮提速()
        {
            var board = new FroggerBoard(CrossRules(), seed: 11);
            var spots = new[] { 1, 4, 6, 8, 11 };
            for (int k = 0; k < spots.Length; k++)
            {
                ClimbToRiverTop(board);
                WalkToColumn(board, spots[k]);
                board.Step(0f, FroggerDir.Up);
                if (k < spots.Length - 1) Assert.Equal(k + 1, board.GoalsFilled);
            }

            Assert.Equal(2, board.Round);
            Assert.Equal(0, board.GoalsFilled);
            Assert.Equal(1000, board.Score);
            for (int i = 0; i < 5; i++) Assert.False(board.IsGoalFilled(i));

            // 过关后全体提速 15%：基础 0.5 格/秒的河道在本轮移动 0.575 格
            var lane = board.Lanes[4];
            float before = lane.Positions[0];
            board.Step(1f, null);
            Assert.True(MathF.Abs(lane.TrackDistance(before, lane.Positions[0]) - 0.575f) < 0.05f,
                $"本轮实际位移 {lane.TrackDistance(before, lane.Positions[0])}");
        }

        [Fact]
        public void 生命用尽游戏结束且推进冻结()
        {
            var rules = StaticLogRules(maxLives: 1);
            var board = new FroggerBoard(rules, FindDrownSeed());

            for (int row = 0; row < 8; row++) board.Step(0f, FroggerDir.Up);

            Assert.True(board.IsGameOver);
            Assert.True(board.IsFinished);
            Assert.Equal(0, board.Lives);
            Assert.Equal(FroggerEventType.GameOver, board.Events[^1].Type);

            float elapsed = board.Elapsed;
            int eventCount = board.Events.Count;
            int frogRow = board.FrogRow;
            board.Step(1f, FroggerDir.Up);

            Assert.Equal(elapsed, board.Elapsed, 4);
            Assert.Equal(eventCount, board.Events.Count);
            Assert.Equal(frogRow, board.FrogRow);
        }

        [Fact]
        public void 失命后青蛙复位回起点且棋局继续()
        {
            var rules = StaticLogRules();
            var board = new FroggerBoard(rules, FindDrownSeed());

            for (int row = 0; row < 8; row++) board.Step(0f, FroggerDir.Up);
            Assert.Equal(2, board.Lives);

            // 重置复位：回到起点整格、脱离木排，此后仍可继续跳跃
            Assert.Equal(0, board.FrogRow);
            Assert.Equal(rules.StartColumn, board.FrogColumn);
            Assert.False(board.IsRidingLog);

            board.Step(0f, FroggerDir.Up);
            Assert.Equal(1, board.FrogRow);
            Assert.Equal(FroggerEventType.Hopped, board.Events[^1].Type);
        }

        [Fact]
        public void 相同种子与输入序列完全确定()
        {
            static string Simulate(int seed)
            {
                var board = new FroggerBoard(CrossRules(), seed);
                var dirs = new[] { FroggerDir.Up, FroggerDir.Left, FroggerDir.Up, FroggerDir.Right };
                for (int i = 0; i < 60; i++)
                {
                    FroggerDir? hop = i % 7 == 0 ? dirs[i / 7 % dirs.Length] : null;
                    board.Step(0.05f, hop);
                }

                return string.Join(';', board.Score, board.Lives, board.Round, board.GoalsFilled,
                    board.Elapsed.ToString("R"), board.FrogRow, board.FrogX.ToString("R"), board.IsRidingLog,
                    string.Join("|", board.Lanes[0].Positions), string.Join("|", board.Lanes[7].Positions),
                    board.Events.Count, board.Events[^1].Type);
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        [Fact]
        public void 速度倍率与场地结构为确定性纯函数()
        {
            Assert.Equal(1f, FroggerRules.Default.SpeedFactorFor(1), 4);
            Assert.Equal(1.15f, FroggerRules.Default.SpeedFactorFor(2), 4);
            Assert.Equal(1.6f, FroggerRules.Default.SpeedFactorFor(5), 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => FroggerRules.Default.SpeedFactorFor(0));

            Assert.Equal(13, FroggerRules.Default.Height);
            Assert.Equal(8, FroggerRules.Default.RiverStartRow);
            Assert.Equal(6, FroggerRules.Default.StartColumn);
            Assert.Equal(new[] { 1, 4, 6, 8, 11 }, FroggerRules.Default.GoalColumns.ToArray());
            Assert.Equal(FroggerRowType.Road, FroggerRules.Default.RowTypeFor(2));
            Assert.Equal(FroggerRowType.Grass, FroggerRules.Default.RowTypeFor(6));
            Assert.Equal(FroggerRowType.River, FroggerRules.Default.RowTypeFor(9));
            Assert.Equal(FroggerRowType.Goal, FroggerRules.Default.RowTypeFor(12));
            Assert.Throws<ArgumentOutOfRangeException>(() => FroggerRules.Default.RowTypeFor(13));
        }

        [Fact]
        public void 事件日志封顶裁剪()
        {
            var rules = StaticLogRules();
            var board = new FroggerBoard(rules, seed: 3);

            // 反复上下跳 200 次：201 条事件超过上限，日志只保留最近 128 条
            for (int i = 0; i < 200; i++)
            {
                board.Step(0f, i % 2 == 0 ? FroggerDir.Up : FroggerDir.Down);
            }

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(FroggerEventType.Hopped, board.Events[0].Type);
            Assert.Equal(FroggerEventType.Hopped, board.Events[^1].Type);
        }
    }
}
