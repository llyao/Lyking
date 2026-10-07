using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.BattleCity.Core;
using Xunit;

namespace Lyking.BattleCity.Core.Tests
{
    /// <summary>坦克大战核心规则测试：移动阻挡、射击冷却、墙体摧毁、命中计分、对消、失命无敌、基地被毁、生成与 AI 确定性、过关提速、地图合法性与事件封顶。</summary>
    public class BattleCityGameTests
    {
        /// <summary>禁用敌人开火用的超长冷却：测试时间窗内敌人不会射击。</summary>
        private const float NoFire = 1_000_000f;

        private static readonly string[] MovementMap =
        {
            // 玩家出生格上方有一道钢墙：向上移动被挡，向右被基地格挡。
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "....###......",
            ".............",
            ".....P......E",
        };

        private static readonly string[] BrickSteelMap =
        {
            // 玩家出生格正上方依次是两块砖墙与一块钢墙。
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".....#.......",
            ".....B.......",
            ".....B.......",
            ".....P......E",
        };

        private static readonly string[] LaneMap =
        {
            // 空旷直线地图：基地在左下角，玩家出生在中下，便于布置迎面对决。
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "E.....P......",
        };

        private static readonly string[] BaseMap =
        {
            // 玩家与基地同排，中间隔一块砖墙：两炮即可击穿并命中基地。
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "..P..BE......",
        };

        private static readonly string[] FatMap =
        {
            // 空旷地图加一块远离玩家弹道列的砖墙：配合超大子弹半径全歼敌人并验证过关重置。
            ".............",
            ".............",
            ".............",
            "B............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "E.....P......",
        };

        private static readonly string[] SteelRingMap =
        {
            // 基地被钢墙完全包围：地图不合法，玩家出生点无法规划到基地邻格。
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".....###.....",
            "..P..#E#.....",
        };

        private static readonly GridPos[] SpawnCells =
        {
            new GridPos(0, 12),
            new GridPos(6, 12),
            new GridPos(12, 12),
        };

        /// <summary>以默认规则为基底、按测试需要覆盖部分参数构建规则表。</summary>
        private static BattleCityRules Rules(
            string[] map,
            float? playerSpeed = null,
            float? enemySpeed = null,
            float? bulletSpeed = null,
            float? bulletRadius = null,
            float? playerFireCooldown = null,
            float? enemyFireCooldown = null,
            int? enemiesPerLevel = null,
            float? spawnInterval = null,
            int? maxLives = null,
            float? invincibleSeconds = null,
            int? scorePerKill = null,
            float? levelSpeedGain = null)
        {
            var d = BattleCityRules.Default;
            return new BattleCityRules(
                map,
                playerSpeed ?? d.PlayerSpeed,
                enemySpeed ?? d.EnemyBaseSpeed,
                bulletSpeed ?? d.BulletSpeed,
                bulletRadius ?? d.BulletRadius,
                d.TankHalfSize,
                playerFireCooldown ?? d.PlayerFireCooldownSeconds,
                enemyFireCooldown ?? d.EnemyFireCooldownSeconds,
                enemiesPerLevel ?? d.EnemiesPerLevel,
                spawnInterval ?? d.SpawnIntervalSeconds,
                maxLives ?? d.MaxLives,
                invincibleSeconds ?? d.RespawnInvincibleSeconds,
                scorePerKill ?? d.ScorePerKill,
                levelSpeedGain ?? d.LevelSpeedGain);
        }

        /// <summary>确定性搜索一个首位敌人出生在指定槽位的种子，保证测试布景稳定。</summary>
        private static int SeedWithSpawnSlot(int slot)
        {
            for (int seed = 0; seed < 1024; seed++)
            {
                if (new Random(seed).Next(3) == slot) return seed;
            }

            throw new InvalidOperationException("未找到合适种子。");
        }

        private static GridPos CellFloor(Vec2 position)
            => new GridPos((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));

        private static List<BattleCityEventType> Types(BattleCityGame game) => game.Events.Select(e => e.Type).ToList();

