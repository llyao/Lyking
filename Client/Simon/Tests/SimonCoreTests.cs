using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Simon.Core;
using Xunit;

namespace Lyking.Simon.Core.Tests
{
    /// <summary>西蒙记忆核心规则测试：序列确定性、播放节奏、复述判定、超时失败、前缀稳定、重置与最长纪录。</summary>
    public class SimonGameTests
    {
        [Fact]
        public void 固定种子序列与手算一致()
        {
            var expected = new Random(42);
            var expectedItems = Enumerable.Range(0, 8).Select(_ => expected.Next(SimonRules.PadCount)).ToList();

            var game = new SimonGame(seed: 42);

            Assert.Equal(expectedItems, game.Sequence.Take(8).ToList());
            Assert.All(game.Sequence, pad => Assert.InRange(pad, 0, SimonRules.PadCount - 1));
            Assert.Equal(SimonRules.MaxSequenceLength, game.Sequence.Count);
        }

        [Fact]
        public void 构造后处于待开始且无事件()
        {
            var game = new SimonGame(seed: 42);

            Assert.Equal(SimonPhase.Ready, game.Phase);
            Assert.Equal(0, game.Level);
            Assert.Equal(0, game.BestLevel);
            Assert.Equal(0, game.InputProgress);
            Assert.Equal(-1, game.LitPadIndex);
            Assert.Equal(0f, game.ShowingRemaining, 4);
            Assert.Equal(0f, game.InputRemaining, 4);
            Assert.False(game.IsOver);
            Assert.Empty(game.Events);
        }

        [Fact]
        public void 首次输入开始播放()
        {
            var game = new SimonGame(seed: 3);

            Assert.True(game.Press(2));

            Assert.Equal(SimonPhase.Showing, game.Phase);
            Assert.Equal(1, game.Level);
            Assert.Equal(game.Sequence[0], game.LitPadIndex);
            Assert.Equal(SimonRules.SegmentSeconds, game.ShowingRemaining, 3);

            Assert.Equal(SimonEventType.Started, game.Events[0].Type);
            Assert.Equal(0, game.Events[0].Level);
            Assert.Equal(SimonEventType.ShowingStarted, game.Events[1].Type);
            Assert.Equal(1, game.Events[1].Level);
            Assert.Equal(SimonEventType.Lit, game.Events[2].Type);
            Assert.Equal(game.Sequence[0], game.Events[2].PadIndex);
        }

        [Fact]
        public void 播放阶段点击无效()
        {
            var game = new SimonGame(seed: 4);
            game.Press(0);

            // 无论按的是本轮首项还是其他色块，播放阶段一律拒绝且不改变状态。
            Assert.False(game.Press(game.Sequence[0]));
            Assert.False(game.Press((game.Sequence[0] + 1) % SimonRules.PadCount));

            Assert.Equal(SimonPhase.Showing, game.Phase);
            Assert.Equal(1, game.Level);
            Assert.Equal(0, game.InputProgress);
            Assert.DoesNotContain(game.Events, e => e.Type == SimonEventType.InputCorrect);
            Assert.DoesNotContain(game.Events, e => e.Type == SimonEventType.InputWrong);
        }

        [Fact]
        public void 播放节奏逐段点亮与剩余时间递减()
        {
            var game = new SimonGame(seed: 42);
            game.Press(0);
            SkipShowing(game);
            Assert.True(game.Press(game.Sequence[0]));
            Assert.Equal(2, game.Level);

            // 第 2 轮播 2 项，共 2 × 0.6 秒；在每段的安全内部点采样，逐段断言点亮变化。
            Assert.Equal(game.Sequence[0], game.LitPadIndex);
            Assert.Equal(2 * SimonRules.SegmentSeconds, game.ShowingRemaining, 3);

            game.Step(0.2f);
            Assert.Equal(game.Sequence[0], game.LitPadIndex);
            Assert.Equal(1.0f, game.ShowingRemaining, 3);

            game.Step(0.3f);
            Assert.Equal(-1, game.LitPadIndex);

            game.Step(0.3f);
            Assert.Equal(game.Sequence[1], game.LitPadIndex);
            Assert.Equal(0.4f, game.ShowingRemaining, 3);

            game.Step(0.3f);
            Assert.Equal(-1, game.LitPadIndex);

            game.Step(0.2f);
            Assert.Equal(SimonPhase.Input, game.Phase);
            Assert.Equal(0f, game.ShowingRemaining, 3);
        }

        [Fact]
        public void 播放完毕进入复述()
        {
            var game = new SimonGame(seed: 5);
            game.Press(0);
            Assert.Equal(SimonPhase.Showing, game.Phase);

            // 第 1 轮只播 1 项共 0.6 秒，单步跨过后应进入复述。
            game.Step(SimonRules.SegmentSeconds + 0.1f);

            Assert.Equal(SimonPhase.Input, game.Phase);
            Assert.Equal(-1, game.LitPadIndex);
            Assert.Equal(0, game.InputProgress);
            Assert.Equal(0f, game.ShowingRemaining, 4);
            Assert.Equal(SimonRules.InputTimeoutSeconds, game.InputRemaining, 3);
        }

