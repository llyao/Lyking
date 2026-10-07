using System.Linq;
using Lyking.Frogger.Core;
using NUnit.Framework;

namespace Lyking.Frogger.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class FroggerEditModeTests
    {
        /// <summary>通关测试规则：马路无车、河道木排满铺且慢速滚动，任何整列落点都在木排上。</summary>
        private static FroggerRules FullLogRules() => new FroggerRules(
            width: 13,
            maxLives: 3,
            goalCount: 5,
            scorePerGoal: 200,
            roundSpeedGain: 0.15f,
            frogHalfWidth: 0.35f,
            carCounts: new[] { 0, 0, 0, 0 },
            logCounts: new[] { 6, 6, 6, 6 },
            logSpeeds: new[] { 0.5f, 0.5f, 0.5f, 0.5f },
            logLengths: new[] { 2f, 2f, 2f, 2f });

        [Test]
        public void 棋局在引擎外可创建并按子步推进()
        {
            var board = new FroggerBoard(FroggerRules.Default, seed: 7);

            Assert.AreEqual(3, board.Lives);
            Assert.AreEqual(1, board.Round);
            Assert.AreEqual(0, board.FrogRow);
            Assert.IsFalse(board.IsFinished);
            Assert.AreEqual(8, board.Lanes.Count);

            float before = board.Lanes[0].Positions[0];
            board.Step(0.5f, null);
            Assert.AreEqual(0.5f, board.Elapsed, 0.05f);
            Assert.AreNotEqual(before, board.Lanes[0].Positions[0]);
        }

        [Test]
        public void 越界跳跃被拒绝并记录事件()
        {
            var board = new FroggerBoard(FroggerRules.Default, seed: 7);

            board.Step(0f, FroggerDir.Down);
            Assert.AreEqual(0, board.FrogRow);
            Assert.AreEqual(FroggerEventType.HopRejected, board.Events[board.Events.Count - 1].Type);

            for (int i = 0; i < 7; i++) board.Step(0f, FroggerDir.Left);
            Assert.AreEqual(0, board.FrogColumn);
            Assert.AreEqual(FroggerEventType.HopRejected, board.Events[board.Events.Count - 1].Type);
        }

        [Test]
        public void 河道落水即溺水失命并回到起点()
        {
            // 静止木排规则：扫描一个河道第一行木排不覆盖起点列 6 的种子
            var rules = new FroggerRules(
                width: 13,
                maxLives: 3,
                goalCount: 5,
                scorePerGoal: 200,
                roundSpeedGain: 0.15f,
                frogHalfWidth: 0.35f,
                carCounts: new[] { 0, 0, 0, 0 },
                logCounts: new[] { 1, 1, 1, 1 },
                logSpeeds: new[] { 0f, 0f, 0f, 0f },
                logLengths: new[] { 2f, 2f, 2f, 2f });

            FroggerBoard? drowned = null;
            for (int seed = 0; seed < 100 && drowned == null; seed++)
            {
                var candidate = new FroggerBoard(rules, seed);
                for (int row = 0; row < 8; row++) candidate.Step(0f, FroggerDir.Up);
                if (candidate.Events.Any(e => e.Type == FroggerEventType.Drowned)) drowned = candidate;
            }

            Assert.NotNull(drowned);
            Assert.AreEqual(2, drowned!.Lives);
            Assert.IsFalse(drowned.IsFinished);
            Assert.AreEqual(0, drowned.FrogRow);
            Assert.AreEqual(rules.StartColumn, drowned.FrogColumn);
            Assert.IsTrue(drowned.Events.Any(e => e.Type == FroggerEventType.LifeLost));
        }

        [Test]
        public void 到达荷花位进位计分并回起点()
        {
            var board = new FroggerBoard(FullLogRules(), seed: 5);

            for (int row = 0; row < 12; row++) board.Step(0.02f, FroggerDir.Up);

            Assert.AreEqual(200, board.Score);
            Assert.AreEqual(1, board.GoalsFilled);
            Assert.AreEqual(0, board.FrogRow);
            Assert.AreEqual(FullLogRules().StartColumn, board.FrogColumn);
            Assert.IsTrue(board.Events.Any(e => e.Type == FroggerEventType.GoalReached && e.Index == 2));
        }
    }
}
