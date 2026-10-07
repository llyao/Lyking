using System.Linq;
using Lyking.Bomberman.Core;
using Xunit;

namespace Lyking.Bomberman.Core.Tests
{
    /// <summary>
    /// 炸弹人核心规则测试：地形阻挡、炸弹引信与阻挡、十字火焰、摧墙、击杀、
    /// 自爆失命、连锁引爆、出口开启与过关、种子手算与确定性。
    /// 固定使用 seed=3 的 7x7 小棋盘，其布局已人工核算：
    /// <code>
    /// y=6 #######
    /// y=5 #.ss.s#
    /// y=4 #.#.#s#
    /// y=3 #.s.s.#
    /// y=2 #.#E#.#     E = 出口 (3,2)，藏在软墙下
    /// y=1 #P..G.#     P = 出生 (1,1)，G = 敌人 (4,1)
    /// y=0 #######
    /// </code>
    /// </summary>
    public class BombermanGameTests
    {
        /// <summary>7x7 小棋盘规则：单名慢速敌人（0.01 格/秒，测试期间视为驻留原地）与 0.4 软墙密度。</summary>
        private static readonly BombermanRules Small = new BombermanRules(
            width: 7, height: 7, enemyCount: 1, enemySpeed: 0.01f, softWallDensity: 0.4);

        /// <summary>无敌变体：地形与 Small 完全一致（软墙分布先于敌人抽取），便于自由走位。</summary>
        private static readonly BombermanRules SmallNoEnemy = new BombermanRules(
            width: 7, height: 7, enemyCount: 0, softWallDensity: 0.4);

        /// <summary>双弹变体：允许同时两颗炸弹，用于连锁引爆。</summary>
        private static readonly BombermanRules SmallChain = new BombermanRules(
            width: 7, height: 7, maxBombs: 2, enemyCount: 0, softWallDensity: 0.4);

        [Fact]
        public void 构造后第一关初始状态合法()
        {
            var game = new BombermanGame(3, Small);

            Assert.Equal(1, game.Level);
            Assert.Equal(3, game.Seed);
            Assert.Equal(7, game.Width);
            Assert.Equal(7, game.Height);
            Assert.Equal(new GridPos(1, 1), game.Player);
            Assert.Equal(new GridPos(3, 2), game.Exit);
            Assert.False(game.IsExitRevealed);
            Assert.False(game.IsExitOpen);
            Assert.False(game.IsGameOver);
            Assert.Equal(0, game.Score);
            Assert.Equal(3, game.Lives);
            Assert.Equal(0f, game.Elapsed);
            Assert.Single(game.Enemies);
            Assert.Empty(game.Bombs);
            Assert.Empty(game.Flames);
            Assert.Equal(BombermanEventType.LevelStarted, game.Events[^1].Type);
            Assert.Equal(BombermanTile.HardWall, game.TileAt(new GridPos(0, 1)));
            Assert.Equal(BombermanTile.SoftWall, game.TileAt(new GridPos(3, 2)));
            Assert.Equal(BombermanTile.Floor, game.TileAt(new GridPos(2, 1)));
            // 网格之外一律视为硬墙。
            Assert.Equal(BombermanTile.HardWall, game.TileAt(new GridPos(-1, 1)));
            Assert.Equal(BombermanTile.HardWall, game.TileAt(new GridPos(1, 7)));
        }

