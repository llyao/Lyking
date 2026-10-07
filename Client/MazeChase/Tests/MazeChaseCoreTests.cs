using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.MazeChase.Core;
using Xunit;

namespace Lyking.MazeChase.Core.Tests
{
    /// <summary>吃豆迷宫核心规则测试：地图解析、连续移动、转角缓冲、计分、惊惧、抓捕、过关与确定性。</summary>
    public class MazeChaseGameTests
    {
        /// <summary>构造一张以给定行为地图、幽灵封在死格里的测试规则表。</summary>
        private static MazeChaseRules RulesFor(string[] rows, float playerSpeed = 5f, float ghostSpeed = 4f,
            float powerDurationSeconds = 1f, float respawnDelaySeconds = 0.5f, int lives = 3, int ghostCount = 1)
        {
            return new MazeChaseRules(
                new MazeMap(rows),
                playerSpeed: playerSpeed,
                ghostSpeed: ghostSpeed,
                powerDurationSeconds: powerDurationSeconds,
                respawnDelaySeconds: respawnDelaySeconds,
                collisionDistance: 0.6f,
                scorePerPellet: 10,
                scorePerPowerPellet: 50,
                scorePerGhost: 200,
                lives: lives,
                ghostCount: ghostCount,
                levelSpeedGain: 0.1f);
        }

        [Fact]
        public void 地图解析_墙豆能量豆与出生点数量正确()
        {
            var map = new MazeMap(new[]
            {
                "#######",
                "#P..o #",
                "#.##..#",
                "#...G.#",
                "#######",
            });

            Assert.Equal(7, map.Width);
            Assert.Equal(5, map.Height);
            Assert.Equal(new GridPos(1, 3), map.PlayerStart);
            Assert.Single(map.GhostHouses);
            Assert.Equal(new GridPos(4, 1), map.GhostHouses[0]);
            Assert.Equal(9, map.InitialPellets.Count);
            Assert.Equal(new GridPos(4, 3), Assert.Single(map.InitialPowerPellets));
            Assert.Equal(10, map.TotalPelletCount);
            Assert.Equal(22, map.Walls.Count);
            Assert.True(map.IsWall(new GridPos(2, 2)));
            Assert.True(map.IsWalkable(new GridPos(5, 3)));
            Assert.True(map.IsWall(new GridPos(-1, 0)));
            Assert.True(map.IsWall(new GridPos(7, 0)));
        }

        [Fact]
        public void 行宽不一致_非法字符或出生点数量不符时抛出异常()
        {
            Assert.Throws<ArgumentException>(() => new MazeMap(new[] { "######", "#P.G#" }));
            Assert.Throws<ArgumentException>(() => new MazeMap(new[] { "######", "#P.Gx#", "######" }));
            Assert.Throws<ArgumentException>(() => new MazeMap(new[] { "######", "#..G.#", "######" }));
            Assert.Throws<ArgumentException>(() => new MazeMap(new[] { "######", "#PPG.#", "######" }));
            Assert.Throws<ArgumentException>(() => new MazeMap(new[] { "######", "#P...#", "######" }));
        }

        [Fact]
        public void 移动不可穿墙_在墙前格点停住()
        {
            // 尾部的能量豆留在墙后口袋里，保证玩家吃光走廊豆子也不会触发过关复位。
            var rules = RulesFor(new[]
            {
                "########",
                "#P..#Go#",
                "########",
            });
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Right);
            game.Step(2f);

            Assert.Equal(3f, game.PlayerX);
            Assert.Equal(1f, game.PlayerY);
            Assert.Equal(MazeDir.None, game.PlayerDirection);
            Assert.Equal(20, game.Score);

            game.Step(0.5f);
            Assert.Equal(3f, game.PlayerX);
        }

        [Fact]
        public void 转角缓冲生效_提前按方向到格点自动转()
        {
            var rules = RulesFor(new[]
            {
                "###.##",
                "#P..#G",
                "#.###.",
                "######",
            });
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Right);
            game.Step(0.24f);
            Assert.Equal(2f, game.PlayerY);
            Assert.InRange(game.PlayerX, 2.1f, 2.3f);

            // 在 (2,2) 与 (3,2) 之间提前按上：意向被缓冲，到 (3,2) 格点才转向。
            game.SetIntent(MazeDir.Up);
            game.Step(2f);