        [Fact]
        public void 构造后初始状态合法()
        {
            var game = new BattleCityGame(BattleCityRules.Default, seed: 42);

            Assert.Equal(new Vec2(4.5f, 0.5f), game.Player.Position);
            Assert.Equal(TankDir.Up, game.Player.Facing);
            Assert.Equal(3, game.Lives);
            Assert.Equal(1, game.Level);
            Assert.Equal(0, game.Score);
            Assert.Equal(0f, game.Elapsed, 4);
            Assert.False(game.IsFinished);
            Assert.Equal(6, game.EnemiesRemaining);
            Assert.Empty(game.Bullets);
            Assert.Single(game.Enemies);
            Assert.Equal(BattleCityEventType.Spawned, game.Events[0].Type);
            Assert.Equal(BattleCityEventType.EnemySpawned, game.Events[1].Type);

            // 内置地图解析：基地、护基地砖墙、钢墙与玩家出生格
            Assert.Equal(new GridPos(4, 0), game.Map.PlayerSpawn);
            Assert.Equal(new GridPos(6, 0), game.Map.BaseCell);
            Assert.Equal(BattleCityCell.Base, game.Map.CellAt(new GridPos(6, 0)));
            Assert.Equal(BattleCityCell.Brick, game.Map.CellAt(new GridPos(5, 0)));
            Assert.Equal(BattleCityCell.Steel, game.Map.CellAt(new GridPos(4, 6)));
            Assert.Equal(BattleCityCell.Empty, game.Map.CellAt(new GridPos(4, 0)));
        }

        [Fact]
        public void 玩家四向移动并撞墙停()
        {
            var rules = Rules(MovementMap, enemySpeed: 0f, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, seed: 3);

            // 上行被钢墙挡住：贴在钢墙下缘，纵坐标停在格心
            game.Step(2f, new TankInput(TankDir.Up, false));
            Assert.Equal(TankDir.Up, game.Player.Facing);
            Assert.Equal(1.5f, game.Player.Position.Y, 3);
            Assert.Equal(5.5f, game.Player.Position.X, 3);

            // 左行贴到场左缘
            game.Step(2f, new TankInput(TankDir.Left, false));
            Assert.Equal(TankDir.Left, game.Player.Facing);
            Assert.Equal(0.5f, game.Player.Position.X, 3);

            // 下行贴到场下缘
            game.Step(1f, new TankInput(TankDir.Down, false));
            Assert.Equal(TankDir.Down, game.Player.Facing);
            Assert.Equal(0.5f, game.Player.Position.Y, 3);

            // 右行被基地格挡住：停在基地西邻（11 格路程需约 3.4 秒）
            game.Step(4f, new TankInput(TankDir.Right, false));
            Assert.Equal(TankDir.Right, game.Player.Facing);
            Assert.Equal(11.5f, game.Player.Position.X, 3);
        }

        [Fact]
        public void 射击沿出膛且冷却内被拒绝()
        {
            var rules = Rules(LaneMap, enemySpeed: 0f, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, seed: 3);

            game.Step(0f, new TankInput(TankDir.None, true));
            var bullet = Assert.Single(game.Bullets);
            // 炮口在玩家朝向上：中心距离 0.5 + 0.12，纵坐标 1.12
            Assert.Equal(6.5f, bullet.Position.X, 3);
            Assert.Equal(1.12f, bullet.Position.Y, 3);
            Assert.Equal(TankDir.Up, bullet.Direction);
            Assert.Equal(BattleCityEventType.Shot, game.Events[2].Type);

            // 冷却 0.5 秒：0.2、0.4、0.6 秒时刻的按下沿都在帧首被拒，冷却走完后放行
            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.Single(game.Bullets);
            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.Single(game.Bullets);
            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.Single(game.Bullets);
            game.Step(0.1f, new TankInput(TankDir.None, true));
            Assert.Equal(2, game.Bullets.Count);
        }

        [Fact]
        public void 砖墙可被摧毁而钢墙不可()
        {
            var rules = Rules(BrickSteelMap, playerFireCooldown: 0.2f, enemySpeed: 0f, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, seed: 3);

            // 三连发：前两发摧毁两块砖墙，第三发被钢墙挡下
            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, default);