        [Fact]
        public void 复述正确推进序列长度与等级()
        {
            var game = new SimonGame(seed: 7);
            game.Press(0);
            SkipShowing(game);

            Assert.True(game.Press(game.Sequence[0]));
            Assert.Equal(2, game.Level);
            Assert.Equal(SimonPhase.Showing, game.Phase);

            // 完成一轮的末尾事件依次为：复述正确、播放开始（新等级）、点亮首项。
            Assert.Equal(SimonEventType.InputCorrect, game.Events[^3].Type);
            Assert.Equal(game.Sequence[0], game.Events[^3].PadIndex);
            Assert.Equal(1, game.Events[^3].Detail);
            Assert.Equal(SimonEventType.ShowingStarted, game.Events[^2].Type);
            Assert.Equal(2, game.Events[^2].Level);

            SkipShowing(game);
            Assert.True(game.Press(game.Sequence[0]));
            Assert.Equal(SimonPhase.Input, game.Phase);
            Assert.Equal(1, game.InputProgress);
            Assert.True(game.Press(game.Sequence[1]));

            // 输满第 2 轮即升到第 3 轮，序列长度变为 3 且进度清零。
            Assert.Equal(3, game.Level);
            Assert.Equal(SimonPhase.Showing, game.Phase);
            Assert.Equal(0, game.InputProgress);
        }

        [Fact]
        public void 复述错误即结束并记录最长纪录()
        {
            var game = new SimonGame(seed: 8);
            game.Press(0);
            SkipShowing(game);
            Assert.True(game.Press(game.Sequence[0]));
            SkipShowing(game);

            int wrongPad = (game.Sequence[0] + 1) % SimonRules.PadCount;
            Assert.False(game.Press(wrongPad));

            Assert.Equal(SimonPhase.GameOver, game.Phase);
            Assert.True(game.IsOver);
            Assert.Equal(1, game.BestLevel);

            Assert.Equal(SimonEventType.InputWrong, game.Events[^3].Type);
            Assert.Equal(wrongPad, game.Events[^3].PadIndex);
            Assert.Equal(game.Sequence[0], game.Events[^3].Detail);
            Assert.Equal(SimonEventType.Failed, game.Events[^2].Type);
            Assert.Equal(SimonEventType.NewRecord, game.Events[^1].Type);
            Assert.Equal(1, game.Events[^1].Detail);

            Assert.False(game.Press(game.Sequence[0]));
            Assert.Equal(SimonPhase.GameOver, game.Phase);
        }

        [Fact]
        public void 复述超时不输入失败()
        {
            var game = new SimonGame(seed: 9);
            game.Press(0);
            SkipShowing(game);
            Assert.Equal(SimonPhase.Input, game.Phase);

            game.Step(SimonRules.InputTimeoutSeconds * 0.5f);
            Assert.Equal(SimonPhase.Input, game.Phase);
            Assert.Equal(SimonRules.InputTimeoutSeconds * 0.5f, game.InputRemaining, 3);

            game.Step(SimonRules.InputTimeoutSeconds);

            Assert.Equal(SimonPhase.GameOver, game.Phase);
            Assert.Equal(0f, game.InputRemaining, 4);
            Assert.Equal(0, game.BestLevel);
            Assert.Contains(game.Events, e => e.Type == SimonEventType.Failed);
            Assert.DoesNotContain(game.Events, e => e.Type == SimonEventType.InputWrong);
            Assert.DoesNotContain(game.Events, e => e.Type == SimonEventType.NewRecord);
        }

        [Fact]
        public void 第N轮序列与前轮共享前缀()
        {
            var game = new SimonGame(seed: 11);
            Assert.True(game.Press(0));

            var round1 = CollectLitPads(game);
            Assert.Equal(new[] { game.Sequence[0] }, round1);

            Assert.True(game.Press(round1[0]));
            var round2 = CollectLitPads(game);
            Assert.Equal(2, round2.Count);
            Assert.Equal(round1[0], round2[0]);
            Assert.Equal(game.Sequence[1], round2[1]);

            Assert.True(game.Press(round2[0]));
            Assert.True(game.Press(round2[1]));
            var round3 = CollectLitPads(game);
            Assert.Equal(3, round3.Count);
            Assert.Equal(round2[0], round3[0]);
            Assert.Equal(round2[1], round3[1]);
            Assert.Equal(game.Sequence[2], round3[2]);
        }

