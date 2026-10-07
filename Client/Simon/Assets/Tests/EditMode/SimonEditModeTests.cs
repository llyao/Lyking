using System.Linq;
using Lyking.Simon.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Simon.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SimonEditModeTests
    {
        [Test]
        public void 待开始对局就绪且序列合法()
        {
            var game = new SimonGame(seed: 7);

            Assert.AreEqual(SimonRules.PadCount, 4);
            Assert.AreEqual(SimonPhase.Ready, game.Phase);
            Assert.AreEqual(0, game.Level);
            Assert.AreEqual(0, game.BestLevel);
            Assert.AreEqual(-1, game.LitPadIndex);
            Assert.IsEmpty(game.Events);
            Assert.AreEqual(SimonRules.MaxSequenceLength, game.Sequence.Count);
            Assert.That(game.Sequence, Is.All.InRange(0, SimonRules.PadCount - 1));
        }

        [Test]
        public void 首次输入开始播放并按节奏进入复述()
        {
            var game = new SimonGame(seed: 7);

            Assert.IsTrue(game.Press(3));
            Assert.AreEqual(SimonPhase.Showing, game.Phase);
            Assert.AreEqual(1, game.Level);
            Assert.AreEqual(game.Sequence[0], game.LitPadIndex);

            // 点亮 0.4 秒后转入间隔熄灭，跨过 0.6 秒整段后进入复述。
            game.Step(SimonRules.LitSeconds * 0.5f);
            Assert.AreEqual(game.Sequence[0], game.LitPadIndex);
            game.Step(SimonRules.GapSeconds + 0.2f);
            Assert.AreEqual(-1, game.LitPadIndex);

            game.Step(SimonRules.SegmentSeconds);
            Assert.AreEqual(SimonPhase.Input, game.Phase);
            Assert.AreEqual(SimonRules.InputTimeoutSeconds, game.InputRemaining, 1e-4f);
        }

        [Test]
        public void 复述错误即失败并记录最长纪录()
        {
            var game = new SimonGame(seed: 7);
            game.Press(0);
            SkipShowing(game);
            Assert.IsTrue(game.Press(game.Sequence[0]));
            SkipShowing(game);

            int wrongPad = (game.Sequence[0] + 1) % SimonRules.PadCount;
            Assert.IsFalse(game.Press(wrongPad));

            Assert.AreEqual(SimonPhase.GameOver, game.Phase);
            Assert.IsTrue(game.IsOver);
            Assert.AreEqual(1, game.BestLevel);

            var types = game.Events.Select(e => e.Type).ToList();
            Assert.Contains(SimonEventType.InputWrong, types);
            Assert.Contains(SimonEventType.Failed, types);
            Assert.Contains(SimonEventType.NewRecord, types);
            Assert.AreEqual(wrongPad, game.Events.Last(e => e.Type == SimonEventType.InputWrong).PadIndex);
        }

        [Test]
        public void 重置清空日志并保留最长纪录()
        {
            var game = new SimonGame(seed: 7);
            game.Press(0);
            SkipShowing(game);
            game.Press(game.Sequence[0]);
            SkipShowing(game);
            game.Press((game.Sequence[0] + 1) % SimonRules.PadCount);
            Assert.AreEqual(SimonPhase.GameOver, game.Phase);
            Assert.AreEqual(1, game.BestLevel);

            game.Reset(seed: 9);

            Assert.AreEqual(SimonPhase.Ready, game.Phase);
            Assert.AreEqual(0, game.Level);
            Assert.AreEqual(1, game.BestLevel);
            Assert.AreEqual(-1, game.LitPadIndex);
            Assert.AreEqual(1, game.Events.Count);
            Assert.AreEqual(SimonEventType.Reset, game.Events[0].Type);
            var rebuilt = new SimonGame(seed: 9);
            CollectionAssert.AreEqual(rebuilt.Sequence.ToList(), game.Sequence.ToList());
        }

        private static void SkipShowing(SimonGame game)
        {
            int guard = 0;
            while (game.Phase == SimonPhase.Showing && guard++ < 4096)
            {
                game.Step(SimonRules.SegmentSeconds / 4f);
            }

            Assert.AreEqual(SimonPhase.Input, game.Phase);
        }
    }
}