        [Fact]
        public void 移动不可穿硬墙()
        {
            var game = new BombermanGame(3, Small);

            // 出生点左侧与下侧均为边框硬墙。
            game.Step(0.25f, new BombInput(BombermanDir.Left, false));
            game.Step(0.25f, new BombInput(BombermanDir.Down, false));
            Assert.Equal(new GridPos(1, 1), game.Player);
            Assert.Equal(BombermanEventType.MoveRejected, game.Events[^1].Type);

            // 内部柱状硬墙：从 (1,2) 向右是 (2,2) 柱。
            game.Step(0.25f, new BombInput(BombermanDir.Up, false));
            Assert.Equal(new GridPos(1, 2), game.Player);
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));
            Assert.Equal(new GridPos(1, 2), game.Player);
            Assert.Equal(BombermanEventType.MoveRejected, game.Events[^1].Type);
        }

        [Fact]
        public void 移动不可穿软墙()
        {
            var game = new BombermanGame(3, Small);

            // 沿左列走到 (1,3)，右侧 (2,3) 是软墙。
            game.Step(0.25f, new BombInput(BombermanDir.Up, false));
            game.Step(0.25f, new BombInput(BombermanDir.Up, false));
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));

            Assert.Equal(new GridPos(1, 3), game.Player);
            Assert.Equal(BombermanEventType.MoveRejected, game.Events[^1].Type);
            Assert.Equal(BombermanTile.SoftWall, game.TileAt(new GridPos(2, 3)));
        }

        [Fact]
        public void 炸弹阻挡通行但玩家可从其上走离()
        {
            var game = new BombermanGame(3, Small);

            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            Assert.True(game.IsBombAt(new GridPos(1, 1)));

            // 玩家可以从自己脚下的炸弹格走离。
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));
            Assert.Equal(new GridPos(2, 1), game.Player);

            // 但走不回仍被引信占据的格子。
            game.Step(0.25f, new BombInput(BombermanDir.Left, false));
            Assert.Equal(new GridPos(2, 1), game.Player);
            Assert.Equal(BombermanEventType.MoveRejected, game.Events[^1].Type);
            Assert.Single(game.Bombs);
        }

        [Fact]
        public void 炸弹引信两秒半整点爆炸()
        {
            var game = new BombermanGame(3, Small);

            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            Assert.Single(game.Bombs);

            // 累计推进 2.25 秒时引信未到，炸弹仍在倒计时。
            for (int i = 0; i < 8; i++) game.Step(0.25f, default);
            Assert.Single(game.Bombs);
            Assert.InRange(game.Bombs[0].FuseRemaining, 0.24f, 0.26f);

            // 累计 2.5 秒即爆炸，火焰原地燃烧。
            game.Step(0.25f, default);
            Assert.Empty(game.Bombs);
            Assert.Contains(game.Events, e => e.Type == BombermanEventType.Explosion);
            Assert.True(game.IsFlameAt(new GridPos(1, 1)));
        }

        [Fact]
        public void 爆炸产生十字火焰两格范围()
        {
            var game = new BombermanGame(3, Small);

            // 按 0.25 秒分帧推进到引信到期：爆炸后火焰本帧剩余 0.25 秒可被观测。
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            for (int i = 0; i < 9; i++) game.Step(0.25f, default);
            Assert.Empty(game.Bombs);

            // 中心与右、上各 2 格有火焰。
            Assert.True(game.IsFlameAt(new GridPos(1, 1)));
            Assert.True(game.IsFlameAt(new GridPos(2, 1)));
            Assert.True(game.IsFlameAt(new GridPos(3, 1)));
            Assert.True(game.IsFlameAt(new GridPos(1, 2)));
            Assert.True(game.IsFlameAt(new GridPos(1, 3)));
            Assert.Equal(5, game.Flames.Count);

            // 左、下被边框硬墙截断，超出 2 格范围没有火焰。
            Assert.False(game.IsFlameAt(new GridPos(0, 1)));
            Assert.False(game.IsFlameAt(new GridPos(1, 0)));
            Assert.False(game.IsFlameAt(new GridPos(4, 1)));

            // 火焰自爆炸起燃烧 0.5 秒，再过一帧即熄灭。
            game.Step(0.25f, default);
            Assert.Empty(game.Flames);
        }

        [Fact]
        public void 火焰摧毁软墙并计分()
        {
            var game = new BombermanGame(3, SmallNoEnemy);

            // 走到 (5,3) 放弹：十字向左与向上各烧掉一面软墙 ((4,3) 与 (5,4))。
            foreach (var dir in new[] { BombermanDir.Right, BombermanDir.Right, BombermanDir.Right, BombermanDir.Right, BombermanDir.Up, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            Assert.Equal(new GridPos(5, 3), game.Player);
            game.Step(0.25f, new BombInput(BombermanDir.None, true));

            // 撤到 (4,1) 躲开火焰十字，等待爆炸。
            foreach (var dir in new[] { BombermanDir.Down, BombermanDir.Down, BombermanDir.Left })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(3f, default);

            Assert.Equal(BombermanTile.Floor, game.TileAt(new GridPos(4, 3)));
            Assert.Equal(BombermanTile.Floor, game.TileAt(new GridPos(5, 4)));
            Assert.Equal(20, game.Score);
            Assert.Equal(2, game.Events.Count(e => e.Type == BombermanEventType.WallDestroyed));
            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void 火焰炸死敌人并计分()
        {
            var game = new BombermanGame(3, Small);

            // 走到 (2,1) 放弹：十字向右 2 格覆盖敌人驻留格 (4,1)。
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));
            game.Step(0.25f, new BombInput(BombermanDir.None, true));

            // 撤到 (1,3) 躲进火焰十字之外，等待爆炸。
            foreach (var dir in new[] { BombermanDir.Left, BombermanDir.Up, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(3f, default);

            Assert.Empty(game.Enemies);
            Assert.Equal(100, game.Score);
            Assert.Contains(game.Events, e => e.Type == BombermanEventType.EnemyKilled);
            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void 火焰烧到自己失命且三命尽失败()
        {
            var game = new BombermanGame(3, Small);

            // 原地放弹不撤离，爆炸烧到自己：失一条命并回到出生点。
            for (int i = 0; i < 2; i++)
            {
                game.Step(0.25f, new BombInput(BombermanDir.None, true));
                game.Step(1.25f, default);
                game.Step(1.25f, default);
            }

            Assert.Equal(1, game.Lives);
            Assert.Equal(new GridPos(1, 1), game.Player);
            Assert.Contains(game.Events, e => e.Type == BombermanEventType.PlayerBurned);
            Assert.Contains(game.Events, e => e.Type == BombermanEventType.LifeLost);
            Assert.False(game.IsGameOver);

            // 第三次自爆耗尽生命，游戏结束，后续推进被忽略。
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            game.Step(1.25f, default);
            game.Step(1.25f, default);
            Assert.Equal(0, game.Lives);
            Assert.True(game.IsGameOver);
            Assert.Contains(game.Events, e => e.Type == BombermanEventType.GameOver);
            float elapsed = game.Elapsed;
            game.Step(1f, default);
            Assert.Equal(elapsed, game.Elapsed);
        }

        [Fact]
        public void 火焰引燃其他炸弹立即连锁()
        {
            var game = new BombermanGame(3, SmallChain);

            // (1,1) 放第一颗，走到 (2,1) 放第二颗，随后走到 (5,1) 躲开两次火焰十字。
            game.Step(0.5f, new BombInput(BombermanDir.None, true));
            game.Step(0.5f, new BombInput(BombermanDir.Right, false));
            game.Step(0.5f, new BombInput(BombermanDir.None, true));
            foreach (var dir in new[] { BombermanDir.Right, BombermanDir.Right, BombermanDir.Right })
                game.Step(0.25f, new BombInput(dir, false));
            Assert.Equal(2, game.Bombs.Count);

            // 第一颗引信到期时第二颗仍在燃烧，被十字火焰引爆，两声爆炸同帧发生。
            game.Step(1f, default);
            Assert.Empty(game.Bombs);
            Assert.Equal(2, game.Events.Count(e => e.Type == BombermanEventType.Explosion));
            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void 敌人全灭且出口显露后出口才打开()
        {
            var game = new BombermanGame(3, Small);

            // 第一颗弹在 (2,1)：十字向右烧死敌人 (4,1)；出口未露，出口仍未打开。
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            foreach (var dir in new[] { BombermanDir.Left, BombermanDir.Up, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(2.5f, default);
            Assert.Empty(game.Enemies);
            Assert.False(game.IsExitRevealed);
            Assert.False(game.IsExitOpen);

            // 第二颗弹在 (3,1)：十字向上烧开出口软墙 (3,2)；此时敌人已灭，出口打开。
            foreach (var dir in new[] { BombermanDir.Down, BombermanDir.Down, BombermanDir.Right, BombermanDir.Right })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            foreach (var dir in new[] { BombermanDir.Left, BombermanDir.Left, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(3f, default);

            Assert.Equal(110, game.Score);
            Assert.True(game.IsExitRevealed);
            Assert.True(game.IsExitOpen);
        }

        [Fact]
        public void 走上打开的出口过关进入下一关()
        {
            var game = CreateOpenExitGame();

            // 从 (1,2) 走回出生走廊再向上踏上出口格 (3,2)。
            foreach (var dir in new[] { BombermanDir.Down, BombermanDir.Right, BombermanDir.Right, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));

            Assert.Contains(game.Events, e => e.Type == BombermanEventType.LevelCleared);
            Assert.Equal(2, game.Level);
            Assert.Equal(new GridPos(1, 1), game.Player);
            Assert.Equal(110, game.Score);
            Assert.Equal(3, game.Lives);
            Assert.Empty(game.Bombs);
            Assert.Empty(game.Flames);
            Assert.False(game.IsExitRevealed);
            Assert.Equal(BombermanEventType.LevelStarted, game.Events[^1].Type);
        }

        [Fact]
        public void 软墙分布由固定种子手算确定()
        {
            // 7x7 小棋盘候选空格 18 个，密度 0.4 抽 7 面软墙；期望序列由生成算法的
            // 固定抽样顺序（行序候选 + 部分洗牌不放回抽取）手算得出。
            var level = BombermanLevel.Generate(1, 3, Small);
            var expected = new[]
            {
                new GridPos(5, 5), new GridPos(2, 3), new GridPos(3, 2), new GridPos(3, 5),
                new GridPos(4, 3), new GridPos(5, 4), new GridPos(2, 5),
            };
            Assert.Equal(7, level.SoftWalls.Count);
            Assert.Equal(expected, level.SoftWalls);
            Assert.Equal(new GridPos(3, 2), level.Exit);
            Assert.Equal(new GridPos(1, 1), level.PlayerSpawn);

            // 默认 13x11 规则 seed=7：候选 76 格抽 45 面，首个软墙与手算一致。
            var standard = BombermanLevel.Generate(1, 7, BombermanRules.Default);
            Assert.Equal(45, standard.SoftWalls.Count);
            Assert.Equal(new GridPos(3, 1), standard.SoftWalls[0]);
        }

        [Fact]
        public void 相同种子与输入序列的对局完全一致()
        {
            Assert.Equal(Simulate(), Simulate());

            static int Simulate()
            {
                var game = new BombermanGame(7, BombermanRules.Default);
                var dirs = new[] { BombermanDir.Up, BombermanDir.Right, BombermanDir.Right, BombermanDir.Down, BombermanDir.None };
                for (int frame = 0; frame < 60; frame++)
                {
                    var input = new BombInput(dirs[frame % dirs.Length], frame % 11 == 0);
                    game.Step(0.1f, input);
                }

                return HashCode.Combine(
                    HashCode.Combine(game.Score, game.Lives, game.Level, game.Player, game.IsExitRevealed,
                        game.Events.Count, game.Bombs.Count, game.Elapsed),
                    string.Join(';', game.Enemies.Select(e => $"{e.Position}:{e.Direction}:{e.Progress}")));
            }
        }

        /// <summary>
        /// 在 seed=3 小棋盘上连放两颗弹：先烧死敌人，再烧开出口软墙，
        /// 返回出口已打开（敌人全灭）且玩家停在 (1,2) 的战局。
        /// </summary>
        private static BombermanGame CreateOpenExitGame()
        {
            var game = new BombermanGame(3, Small);

            // 第一颗弹在 (2,1)：十字向右烧死敌人 (4,1)，玩家撤到 (1,3) 躲开。
            game.Step(0.25f, new BombInput(BombermanDir.Right, false));
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            foreach (var dir in new[] { BombermanDir.Left, BombermanDir.Up, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(2.5f, default);

            // 第二颗弹在 (3,1)：十字向上烧开出口软墙 (3,2)，玩家撤到 (1,2) 躲开。
            foreach (var dir in new[] { BombermanDir.Down, BombermanDir.Down, BombermanDir.Right, BombermanDir.Right })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            foreach (var dir in new[] { BombermanDir.Left, BombermanDir.Left, BombermanDir.Up })
                game.Step(0.25f, new BombInput(dir, false));
            game.Step(3f, default);

            Assert.Equal(110, game.Score);
            Assert.True(game.IsExitRevealed);
            Assert.True(game.IsExitOpen);
            return game;
        }
    }
}
