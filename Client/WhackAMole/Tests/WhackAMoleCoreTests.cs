using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.WhackAMole.Core;
using Xunit;

namespace Lyking.WhackAMole.Core.Tests
{
    /// <summary>打地鼠核心规则测试：种子确定性、地鼠调度、命中计分、连击加成、炸弹扣分、倒计时与状态机。</summary>
    public class WhackAMoleGameTests
    {
        /// <summary>测试推进粒度（秒），与手算的冒头/缩回时刻网格一致。</summary>
        private const float Step = 0.05f;

        [Fact]
        public void 构造后九洞就绪且处于未开始状态()
        {
            Assert.Equal(3, WhackAMoleRules.HoleRows);
            Assert.Equal(3, WhackAMoleRules.HoleColumns);
            Assert.Equal(9, WhackAMoleRules.HoleCount);
            Assert.Equal(30f, WhackAMoleRules.RoundSeconds);

            var game = new WhackAMoleGame(seed: 42);

            Assert.Equal(WhackAMolePhase.Ready, game.Phase);
            Assert.False(game.HasMole);
            Assert.Equal(-1, game.MoleHole);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.Combo);
            Assert.Equal(30f, game.TimeRemaining, 4);
            Assert.False(game.IsFinished);
            Assert.Empty(game.Events);
        }

