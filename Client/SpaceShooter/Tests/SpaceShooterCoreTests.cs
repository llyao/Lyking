using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.SpaceShooter.Core;
using Xunit;

namespace Lyking.SpaceShooter.Core.Tests
{
    /// <summary>太空射击核心规则测试：确定性、射击冷却、命中判定、敌人轨迹、逃逸、受击、升波与事件封顶。</summary>
    public class SpaceShooterGameTests
    {
        /// <summary>逃逸测试用窄玩家规则：敌人落底前不会碰到靠边躲避的玩家。</summary>
        private static ShooterRules EscapeRules() => new ShooterRules(
            width: 8f, height: 10f,
            playerHalfWidth: 0.5f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 0.15f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 100f, spawnMinInterval: 0.7f, spawnShrinkPerWave: 0.15f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        /// <summary>受击测试用小场地规则：宽 4 场内任何敌人出生点都必然撞上中心玩家。</summary>
        private static ShooterRules CollisionRules() => new ShooterRules(
            width: 4f, height: 10f,
            playerHalfWidth: 1f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 0.15f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 100f, spawnMinInterval: 0.7f, spawnShrinkPerWave: 0.15f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        /// <summary>游戏结束测试规则：在受击规则上把生成间隔缩短到 0.5 秒，敌人持续补位。</summary>
        private static ShooterRules GameOverRules() => new ShooterRules(
            width: 4f, height: 10f,
            playerHalfWidth: 1f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 0.15f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 0.5f, spawnMinInterval: 0.5f, spawnShrinkPerWave: 0.05f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        /// <summary>命中测试用宽子弹规则：子弹横坐标带覆盖宽 8 全场，任何子弹都必然与任何敌人横向相交。</summary>
        private static ShooterRules FatRules() => new ShooterRules(
            width: 8f, height: 10f,
            playerHalfWidth: 0.5f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 3.5f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 100f, spawnMinInterval: 0.7f, spawnShrinkPerWave: 0.15f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        /// <summary>升波测试规则：在宽子弹规则上加密生成、缩短冷却，保证杀敌速度快于敌人到场。</summary>
        private static ShooterRules WaveRules() => new ShooterRules(
            width: 8f, height: 10f,
            playerHalfWidth: 0.5f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 3.5f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.2f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 0.4f, spawnMinInterval: 0.4f, spawnShrinkPerWave: 0.05f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        /// <summary>事件封顶测试规则：极短的生成间隔在数秒内制造远超上限的事件。</summary>
        private static ShooterRules TrimRules() => new ShooterRules(
            width: 8f, height: 10f,
            playerHalfWidth: 0.5f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 0.15f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 0.02f, spawnMinInterval: 0.01f, spawnShrinkPerWave: 0.15f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        private static List<ShooterEventType> Types(SpaceShooterGame game) => game.Events.Select(e => e.Type).ToList();

        [Fact]
        public void 构造后初始状态合法()
        {
            var game = new SpaceShooterGame(ShooterRules.Default, seed: 42);

            Assert.Equal(new Vec2(10f, 1f), game.Player);
            Assert.Equal(3, game.Lives);
            Assert.Equal(1, game.Wave);
            Assert.Equal(0, game.Score);
            Assert.Equal(0, game.Kills);
            Assert.Equal(0f, game.Elapsed, 4);
            Assert.False(game.IsFinished);
            Assert.Empty(game.Bullets);
            Assert.Single(game.Enemies);
            Assert.Equal(ShooterEventType.Spawned, game.Events[0].Type);
            Assert.Equal(ShooterEventType.EnemySpawned, game.Events[1].Type);
        }

        [Fact]
        public void 射击沿立即出膛且冷却内被拒绝()
        {
            var game = new SpaceShooterGame(ShooterRules.Default, seed: 14);

            game.Step(0f, new ShooterInput(0f, true));
            var bullet = Assert.Single(game.Bullets);
            // 枪口在玩家顶部：纵坐标 1 + 0.5 + 0.45 = 1.95，横坐标与玩家对齐
            Assert.Equal(10f, bullet.Position.X, 3);
            Assert.Equal(1.95f, bullet.Position.Y, 3);
            Assert.Equal(ShooterEventType.Shot, game.Events[2].Type);

            // 冷却 0.35 秒：0.2 秒与 0.4 秒时刻的按下沿都在帧首被拒，冷却归零后的下一次调用放行
            game.Step(0.2f, new ShooterInput(0f, true));
            Assert.Single(game.Bullets);
            game.Step(0.2f, new ShooterInput(0f, true));
            Assert.Single(game.Bullets);
            game.Step(0.1f, new ShooterInput(0f, true));
            Assert.Equal(2, game.Bullets.Count);
        }

        [Fact]
        public void 子弹匀速上升并飞出场顶消失()
        {
            var game = new SpaceShooterGame(ShooterRules.Default, seed: 14);

            game.Step(0f, new ShooterInput(0f, true));
            Assert.Single(game.Bullets);

            game.Step(0.5f, default);
            var bullet = Assert.Single(game.Bullets);
            Assert.Equal(1.95f + 12f * 0.5f, bullet.Position.Y, 2);
            Assert.Equal(10f, bullet.Position.X, 3);

            // 累计 1.5 秒后子弹纵坐标 19.95，越过场顶 14.45 的离场线
            game.Step(1f, default);
            Assert.Empty(game.Bullets);
        }

        [Fact]
        public void 敌人按固定种子出生并沿正弦横摆下落()
        {
            var rules = ShooterRules.Default;
            var game = new SpaceShooterGame(rules, seed: 7);
            var enemy = Assert.Single(game.Enemies);
            // Random(7).NextDouble() 首值 0.38322047，出生横坐标 = 下限 + 首值 * 区间宽
            float spawnX = rules.MinEnemySpawnX + 0.38322047f * (rules.MaxEnemySpawnX - rules.MinEnemySpawnX);
            Assert.Equal(rules.EnemySpawnY, enemy.Position.Y, 3);
            Assert.Equal(spawnX, enemy.Position.X, 3);

            // 摆动频率 0.5Hz：半秒后相位 pi/2，sin=1，横坐标右移一个摆幅；纵坐标下落 2.2 * 0.5
            game.Step(0.5f, default);
            enemy = game.Enemies[0];
            Assert.Equal(spawnX + rules.EnemySwayAmplitude, enemy.Position.X, 2);
            Assert.Equal(rules.EnemySpawnY - 1.1f, enemy.Position.Y, 2);

            // 一秒后相位 pi，sin=0，横坐标回到出生锚点；纵坐标累计下落 2.2
            game.Step(0.5f, default);
            enemy = game.Enemies[0];
            Assert.Equal(spawnX, enemy.Position.X, 2);
            Assert.Equal(rules.EnemySpawnY - 2.2f, enemy.Position.Y, 2);
        }

        [Fact]
        public void 子弹与敌人相交即击杀加分()
        {
            var rules = FatRules();
            var game = new SpaceShooterGame(rules, seed: 3);

            // 相对合围速度 12 + 2.2：相交发生在约 0.59 秒，半秒时纵向间隙 1.55 尚未命中
            game.Step(0.5f, new ShooterInput(0f, true));
            Assert.Single(game.Bullets);
            Assert.Equal(0, game.Score);

            game.Step(0.2f, default);
            Assert.Empty(game.Bullets);
            Assert.Empty(game.Enemies);
            Assert.Equal(rules.ScorePerKill, game.Score);
            Assert.Equal(1, game.Kills);
            Assert.Equal(rules.MaxLives, game.Lives);
            Assert.Contains(ShooterEventType.EnemyKilled, Types(game));
        }

        [Fact]
        public void 敌人落底逃逸并移除()
        {
            var rules = EscapeRules();
            var game = new SpaceShooterGame(rules, seed: 2);
            var enemy = Assert.Single(game.Enemies);
            // Random(2).NextDouble() 首值 0.77109390
            float spawnX = rules.MinEnemySpawnX + 0.77109390f * (rules.MaxEnemySpawnX - rules.MinEnemySpawnX);
            Assert.Equal(spawnX, enemy.Position.X, 3);

            // 玩家半秒内横移 4 个单位贴到左壁，与出生横坐标 5.735 的敌人彻底错开
            game.Step(0.5f, new ShooterInput(-1f, false));
            Assert.Equal(rules.MinPlayerX, game.Player.X, 3);
            Assert.Equal(spawnX, game.Enemies[0].Position.X, 3);

            // 敌人从 10.6 落到 -0.6 的逃逸线需要约 5.09 秒
            game.Step(6f, default);
            Assert.Empty(game.Enemies);
            Assert.Contains(ShooterEventType.EnemyEscaped, Types(game));
            Assert.DoesNotContain(ShooterEventType.PlayerHit, Types(game));
            Assert.Equal(rules.MaxLives, game.Lives);
            Assert.Equal(0, game.Score);
        }

        [Fact]
        public void 敌人撞玩家扣命并移除敌人()
        {
            var rules = CollisionRules();
            var game = new SpaceShooterGame(rules, seed: 5);

            // 敌人从 10.6 落到玩家受击线 2.1 需要 8.5 / 2.2 ≈ 3.86 秒
            game.Step(4.5f, default);

            Assert.Empty(game.Enemies);
            Assert.Equal(rules.MaxLives - 1, game.Lives);
            Assert.Contains(ShooterEventType.PlayerHit, Types(game));
            Assert.Contains(ShooterEventType.LifeLost, Types(game));
            Assert.False(game.IsFinished);
        }

        [Fact]
        public void 生命耗尽后战局结束且推进冻结()
        {
            var rules = GameOverRules();
            var game = new SpaceShooterGame(rules, seed: 5);

            // 三名敌人分别约在 3.86、4.36、4.86 秒撞上玩家
            game.Step(6f, default);

            Assert.True(game.IsGameOver);
            Assert.Equal(0, game.Lives);
            Assert.Contains(ShooterEventType.PlayerHit, Types(game));
            Assert.Contains(ShooterEventType.LifeLost, Types(game));
            Assert.Contains(ShooterEventType.GameOver, Types(game));

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new ShooterInput(1f, true));
            Assert.Equal(elapsed, game.Elapsed, 4);
            Assert.Equal(score, game.Score);
            Assert.Empty(game.Bullets);
        }

        [Fact]
        public void 击杀十次升波且敌人提速()
        {
            var rules = WaveRules();
            var game = new SpaceShooterGame(rules, seed: 3);

            // 宽玩家规则下每颗子弹必命中：以 0.25 秒节奏持续射击，杀敌速度追平 0.4 秒的生成节奏
            for (int i = 0; i < 40 && game.Wave < 2; i++)
            {
                game.Step(0.25f, new ShooterInput(0f, true));
            }

            Assert.Equal(2, game.Wave);
            Assert.True(game.Kills >= rules.KillsPerWave, $"击杀数应达到 {rules.KillsPerWave}，实际 {game.Kills}");
            Assert.True(game.Kills < rules.KillsPerWave + 5, $"循环应停在升波后不久，实际击杀 {game.Kills}");
            Assert.Equal(game.Kills * rules.ScorePerKill, game.Score);
            Assert.Contains(ShooterEventType.WaveUp, Types(game));
            Assert.Equal(rules.MaxLives, game.Lives);
            // 升波后下落速度提升 10%：2.2 * 1.1 = 2.42
            Assert.Equal(2.42f, rules.FallSpeedFor(2), 3);
        }

        [Fact]
        public void 相同种子与输入的模拟完全一致()
        {
            static string Simulate(int seed)
            {
                var game = new SpaceShooterGame(ShooterRules.Default, seed);
                for (int i = 0; i < 24; i++)
                {
                    var moveX = (i % 4) switch { 0 => 1f, 1 => 1f, 2 => -1f, _ => 0f };
                    game.Step(0.25f, new ShooterInput(moveX, i % 3 == 0));
                }

                return string.Join(';', game.Score, game.Lives, game.Wave, game.Kills, game.Player,
                    string.Join("|", game.Bullets), string.Join("|", game.Enemies),
                    game.Events.Count, game.Events[^1].Type);
            }

            Assert.Equal(Simulate(7), Simulate(7));
            Assert.NotEqual(Simulate(7), Simulate(8));
        }

        [Fact]
        public void 玩家横移被场地边界夹住()
        {
            var rules = ShooterRules.Default;
            var game = new SpaceShooterGame(rules, seed: 20);

            // 3 秒位移 24 个单位，远超场地，被夹在右壁 19
            game.Step(3f, new ShooterInput(1f, false));
            Assert.Equal(rules.MaxPlayerX, game.Player.X, 3);

            // 反向 3 秒后贴到左壁 1，再半秒回移 4 个单位
            game.Step(3f, new ShooterInput(-1f, false));
            Assert.Equal(rules.MinPlayerX, game.Player.X, 3);
            game.Step(0.5f, new ShooterInput(1f, false));
            Assert.Equal(rules.MinPlayerX + rules.PlayerSpeed * 0.5f, game.Player.X, 2);
        }

        [Fact]
        public void 生成间隔与敌速随波次变化()
        {
            var rules = ShooterRules.Default;
            Assert.Equal(2f, rules.SpawnIntervalFor(1), 3);
            Assert.Equal(2f - 3 * rules.SpawnShrinkPerWave, rules.SpawnIntervalFor(4), 3);
            Assert.Equal(rules.SpawnMinInterval, rules.SpawnIntervalFor(100), 3);
            Assert.Equal(2.2f, rules.FallSpeedFor(1), 3);
            Assert.Equal(2.42f, rules.FallSpeedFor(2), 3);
            Assert.Equal(4.4f, rules.FallSpeedFor(11), 3);
            Assert.Equal(1, rules.WaveFor(0));
            Assert.Equal(1, rules.WaveFor(9));
            Assert.Equal(2, rules.WaveFor(10));
            Assert.Equal(3, rules.WaveFor(25));
        }

        [Fact]
        public void 事件日志封顶裁剪()
        {
            var rules = TrimRules();
            var game = new SpaceShooterGame(rules, seed: 2);
            Assert.Equal(ShooterEventType.Spawned, game.Events[0].Type);

            // 0.02 秒一敌，3.5 秒约产生 175 条生成事件，日志保留最近 128 条
            game.Step(3.5f, default);

            Assert.Equal(128, game.Events.Count);
            Assert.Equal(ShooterEventType.EnemySpawned, game.Events[^1].Type);
        }
    }
}