            Assert.Equal(3f, game.PlayerX);
            Assert.Equal(3f, game.PlayerY);
            Assert.Equal(MazeDir.None, game.PlayerDirection);
            Assert.Equal(30, game.Score);
        }

        [Fact]
        public void 吃豆计分并写入事件日志()
        {
            var rules = RulesFor(new[]
            {
                "#########",
                "#P...#Go#",
                "#########",
            });
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Right);
            game.Step(1f);

            Assert.Equal(30, game.Score);
            Assert.Equal(1, game.PelletsRemaining);
            Assert.Equal(3, game.Events.Count(e => e.Type == MazeChaseEventType.AtePellet));
        }

        [Fact]
        public void 能量豆触发惊惧并在时限后结束()
        {
            var rules = RulesFor(new[]
            {
                "########",
                "#Po.#Go#",
                "########",
            }, powerDurationSeconds: 1f);
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Right);
            game.Step(0.4f);

            Assert.True(game.IsFrightened);
            Assert.True(game.FrightenedRemaining > 0.6f && game.FrightenedRemaining < 0.8f);
            Assert.Contains(MazeChaseEventType.FrightenedStarted, game.Events.Select(e => e.Type).ToList());
            Assert.Equal(60, game.Score);

            game.Step(0.8f);

            Assert.False(game.IsFrightened);
            Assert.Equal(0f, game.FrightenedRemaining);
            Assert.Contains(MazeChaseEventType.FrightenedEnded, game.Events.Select(e => e.Type).ToList());
            Assert.Equal(60, game.Score);
        }

        [Fact]
        public void 惊惧期吃幽灵得分并送回巢()
        {
            // 走廊内无豆、口袋里留豆：吃能量豆不会清空棋盘，幽灵被堵在唯一通道上只能迎面撞上玩家。
            var rules = RulesFor(new[]
            {
                "######",
                "#.##.#",
                "#Po.G#",
                "######",
            }, playerSpeed: 5f, ghostSpeed: 2f, powerDurationSeconds: 5f, respawnDelaySeconds: 0.5f);
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Right);
            for (int i = 0; i < 50 && !game.Events.Any(e => e.Type == MazeChaseEventType.GhostEaten); i++)
                game.Step(0.02f);

            var ghost = Assert.Single(game.Ghosts);
            Assert.Contains(MazeChaseEventType.GhostEaten, game.Events.Select(e => e.Type).ToList());
            // 路径上普通豆 10 + 能量豆 50 + 惊惧期吃幽灵 200 = 260
            Assert.Equal(260, game.Score);
            Assert.Equal(3, game.Lives);
            Assert.Equal(4f, ghost.X);
            Assert.Equal(1f, ghost.Y);
            Assert.Equal(ghost.House, ghost.Cell);
            Assert.True(ghost.IsRespawning);
            Assert.False(ghost.IsFrightened);
        }

        [Fact]
        public void 普通期被幽灵抓住失命回出生点()
        {
            var rules = RulesFor(new[]
            {
                "######",
                "#P..G#",
                "######",
            }, respawnDelaySeconds: 0.5f);
            var game = new MazeChaseGame(rules, seed: 1);

            for (int i = 0; i < 100 && !game.Events.Any(e => e.Type == MazeChaseEventType.PlayerCaught); i++)
                game.Step(0.02f);

            Assert.Equal(2, game.Lives);
            Assert.Equal(MazeChaseEventType.PlayerCaught, game.Events[^2].Type);
            Assert.Equal(MazeChaseEventType.LifeLost, game.Events[^1].Type);
            Assert.Equal(1f, game.PlayerX);
            Assert.Equal(1f, game.PlayerY);
            Assert.Equal(MazeDir.None, game.PlayerDirection);
            Assert.Equal(rules.Map.PlayerStart, game.PlayerCell);

            var ghost = Assert.Single(game.Ghosts);
            Assert.Equal(4f, ghost.X);
            Assert.Equal(1f, ghost.Y);
            Assert.True(ghost.IsRespawning);
        }

        [Fact]
        public void 三条生命耗尽后游戏结束且推进被忽略()
        {
            var rules = RulesFor(new[]
            {
                "######",
                "#P..G#",
                "######",
            }, respawnDelaySeconds: 0.2f);
            var game = new MazeChaseGame(rules, seed: 1);

            for (int i = 0; i < 100 && !game.IsFinished; i++) game.Step(0.1f);

            Assert.True(game.IsGameOver);
            Assert.Equal(0, game.Lives);
            Assert.Equal(3, game.Events.Count(e => e.Type == MazeChaseEventType.PlayerCaught));
            Assert.Equal(MazeChaseEventType.GameOver, game.Events[^1].Type);

            float elapsed = game.Elapsed;
            game.Step(1f);
            Assert.Equal(elapsed, game.Elapsed);
        }

        [Fact]
        public void 清空全部豆过关并提速()
        {
            // 唯一豆子在出生点左侧，右侧是无豆通道，过关复位后可向右测速而不触发再次过关。
            var rules = RulesFor(new[]
            {
                "########",
                "#.P  #G#",
                "########",
            }, respawnDelaySeconds: 0.5f);
            var game = new MazeChaseGame(rules, seed: 1);

            game.SetIntent(MazeDir.Left);
            for (int i = 0; i < 100 && game.Level == 1; i++) game.Step(0.02f);

            Assert.Equal(2, game.Level);
            Assert.Contains(MazeChaseEventType.AtePellet, game.Events.Select(e => e.Type).ToList());
            Assert.Contains(MazeChaseEventType.LevelCleared, game.Events.Select(e => e.Type).ToList());
            Assert.Equal(1, game.PelletsRemaining);
            Assert.Equal(10, game.Score);
            Assert.Equal(2f, game.PlayerX);
            Assert.Equal(rules.Map.PlayerStart, game.PlayerCell);

            // 第 2 关整体提速 10%：相同时间走得更远。
            Assert.Equal(1.1f, rules.PlayerSpeedFor(2) / rules.PlayerSpeedFor(1), 4);
            Assert.Equal(1.1f, rules.GhostSpeedFor(2) / rules.GhostSpeedFor(1), 4);
            game.SetIntent(MazeDir.Right);
            game.Step(0.1f);
            Assert.True(game.PlayerX > 2.54f && game.PlayerX < 2.56f);
        }

        [Fact]
        public void 幽灵决策_同种子同局面结果一致()
        {
            var map = new MazeMap(new[]
            {
                "#####",
                "#PG.#",
                "#####",
            });

            foreach (int seed in new[] { 0, 1, 7, 42, 999 })
            {
                // 玩家与幽灵同格：左右邻格距离并列，方向由种子决定。
                var first = MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(2, 1), isFrightened: false, new Random(seed));
                var second = MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(2, 1), isFrightened: false, new Random(seed));
                Assert.Equal(first, second);

                var frightenedFirst = MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(2, 1), isFrightened: true, new Random(seed));
                var frightenedSecond = MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(2, 1), isFrightened: true, new Random(seed));
                Assert.Equal(frightenedFirst, frightenedSecond);
            }
        }

        [Fact]
        public void 幽灵决策_贪心距离与折返兜底()
        {
            var map = new MazeMap(new[]
            {
                "#####",
                "#PG.#",
                "#####",
            });

            // 普通期选距玩家曼哈顿距离最小的邻格。
            Assert.Equal(MazeDir.Left, MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(1, 1), isFrightened: false, new Random(3)));
            // 惊惧期反向贪心，选距离最大的邻格。
            Assert.Equal(MazeDir.Right, MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(1, 1), isFrightened: true, new Random(3)));
            // 死路：无可走邻格时允许反向折返。
            Assert.Equal(MazeDir.Right, MazeChaseGame.DecideGhostDirection(map, new GridPos(1, 1), MazeDir.Left, new GridPos(1, 1), isFrightened: false, new Random(3)));

            var sealedMap = new MazeMap(new[]
            {
                "######",
                "#G#.P#",
                "######",
            });

            // 完全被围死时原地等待。
            Assert.Equal(MazeDir.None, MazeChaseGame.DecideGhostDirection(sealedMap, new GridPos(1, 1), MazeDir.Left, new GridPos(4, 1), isFrightened: false, new Random(3)));
        }

        [Fact]
        public void 幽灵决策_距离并列时由种子决定()
        {
            var map = new MazeMap(new[]
            {
                "#####",
                "#PG.#",
                "#####",
            });

            var outcomes = new HashSet<MazeDir>();
            for (int seed = 0; seed < 100; seed++)
            {
                var dir = MazeChaseGame.DecideGhostDirection(map, new GridPos(2, 1), MazeDir.None, new GridPos(2, 1), isFrightened: false, new Random(seed));
                Assert.True(dir == MazeDir.Left || dir == MazeDir.Right);
                outcomes.Add(dir);
            }

            // 两个并列方向在不同种子下都会出现，且同一种子结果稳定。
            Assert.Equal(2, outcomes.Count);
        }

        [Fact]
        public void 相同种子两次对局完全一致()
        {
            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        [Fact]
        public void 事件日志封顶128条并裁剪旧事件()
        {
            var rules = RulesFor(new[]
            {
                "######",
                "#P..G#",
                "######",
            }, lives: 100, respawnDelaySeconds: 0.1f);
            var game = new MazeChaseGame(rules, seed: 1);

            int guard = 400;
            while (!game.IsFinished && game.Events.Count < 128 && guard-- > 0) game.Step(0.5f);

            Assert.Equal(128, game.Events.Count);
            Assert.NotEqual(MazeChaseEventType.Spawned, game.Events[0].Type);
        }

        /// <summary>以种子驱动的随机意向模拟一整局，返回棋局终态摘要。</summary>
        private static long Simulate(int seed)
        {
            var game = new MazeChaseGame(MazeChaseRules.Default, seed);
            var inputs = new Random(seed * 31 + 7);
            var dirs = new[] { MazeDir.Up, MazeDir.Down, MazeDir.Left, MazeDir.Right, MazeDir.None };
            for (int i = 0; i < 600 && !game.IsFinished; i++)
            {
                game.SetIntent(dirs[inputs.Next(dirs.Length)]);
                game.Step(0.05f);
            }

            return HashCode.Combine(game.Score, game.Lives, game.Level, game.Elapsed,
                game.PlayerX, game.PlayerY, game.PelletsRemaining, string.Join(';', game.Events));
        }
    }
}