        [Fact]
        public void 首次输入开始对局且不计打击()
        {
            var game = new WhackAMoleGame(seed: 7);

            // Ready 阶段的首次输入只负责开局：倒计时从零开始，本拍不判定命中。
            Assert.False(game.Whack(0));

            Assert.Equal(WhackAMolePhase.Running, game.Phase);
            Assert.Equal(0f, game.Elapsed, 4);
            Assert.Equal(30f, game.TimeRemaining, 4);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.Combo);
            Assert.Equal(WhackAMoleEventType.Started, game.Events[^1].Type);
            Assert.Equal(-1, game.Events[^1].HoleIndex);
            Assert.DoesNotContain(game.Events, e => e.Type == WhackAMoleEventType.Missed);
        }

        [Fact]
        public void 固定种子地鼠序列手算()
        {
            // 种子 7 的抽签序手算：开局调度抽得间隔 0.6299、洞位 7、普通地鼠（类型签 ≥ 0.2），
            // 故第一只在第 13 步（0.65 秒）冒头；停留 0.5 + 0.7×(30−0.65)/30 = 1.1848 秒，
            // 到 1.85 秒自动缩回；缩回时调度抽得间隔 0.4314，故第二只在 2.30 秒冒头、洞位 3、仍为普通。
            var game = new WhackAMoleGame(seed: 7);
            Assert.False(game.Whack(0));

            Assert.Equal(0.65f, TickUntilMole(game), 3);
            Assert.Equal(7, game.MoleHole);
            Assert.False(game.MoleIsBomb);
            Assert.Equal(WhackAMoleEventType.Spawned, game.Events[^1].Type);
            Assert.Equal(0.65f, game.Events[^1].Time, 3);
            Assert.Equal(1.1848f, game.MoleRetractTime - game.MoleSpawnTime, 3);

            while (game.HasMole) game.Tick(Step);
            Assert.Equal(WhackAMoleEventType.Retreated, game.Events[^1].Type);
            Assert.Equal(1.85f, game.Events[^1].Time, 3);

            Assert.Equal(2.3f, TickUntilMole(game), 3);
            Assert.Equal(3, game.MoleHole);
            Assert.False(game.MoleIsBomb);
        }

        [Fact]
        public void 地鼠停留时长后自动缩回()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            TickUntilMole(game);
            float spawnTime = game.MoleSpawnTime;
            float stay = game.MoleRetractTime - game.MoleSpawnTime;
            Assert.Equal(1.1848f, stay, 3);

            // 停留时长内不打，到时自动缩回：缩回事件落在计划缩回时刻之后的第一个推进粒度上。
            while (game.HasMole) game.Tick(Step);

            Assert.False(game.HasMole);
            Assert.Equal(WhackAMoleEventType.Retreated, game.Events[^1].Type);
            Assert.InRange(game.Events[^1].Time - (spawnTime + stay), -1e-4f, Step + 1e-4f);

            // 缩回后按冒头间隔冒出下一只。
            Assert.Equal(2.3f, TickUntilMole(game), 3);
            Assert.Equal(3, game.MoleHole);
        }

        [Fact]
        public void 命中普通地鼠加分且连击增加()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            TickUntilMole(game);
            Assert.Equal(7, game.MoleHole);
            Assert.False(game.MoleIsBomb);

            int hitHole = game.MoleHole;
            Assert.True(game.Whack(hitHole));

            // 100 基础分 + 连击加成 20×连击 1。
            Assert.Equal(120, game.Score);
            Assert.Equal(1, game.Combo);
            Assert.False(game.HasMole);

            var hit = game.Events[^1];
            Assert.Equal(WhackAMoleEventType.Hit, hit.Type);
            Assert.Equal(hitHole, hit.HoleIndex);
            Assert.Equal(120, hit.Score);
            Assert.Equal(1, hit.Combo);
        }

        [Fact]
        public void 连击加成随连击数增长且封顶()
        {
            Assert.Equal(20, WhackAMoleRules.ComboBonusFor(1));
            Assert.Equal(80, WhackAMoleRules.ComboBonusFor(4));
            Assert.Equal(100, WhackAMoleRules.ComboBonusFor(5));
            Assert.Equal(100, WhackAMoleRules.ComboBonusFor(6));

            // 种子 7 前三只地鼠全为普通：连续命中得 120、140、160，累计 120、260、420。
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);
            int[] expectedScores = { 120, 260, 420 };

            for (int i = 0; i < expectedScores.Length; i++)
            {
                TickUntilMole(game);
                Assert.False(game.MoleIsBomb, "种子 7 前三只地鼠应为普通。");
                Assert.True(game.Whack(game.MoleHole));
                Assert.Equal(expectedScores[i], game.Score);
                Assert.Equal(i + 1, game.Combo);
            }
        }

        [Fact]
        public void 打空连击清零且总分不变()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            TickUntilMole(game);
            int hitHole = game.MoleHole;
            Assert.True(game.Whack(hitHole));
            Assert.Equal(1, game.Combo);
            Assert.Equal(120, game.Score);

            // 地鼠已缩回，再打任何洞都是打空：连击清零、总分不变。
            Assert.False(game.Whack((hitHole + 1) % WhackAMoleRules.HoleCount));

            Assert.Equal(0, game.Combo);
            Assert.Equal(120, game.Score);
            Assert.Equal(WhackAMoleEventType.Missed, game.Events[^1].Type);
        }

        [Fact]
        public void 命中炸弹地鼠按规则扣分()
        {
            // 种子 1 前三只为普通、普通、炸弹：先连得 120 + 140 = 260，再命中炸弹扣 200 得 60。
            var game = new WhackAMoleGame(seed: 1);
            game.Whack(0);

            for (int i = 0; i < 2; i++)
            {
                TickUntilMole(game);
                Assert.False(game.MoleIsBomb, "种子 1 前两只地鼠应为普通。");
                Assert.True(game.Whack(game.MoleHole));
            }

            Assert.Equal(260, game.Score);
            TickUntilMole(game);
            Assert.True(game.MoleIsBomb, "种子 1 第三只地鼠应为炸弹。");
            Assert.True(game.Whack(game.MoleHole));

            Assert.Equal(60, game.Score);
            Assert.Equal(0, game.Combo);
            Assert.Equal(WhackAMoleEventType.BombHit, game.Events[^1].Type);
        }

        [Fact]
        public void 分数下限夹零()
        {
            // 种子 42 首只即炸弹：0 − 200 应夹 0 下限，而不是 −200。
            var game = new WhackAMoleGame(seed: 42);
            game.Whack(0);

            TickUntilMole(game);
            Assert.True(game.MoleIsBomb);
            Assert.Equal(1, game.MoleHole);
            Assert.True(game.Whack(game.MoleHole));

            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.Combo);
            Assert.Equal(WhackAMoleEventType.BombHit, game.Events[^1].Type);
        }

        [Fact]
        public void 同一地鼠重复打击无效()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            TickUntilMole(game);
            int hitHole = game.MoleHole;
            Assert.True(game.Whack(hitHole));
            Assert.Equal(120, game.Score);

            // 命中后地鼠立刻缩回，同洞第二击落空：不重复计分，按打空清零连击。
            Assert.False(game.Whack(hitHole));

            Assert.Equal(120, game.Score);
            Assert.Equal(0, game.Combo);
            Assert.Equal(1, game.Events.Count(e => e.Type == WhackAMoleEventType.Hit));
            Assert.Equal(WhackAMoleEventType.Missed, game.Events[^1].Type);
        }

        [Fact]
        public void 打击空洞无效()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            // 第一只冒头前（0.25 秒）九洞皆空。
            for (int i = 0; i < 5; i++) game.Tick(Step);
            Assert.False(game.HasMole);
            Assert.False(game.Whack(8));
            Assert.Equal(0, game.Score);
            Assert.Equal(WhackAMoleEventType.Missed, game.Events[^1].Type);
            Assert.Equal(8, game.Events[^1].HoleIndex);

            // 地鼠冒头后打错洞同样按打空处理。
            TickUntilMole(game);
            int moleHole = game.MoleHole;
            Assert.False(game.Whack((moleHole + 1) % WhackAMoleRules.HoleCount));
            Assert.Equal(0, game.Score);
            Assert.Equal(WhackAMoleEventType.Missed, game.Events[^1].Type);
        }

        [Fact]
        public void 倒计时归零结束后输入与推进无效()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            for (int i = 0; i < 30; i++) game.Tick(1f);

            Assert.Equal(WhackAMolePhase.GameOver, game.Phase);
            Assert.True(game.IsFinished);
            Assert.Equal(0f, game.TimeRemaining, 4);
            Assert.False(game.HasMole);
            Assert.Equal(WhackAMoleEventType.TimeUp, game.Events[^2].Type);
            Assert.Equal(WhackAMoleEventType.Finished, game.Events[^1].Type);
            Assert.Equal(30f, game.Events[^1].Time, 2);

            int count = game.Events.Count;
            Assert.False(game.Whack(0));
            game.Tick(1f);
            Assert.Equal(count, game.Events.Count);
            Assert.Equal(WhackAMolePhase.GameOver, game.Phase);
        }

        [Fact]
        public void 地鼠停留时长随时间缩短()
        {
            // 纯函数：剩余 30 秒停留 1.2 秒，剩余 15 秒 0.85 秒，剩余 0 秒夹 0.5 秒下限。
            Assert.Equal(1.2f, WhackAMoleRules.StaySecondsFor(30f), 4);
            Assert.Equal(0.85f, WhackAMoleRules.StaySecondsFor(15f), 4);
            Assert.Equal(0.5f, WhackAMoleRules.StaySecondsFor(0f), 4);
            Assert.True(WhackAMoleRules.StaySecondsFor(10f) > WhackAMoleRules.StaySecondsFor(5f));

            // 对局内全程不打：每只地鼠的停留时长随冒头时刻单调缩短。
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);
            var stays = new List<(float Spawn, float Stay)>();
            bool wasUp = false;

            while (!game.IsFinished)
            {
                if (game.HasMole && !wasUp) stays.Add((game.MoleSpawnTime, game.MoleRetractTime - game.MoleSpawnTime));
                wasUp = game.HasMole;
                game.Tick(Step);
            }

            Assert.Equal(19, stays.Count);
            // 首只接近 1.2 秒基础停留，末只（29.85 秒冒头）已逼近 0.5 秒下限。
            Assert.InRange(stays[0].Stay, 1.18f, 1.19f);
            Assert.InRange(stays[^1].Stay, 0.5f, 0.51f);
            for (int i = 1; i < stays.Count; i++)
            {
                Assert.True(stays[i].Spawn > stays[i - 1].Spawn, "冒头时刻应单调递增。");
                Assert.True(stays[i].Stay < stays[i - 1].Stay, "停留时长应随冒头时刻缩短。");
            }
        }

        [Fact]
        public void 状态机从未开始经进行中到结束()
        {
            var game = new WhackAMoleGame(seed: 7);
            Assert.Equal(WhackAMolePhase.Ready, game.Phase);

            // 未开始时推进无效：倒计时与冒头调度都不走。
            for (int i = 0; i < 5; i++) game.Tick(1f);
            Assert.Equal(WhackAMolePhase.Ready, game.Phase);
            Assert.Equal(30f, game.TimeRemaining, 4);
            Assert.False(game.HasMole);
            Assert.Empty(game.Events);

            Assert.False(game.Whack(4));
            Assert.Equal(WhackAMolePhase.Running, game.Phase);

            for (int i = 0; i < 30; i++) game.Tick(1f);
            Assert.Equal(WhackAMolePhase.GameOver, game.Phase);
            Assert.True(game.IsFinished);
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string PlayToEnd(int seed)
            {
                var game = new WhackAMoleGame(seed);
                game.Whack(0);
                while (!game.IsFinished)
                {
                    // 确定性策略：普通地鼠冒头立即打击，炸弹地鼠放任自动缩回。
                    if (game.HasMole && !game.MoleIsBomb) game.Whack(game.MoleHole);
                    game.Tick(Step);
                }

                return $"score={game.Score};combo={game.Combo};"
                    + string.Join('|', game.Events.Select(e => $"{e.Type}:{e.HoleIndex}:{e.Score}:{e.Combo}:{e.Time:0.000}"));
            }

            Assert.Equal(PlayToEnd(777), PlayToEnd(777));
            Assert.NotEqual(PlayToEnd(777), PlayToEnd(778));
        }

        [Fact]
        public void 事件日志封顶一百二十八条裁剪()
        {
            var game = new WhackAMoleGame(seed: 7);
            game.Whack(0);

            // 连续打空 250 拍（避开冒头地鼠），事件远超封顶后只保留最新 128 条。
            for (int i = 0; i < 250; i++)
            {
                game.Tick(Step);
                int hole = game.HasMole && game.MoleHole == 0 ? 1 : 0;
                game.Whack(hole);
            }

            Assert.Equal(WhackAMoleRules.MaxEventLogs, game.Events.Count);
            Assert.Equal(WhackAMoleEventType.Missed, game.Events[^1].Type);
            Assert.DoesNotContain(game.Events, e => e.Type == WhackAMoleEventType.Started);
        }

        /// <summary>按固定粒度推进直到有地鼠冒头，返回推进后的流逝时间。</summary>
        private static float TickUntilMole(WhackAMoleGame game)
        {
            while (!game.HasMole)
            {
                game.Tick(Step);
                Assert.False(game.IsFinished, "倒计时结束前应有地鼠冒头。");
            }

            return game.Elapsed;
        }
    }
}