        [Fact]
        public void 重置清空状态与日志并重建序列()
        {
            var game = new SimonGame(seed: 5);
            game.Press(0);
            SkipShowing(game);
            Assert.True(game.Press(game.Sequence[0]));
            Assert.Equal(2, game.Level);

            game.Reset(seed: 99);

            Assert.Equal(SimonPhase.Ready, game.Phase);
            Assert.Equal(0, game.Level);
            Assert.Equal(0, game.InputProgress);
            Assert.Equal(-1, game.LitPadIndex);
            Assert.Equal(0f, game.ShowingRemaining, 4);
            Assert.Equal(0f, game.InputRemaining, 4);

            // 日志清空后仅保留重置事件。
            Assert.Single(game.Events);
            Assert.Equal(SimonEventType.Reset, game.Events[0].Type);

            var rebuilt = new SimonGame(seed: 99);
            Assert.Equal(rebuilt.Sequence.ToList(), game.Sequence.ToList());
        }

        [Fact]
        public void 最长纪录跨局保留()
        {
            var game = new SimonGame(seed: 12);
            FailInRoundTwo(game);
            Assert.Equal(1, game.BestLevel);

            game.Reset(seed: 77);
            Assert.Equal(1, game.BestLevel);

            // 第二局同样止步第 2 轮：纪录持平不刷新，也不再重复记新纪录。
            FailInRoundTwo(game);
            Assert.Equal(1, game.BestLevel);
            Assert.DoesNotContain(game.Events, e => e.Type == SimonEventType.NewRecord);
        }

        [Fact]
        public void 同种子对局完全一致且不同种子序列不同()
        {
            Assert.Equal(Simulate(31337), Simulate(31337));
            Assert.NotEqual(Simulate(31337), Simulate(31338));
            Assert.NotEqual(new SimonGame(1).Sequence.ToList(), new SimonGame(2).Sequence.ToList());
        }

        [Fact]
        public void 越界色块编号抛出异常()
        {
            var game = new SimonGame(seed: 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => game.Press(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.Press(SimonRules.PadCount));
            Assert.Equal(SimonPhase.Ready, game.Phase);
        }

        [Fact]
        public void 事件日志封顶一百二十八条()
        {
            var game = new SimonGame(seed: 8);
            Assert.True(game.Press(0));

            // 连打 12 轮：点亮与复述正确事件累计超过封顶，最早的事件应被裁剪。
            for (int round = 1; round <= 12; round++)
            {
                SkipShowing(game);
                // 先固定本轮项数：输满最后一项时等级立即自增，不能拿实时 Level 当循环边界。
                int items = game.Level;
                for (int i = 0; i < items; i++) Assert.True(game.Press(game.Sequence[i]));
            }

            Assert.Equal(13, game.Level);
            Assert.Equal(128, game.Events.Count);
            Assert.NotEqual(SimonEventType.Started, game.Events[0].Type);
        }

        /// <summary>以小步长推进播放阶段直到进入复述，收集逐个点亮的色块编号。</summary>
        private static List<int> CollectLitPads(SimonGame game)
        {
            var pads = new List<int>();
            int current = -1;
            int guard = 0;
            while (game.Phase == SimonPhase.Showing && guard++ < 4096)
            {
                game.Step(0.05f);
                if (game.LitPadIndex >= 0)
                {
                    if (game.LitPadIndex != current)
                    {
                        current = game.LitPadIndex;
                        pads.Add(current);
                    }
                }
                else
                {
                    current = -1;
                }
            }

            Assert.Equal(SimonPhase.Input, game.Phase);
            return pads;
        }

        /// <summary>大步快进，跳过当前轮的整个播放阶段。</summary>
        private static void SkipShowing(SimonGame game)
        {
            int guard = 0;
            while (game.Phase == SimonPhase.Showing && guard++ < 4096)
            {
                game.Step(SimonRules.SegmentSeconds / 4f);
            }

            Assert.Equal(SimonPhase.Input, game.Phase);
        }

        /// <summary>开局、完整复述第 1 轮后在第 2 轮故意按错，止步于 1 轮纪录。</summary>
        private static void FailInRoundTwo(SimonGame game)
        {
            Assert.True(game.Press(0));
            SkipShowing(game);
            Assert.True(game.Press(game.Sequence[0]));
            SkipShowing(game);
            Assert.False(game.Press((game.Sequence[0] + 1) % SimonRules.PadCount));
            Assert.Equal(SimonPhase.GameOver, game.Phase);
        }

        /// <summary>以固定脚本打一局：完整复述 3 轮后在第 4 轮按错，返回对局终态与全部事件类型的指纹。</summary>
        private static string Simulate(int seed)
        {
            var game = new SimonGame(seed);
            Assert.True(game.Press(0));
            for (int round = 1; round <= 3; round++)
            {
                SkipShowing(game);
                int items = game.Level;
                for (int i = 0; i < items; i++) Assert.True(game.Press(game.Sequence[i]));
            }

            SkipShowing(game);
            game.Press((game.Sequence[0] + 1) % SimonRules.PadCount);

            return string.Join(';',
                game.Level,
                game.BestLevel,
                game.Phase,
                string.Join(';', game.Sequence),
                string.Join(';', game.Events.Select(e => $"{e.Level}.{e.Type}.{e.PadIndex}.{e.Detail}")));
        }
    }
}