            Assert.Equal(BattleCityCell.Empty, game.Map.CellAt(new GridPos(5, 1)));
            Assert.Equal(BattleCityCell.Empty, game.Map.CellAt(new GridPos(5, 2)));
            Assert.Equal(BattleCityCell.Steel, game.Map.CellAt(new GridPos(5, 3)));
            Assert.Equal(2, game.Events.Count(e => e.Type == BattleCityEventType.BrickDestroyed));
            Assert.Empty(game.Bullets);
            Assert.False(game.Map.TryDestroyBrick(new GridPos(5, 3)));
        }

        [Fact]
        public void 玩家子弹命中敌人消灭计分()
        {
            var rules = Rules(LaneMap, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(1));

            // 玩家在中列向上射击，敌人从顶中出生格迎面驶来，约 1.25 秒相遇
            game.Step(0f, new TankInput(TankDir.None, true));
            Assert.Single(game.Bullets);
            game.Step(1.5f, default);

            Assert.Empty(game.Enemies);
            Assert.Empty(game.Bullets);
            Assert.Equal(rules.ScorePerKill, game.Score);
            Assert.Equal(rules.EnemiesPerLevel - 1, game.EnemiesRemaining);
            Assert.Contains(BattleCityEventType.EnemyKilled, Types(game));
        }

        [Fact]
        public void 敌我子弹迎面对消()
        {
            var rules = Rules(LaneMap, enemyFireCooldown: 0.1f);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(1));

            game.Step(0f, new TankInput(TankDir.None, true));
            game.Step(1.2f, default);

