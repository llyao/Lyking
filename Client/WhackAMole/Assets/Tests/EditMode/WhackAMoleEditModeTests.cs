using System.Collections.Generic;
using Lyking.WhackAMole.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.WhackAMole.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，开局、命中与倒计时表现一致。</summary>
    public sealed class WhackAMoleEditModeTests
    {
        private const float Step = 0.05f;

        [Test]
        public void 开局九洞就绪且处于未开始状态()
        {
            var game = new WhackAMoleGame(seed: 7);

            Assert.AreEqual(9, WhackAMoleRules.HoleCount);
            Assert.AreEqual(WhackAMoleRules.RoundSeconds, 30f);
            Assert.AreEqual(WhackAMolePhase.Ready, game.Phase);
            Assert.IsFalse(game.HasMole);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(0, game.Combo);
            Assert.AreEqual(WhackAMoleRules.RoundSeconds, game.TimeRemaining, 1e-4f);
            Assert.IsEmpty(game.Events);
        }

        [Test]
        public void 首次打击开局并命中首只普通地鼠()
        {
            var game = new WhackAMoleGame(seed: 7);

            Assert.IsFalse(game.Whack(0));
            Assert.AreEqual(WhackAMolePhase.Running, game.Phase);
            Assert.AreEqual(WhackAMoleEventType.Started, game.Events[game.Events.Count - 1].Type);

            TickUntilMole(game);
            Assert.AreEqual(7, game.MoleHole);
            Assert.IsFalse(game.MoleIsBomb);

            Assert.IsTrue(game.Whack(game.MoleHole));
            Assert.AreEqual(120, game.Score);
            Assert.AreEqual(1, game.Combo);
            Assert.IsFalse(game.HasMole);
            Assert.AreEqual(WhackAMoleEventType.Hit, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 倒计时归零写入时间到与结束事件()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            for (int i = 0; i < 30; i++) game.Tick(1f);

            Assert.AreEqual(WhackAMolePhase.GameOver, game.Phase);
            Assert.IsTrue(game.IsFinished);
            Assert.AreEqual(0f, game.TimeRemaining, 1e-4f);
            Assert.IsFalse(game.HasMole);
            Assert.AreEqual(WhackAMoleEventType.TimeUp, game.Events[game.Events.Count - 2].Type);
            Assert.AreEqual(WhackAMoleEventType.Finished, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 同种子冒头序列与事件完全一致()
        {
            Assert.AreEqual(TimelineOf(7), TimelineOf(7));
            Assert.AreNotEqual(TimelineOf(7), TimelineOf(8));
        }

        /// <summary>以 0.05 秒粒度推进整局（不打），返回前若干只地鼠的冒头快照与事件类型序列。</summary>
        private static string TimelineOf(int seed)
        {
            var game = new WhackAMoleGame(seed);
            game.Whack(0);
            var moles = new List<string>();
            bool wasUp = false;

            while (!game.IsFinished)
            {
                if (game.HasMole && !wasUp)
                {
                    moles.Add($"{game.MoleHole}:{game.MoleIsBomb}:{game.MoleSpawnTime:0.000}");
                }

                wasUp = game.HasMole;
                game.Tick(Step);
            }

            return $"{string.Join(';', moles)}|{game.Score}|{string.Join(',', game.Events)}";
        }

        private static void TickUntilMole(WhackAMoleGame game)
        {
            while (!game.HasMole)
            {
                game.Tick(Step);
                Assert.IsFalse(game.IsFinished, "倒计时结束前应有地鼠冒头。");
            }
        }
    }
}