            Assert.Contains(BattleCityEventType.BulletClashed, Types(game));
            // 对消后敌人仍按冷却持续开火：场上不应再有任何玩家子弹
            Assert.DoesNotContain(game.Bullets, bullet => bullet.FromPlayer);
            Assert.Single(game.Enemies);
            Assert.Equal(0, game.Score);
            Assert.Equal(rules.MaxLives, game.Lives);
        }

        [Fact]
        public void 敌人子弹命中玩家失命重生并获得无敌期()
        {
            // 超大敌方子弹半径覆盖全场列：任何出生槽位的敌人都能命中玩家
            var rules = Rules(LaneMap, bulletRadius: 6.4f, enemyFireCooldown: 0.05f, enemySpeed: 0f, spawnInterval: 100f);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(1));

            game.Step(0.5f, default);

            Assert.Equal(rules.MaxLives - 1, game.Lives);
            Assert.Equal(new Vec2(6.5f, 0.5f), game.Player.Position);
            Assert.Equal(TankDir.Up, game.Player.Facing);
            Assert.True(game.InvincibleRemaining > 1.5f, $"无敌期应剩余 1.5 秒以上，实际 {game.InvincibleRemaining}");
            Assert.Contains(BattleCityEventType.PlayerHit, Types(game));
            Assert.Contains(BattleCityEventType.LifeLost, Types(game));

            // 无敌期内持续的敌方火力全部无效
            game.Step(1.0f, default);
            Assert.Equal(rules.MaxLives - 1, game.Lives);
        }

        [Fact]
        public void 无敌期结束后敌人子弹可再次命中()
        {
            var rules = Rules(LaneMap, bulletRadius: 6.4f, enemyFireCooldown: 0.05f, enemySpeed: 0f, spawnInterval: 100f);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(1));

            game.Step(0.5f, default);
            game.Step(2.5f, default);

            Assert.Equal(rules.MaxLives - 2, game.Lives);
            Assert.Equal(2, game.Events.Count(e => e.Type == BattleCityEventType.PlayerHit));
            Assert.Equal(2, game.Events.Count(e => e.Type == BattleCityEventType.LifeLost));
            Assert.True(game.InvincibleRemaining > 0f);
        }

        [Fact]
        public void 玩家子弹摧毁基地即失败且推进冻结()
        {
            var rules = Rules(BaseMap, playerFireCooldown: 0.2f, enemySpeed: 0f, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, seed: 3);

            // 先转向右，再两连发：第一发摧毁护基地砖墙，第二发命中基地
            game.Step(0.1f, new TankInput(TankDir.Right, false));
            game.Step(0.5f, new TankInput(TankDir.None, true));
            Assert.False(game.IsFinished);
            Assert.Equal(BattleCityCell.Empty, game.Map.CellAt(new GridPos(5, 0)));

            game.Step(0.5f, new TankInput(TankDir.None, true));
            Assert.True(game.IsGameOver);
            Assert.Contains(BattleCityEventType.BaseHit, Types(game));
            Assert.Contains(BattleCityEventType.GameOver, Types(game));
            Assert.Equal(rules.MaxLives, game.Lives);

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new TankInput(TankDir.Up, true));
            Assert.Equal(elapsed, game.Elapsed, 4);
            Assert.Equal(score, game.Score);
            Assert.Empty(game.Bullets);
        }

        [Fact]
        public void 坦克互撞阻挡前进()
        {
            var rules = Rules(LaneMap, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(1));

            // 玩家向上迎面顶入敌人：相撞后双方都被挡停
            game.Step(3f, new TankInput(TankDir.Up, false));
            Assert.InRange(game.Player.Position.Y, 5f, 9f);

            float yBlocked = game.Player.Position.Y;
            float enemyY = game.Enemies[0].Position.Y;
            game.Step(1f, new TankInput(TankDir.Up, false));
            Assert.Equal(yBlocked, game.Player.Position.Y);
            Assert.Equal(enemyY, game.Enemies[0].Position.Y);
        }

        [Fact]
        public void 敌人按固定种子轮换生成()
        {
            var rules = Rules(LaneMap, spawnInterval: 0.3f, enemySpeed: 0.5f, enemyFireCooldown: NoFire);
            var game = new BattleCityGame(rules, seed: 5);
            Assert.Single(game.Enemies);

            // 0.3 秒与 0.6 秒各生成一名，前三名占据互不相同的顶部出生格
            game.Step(0.65f, default);
            Assert.Equal(3, game.Enemies.Count);
            var cells = game.Enemies.Select(e => CellFloor(e.Position)).ToList();
            Assert.Equal(3, cells.Distinct().Count());
            Assert.All(cells, cell => Assert.Contains(cell, SpawnCells));

            int guard = 0;
            while (game.Enemies.Count < 6 && guard < 80)
            {
                game.Step(0.25f, default);
                guard++;
            }

            Assert.Equal(6, game.Enemies.Count);
            Assert.Equal(6, game.Events.Count(e => e.Type == BattleCityEventType.EnemySpawned));
        }

        [Fact]
        public void 敌人AI转向决策纯函数确定()
        {
            var cell = new GridPos(6, 6);

            // 主轴朝向目标且开放：直接选择
            Assert.Equal(TankDir.Down, BattleCityGame.DecideEnemyDirection(
                cell, new GridPos(6, 0), TankDir.Up, true, true, true, true, random01: 0.9));

            // 主轴被阻挡：改走次轴
            Assert.Equal(TankDir.Left, BattleCityGame.DecideEnemyDirection(
                cell, new GridPos(0, 6), TankDir.Up, true, false, true, true, random01: 0.3));

            // 两轴距离相等：random01 平局决定先试哪个朝向
            var target = new GridPos(9, 3);
            Assert.Equal(TankDir.Down, BattleCityGame.DecideEnemyDirection(
                cell, target, TankDir.Up, true, true, true, true, random01: 0.2));
            Assert.Equal(TankDir.Right, BattleCityGame.DecideEnemyDirection(
                cell, target, TankDir.Up, true, true, true, true, random01: 0.8));

            // 朝向目标的方向都被阻挡：按固定顺序与随机序号在开放方向中挑选
            Assert.Equal(TankDir.Up, BattleCityGame.DecideEnemyDirection(
                cell, target, TankDir.Up, true, false, true, false, random01: 0.2));
            Assert.Equal(TankDir.Left, BattleCityGame.DecideEnemyDirection(
                cell, target, TankDir.Up, true, false, true, false, random01: 0.7));

            // 全部阻挡：保持原方向
            Assert.Equal(TankDir.Up, BattleCityGame.DecideEnemyDirection(
                cell, target, TankDir.Up, false, false, false, false, random01: 0.5));

            // 同输入同输出：纯函数无内部状态
            Assert.Equal(
                BattleCityGame.DecideEnemyDirection(cell, target, TankDir.Left, true, true, true, true, 0.42),
                BattleCityGame.DecideEnemyDirection(cell, target, TankDir.Left, true, true, true, true, 0.42));
        }

        [Fact]
        public void 全歼敌人过关重置砖墙并提速()
        {
            var rules = Rules(FatMap, bulletRadius: 6.4f, playerFireCooldown: 0.2f, enemyFireCooldown: NoFire, enemySpeed: 0f, spawnInterval: 0.3f);
            var game = new BattleCityGame(rules, seed: 3);

            // 先摧毁远离玩家弹道列的砖墙，过关重置后应被恢复
            Assert.True(game.Map.TryDestroyBrick(new GridPos(0, 9)));
            Assert.Equal(BattleCityCell.Empty, game.Map.CellAt(new GridPos(0, 9)));

            int guard = 0;
            while (game.Level < 2 && guard < 400)
            {
                game.Step(0.25f, new TankInput(TankDir.None, true));
                guard++;
            }

            Assert.Equal(2, game.Level);
            Assert.Contains(BattleCityEventType.LevelCleared, Types(game));
            Assert.Equal(rules.EnemiesPerLevel, game.EnemiesRemaining);
            Assert.Equal(BattleCityCell.Brick, game.Map.CellAt(new GridPos(0, 9)));
            Assert.Equal(rules.EnemyBaseSpeed * 1.1f, rules.EnemySpeedFor(2), 3);
        }

        [Fact]
        public void 地图合法性玩家出生点可达基地邻格()
        {
            Assert.True(new BattleCityMap(BattleCityRules.Default.MapRows).IsPlayerSpawnConnectedToBase());
            Assert.False(new BattleCityMap(SteelRingMap).IsPlayerSpawnConnectedToBase());

            // 解析校验：非法字符、缺少基地、重复出生格都应拒绝
            Assert.Throws<ArgumentException>(() => new BattleCityMap(new[] { "..X..E" }));
            Assert.Throws<ArgumentException>(() => new BattleCityMap(new[] { "....", "..P." }));
            Assert.Throws<ArgumentException>(() => new BattleCityMap(new[] { "..P..E", "...P.E" }));
        }

        [Fact]
        public void 相同种子与输入的对局完全一致()
        {
            static string Simulate(int seed)
            {
                var game = new BattleCityGame(BattleCityRules.Default, seed);
                for (int i = 0; i < 16; i++)
                {
                    var dir = (i % 4) switch { 0 => TankDir.Up, 1 => TankDir.Right, 2 => TankDir.Down, _ => TankDir.Left };
                    game.Step(0.25f, new TankInput(dir, i % 3 == 0));
                }

                return string.Join(';', game.Score, game.Lives, game.Level, game.Elapsed, game.Player,
                    string.Join("|", game.Bullets), string.Join("|", game.Enemies),
                    game.Events.Count, game.Events[^1].Type);
            }

            int seedA = SeedWithSpawnSlot(0);
            int seedB = SeedWithSpawnSlot(1);
            Assert.NotEqual(seedA, seedB);
            Assert.Equal(Simulate(seedA), Simulate(seedA));
            Assert.NotEqual(Simulate(seedA), Simulate(seedB));
        }

        [Fact]
        public void 事件日志封顶裁剪()
        {
            // 三名静止敌人以 0.02 秒冷却持续开火：1.8 秒内事件数远超上限
            var rules = Rules(LaneMap, spawnInterval: 0.3f, enemySpeed: 0f, enemyFireCooldown: 0.02f);
            var game = new BattleCityGame(rules, SeedWithSpawnSlot(2));

            game.Step(1.8f, default);

            Assert.False(game.IsFinished);
            Assert.Equal(rules.MaxLives, game.Lives);
            Assert.Equal(128, game.Events.Count);
            Assert.DoesNotContain(BattleCityEventType.Spawned, Types(game));
            Assert.Contains(BattleCityEventType.EnemyShot, Types(game));
        }
    }
}
