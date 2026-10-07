using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Asteroids.Core;
using Xunit;

namespace Lyking.Asteroids.Core.Tests
{
    /// <summary>小行星核心规则测试：旋转推进手算、惯性阻尼、子弹环绕与寿命、三段碎裂、受击无敌、波次生成、确定性与事件封顶。</summary>
    public class AsteroidsGameTests
    {
        /// <summary>旋转与推进手算规则：无阻尼、无限速，小行星冻结且仅 1 颗，排除受击干扰。</summary>
        private static AsteroidsRules PhysicsRules() => new AsteroidsRules(
            width: 24f, height: 16f,
            shipRadius: 0.6f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 1000f, shipDampingPerSecond: 0f,
            bulletRadius: 0.15f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.3f,
            asteroidLargeRadius: 1.6f, asteroidMediumRadius: 1f, asteroidSmallRadius: 0.55f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 0.001f, asteroidSpinRange: 0f,
            asteroidsPerWave: 1, waveAsteroidGain: 0, waveSpeedGain: 0f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        /// <summary>惯性漂移规则：在物理手算规则上恢复默认阻尼 0.45。</summary>
        private static AsteroidsRules DampedRules() => new AsteroidsRules(
            width: 24f, height: 16f,
            shipRadius: 0.6f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 1000f, shipDampingPerSecond: 0.45f,
            bulletRadius: 0.15f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.3f,
            asteroidLargeRadius: 1.6f, asteroidMediumRadius: 1f, asteroidSmallRadius: 0.55f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 0.001f, asteroidSpinRange: 0f,
            asteroidsPerWave: 1, waveAsteroidGain: 0, waveSpeedGain: 0f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        /// <summary>碎裂测试规则：宽 10 的子弹覆盖全场，任何一颗子弹都必然在首个子步命中小行星。</summary>
        private static AsteroidsRules FatRules() => new AsteroidsRules(
            width: 8f, height: 10f,
            shipRadius: 0.5f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 9f, shipDampingPerSecond: 0.45f,
            bulletRadius: 10f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.1f,
            asteroidLargeRadius: 1.6f, asteroidMediumRadius: 1f, asteroidSmallRadius: 0.55f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 2.4f, asteroidSpinRange: 1.5f,
            asteroidsPerWave: 4, waveAsteroidGain: 1, waveSpeedGain: 0.1f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        /// <summary>受击测试规则：飞船半径 4 的圆覆盖整个出生区，任意小行星都必然撞上场心飞船。</summary>
        private static AsteroidsRules CollisionRules() => new AsteroidsRules(
            width: 8f, height: 10f,
            shipRadius: 4f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 9f, shipDampingPerSecond: 0.45f,
            bulletRadius: 0.15f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.3f,
            asteroidLargeRadius: 1.6f, asteroidMediumRadius: 1f, asteroidSmallRadius: 0.55f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 2.4f, asteroidSpinRange: 1.5f,
            asteroidsPerWave: 4, waveAsteroidGain: 1, waveSpeedGain: 0.1f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        /// <summary>清波测试规则：每波 1 颗逐波加 1，冷却 0.02 秒，宽子弹保证速射必中。</summary>
        private static AsteroidsRules TrimWaveRules() => new AsteroidsRules(
            width: 8f, height: 10f,
            shipRadius: 0.5f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 9f, shipDampingPerSecond: 0.45f,
            bulletRadius: 10f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.02f,
            asteroidLargeRadius: 1f, asteroidMediumRadius: 0.6f, asteroidSmallRadius: 0.3f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 2.4f, asteroidSpinRange: 1.5f,
            asteroidsPerWave: 1, waveAsteroidGain: 1, waveSpeedGain: 0.1f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        /// <summary>事件封顶测试规则：每波 1 颗逐波不加量，0.02 秒连射在数秒内制造远超上限的事件。</summary>
        private static AsteroidsRules TrimRules() => new AsteroidsRules(
            width: 8f, height: 10f,
            shipRadius: 0.5f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 9f, shipDampingPerSecond: 0.45f,
            bulletRadius: 10f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.02f,
            asteroidLargeRadius: 1f, asteroidMediumRadius: 0.6f, asteroidSmallRadius: 0.3f,
            asteroidLargeScore: 20, asteroidMediumScore: 50, asteroidSmallScore: 100,
            asteroidBaseSpeed: 2.4f, asteroidSpinRange: 1.5f,
            asteroidsPerWave: 1, waveAsteroidGain: 0, waveSpeedGain: 0f,
            maxLives: 3, respawnInvulnerableSeconds: 2.5f);

        private static float Speed(in Vec2 velocity) => MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);

        [Fact]
        public void 构造后初始状态合法()
        {
            var game = new AsteroidsGame(AsteroidsRules.Default, seed: 42);

            // 飞船在场心朝上，满命第 1 波，事件以开局与开波打头。
            Assert.Equal(new Vec2(12f, 8f), game.Ship.Position);
            Assert.Equal(new Vec2(0f, 0f), game.Ship.Velocity);
            Assert.Equal(MathF.PI / 2f, game.Ship.Heading, 4);
            Assert.Equal(3, game.Lives);
            Assert.Equal(1, game.Wave);
            Assert.Equal(0, game.Score);
            Assert.Equal(0f, game.Elapsed, 4);
            Assert.False(game.IsFinished);
            Assert.Empty(game.Bullets);
            Assert.Equal(4, game.Asteroids.Count);
            Assert.Equal(AsteroidsEventType.GameStarted, game.Events[0].Type);
            Assert.Equal(AsteroidsEventType.WaveStarted, game.Events[1].Type);

            // 每颗首波小行星都是大档，速度大小恰为基础速度，出生点整颗在场内且避开飞船。
            var rules = AsteroidsRules.Default;
            foreach (var asteroid in game.Asteroids)
            {
                Assert.Equal(AsteroidSize.Large, asteroid.Size);
                Assert.Equal(rules.AsteroidBaseSpeed, Speed(asteroid.Velocity), 3);
                Assert.InRange(asteroid.Position.X, rules.MinAsteroidSpawnX, rules.MaxAsteroidSpawnX);
                Assert.InRange(asteroid.Position.Y, rules.MinAsteroidSpawnY, rules.MaxAsteroidSpawnY);
                Assert.True(asteroid.Position.DistanceTo(rules.ShipSpawnPosition) >= rules.AsteroidSafeSpawnDistance,
                    $"出生点 {asteroid.Position} 应避开飞船 {rules.ShipSpawnPosition}");
            }
        }

        [Fact]
        public void 旋转与推进按手算推进()
        {
            var game = new AsteroidsGame(PhysicsRules(), seed: 11);

            // 转向速度 4 弧度/秒：0.5 秒右转 2 弧度，朝向从 pi/2 减到 pi/2-2；无推进则速度仍为零、位置不动。
            game.Step(0.5f, new AsteroidsInput(1f, false, false));
            Assert.Equal(MathF.PI / 2f - 2f, game.Ship.Heading, 4);
            Assert.Equal(new Vec2(12f, 8f), game.Ship.Position);
            Assert.Equal(new Vec2(0f, 0f), game.Ship.Velocity);

            // 单个子步（1/120 秒）沿新朝向推进：速度增量 = 朝向向量 * 10/120，
            // 朝向 pi/2-2 的方向向量 = (sin2, -cos2) = (0.9093, -0.4161)。
            game.Step(1f / 120f, new AsteroidsInput(0f, true, false));
            Assert.Equal(0.0758f, game.Ship.Velocity.X, 4);
            Assert.Equal(-0.0347f, game.Ship.Velocity.Y, 4);
            // 位置再前进一个子步：12 + 0.0758/120 = 12.0006，8 - 0.0347/120 = 7.9997。
            Assert.Equal(12.0006f, game.Ship.Position.X, 4);
            Assert.Equal(7.9997f, game.Ship.Position.Y, 4);
        }

        [Fact]
        public void 推进停止后按惯性漂移并受阻尼衰减()
        {
            var game = new AsteroidsGame(DampedRules(), seed: 11);

            // 单子步推进：速度 10/120 = 0.0833，随即被阻尼乘 (1 - 0.45/120) = 0.99625，得 0.0830。
            game.Step(1f / 120f, new AsteroidsInput(0f, true, false));
            Assert.Equal(0.0830f, game.Ship.Velocity.Y, 4);
            Assert.Equal(8.0007f, game.Ship.Position.Y, 4);

            // 之后 1 秒纯惯性：每子步速度乘 0.99625，120 子步后衰减为 0.0830 * 0.99625^120 = 0.0529；
            // 累计位移 = v0 * r * (1 - r^120) / ((1 - r) * 120) = 0.0667，纵坐标共 8.067。
            game.Step(1f, default);
            Assert.Equal(0.0529f, game.Ship.Velocity.Y, 4);
            Assert.Equal(8.067f, game.Ship.Position.Y, 3);
        }

        [Fact]
        public void 子弹沿朝向发射环绕且寿命到期消失()
        {
            var game = new AsteroidsGame(PhysicsRules(), seed: 42);

            // 枪口在飞船朝向前方：纵坐标 8 + 0.6 + 0.15 = 8.75，横坐标与飞船对齐。
            game.Step(0f, new AsteroidsInput(0f, false, true));
            var bullet = Assert.Single(game.Bullets);
            Assert.Equal(12f, bullet.Position.X, 3);
            Assert.Equal(8.75f, bullet.Position.Y, 3);

            // 半秒后匀速上飞 8 个单位：16.75 越过场顶 16，环绕到 0.75，寿命 0.6 仍在。
            game.Step(0.5f, default);
            bullet = Assert.Single(game.Bullets);
            Assert.Equal(12f, bullet.Position.X, 3);
            Assert.Equal(0.75f, bullet.Position.Y, 3);

            // 累计 1 秒后再次环绕回到枪口高度 8.75，寿命剩 0.1 仍在。
            game.Step(0.5f, default);
            bullet = Assert.Single(game.Bullets);
            Assert.Equal(8.75f, bullet.Position.Y, 3);

            // 累计 1.2 秒超过寿命 1.1，子弹消失。
            game.Step(0.2f, default);
            Assert.Empty(game.Bullets);
        }

        [Fact]
        public void 大档小行星被击碎成两个中档并加分()
        {
            var game = new AsteroidsGame(FatRules(), seed: 3);

            game.Step(0.15f, new AsteroidsInput(0f, false, true));

            // 首颗命中的是大档：4 - 1 + 2 = 5 颗，余 3 大 2 中，加 20 分。
            Assert.Equal(5, game.Asteroids.Count);
            Assert.Equal(3, game.Asteroids.Count(a => a.Size == AsteroidSize.Large));
            Assert.Equal(2, game.Asteroids.Count(a => a.Size == AsteroidSize.Medium));
            Assert.Equal(20, game.Score);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.AsteroidSplit && e.Size == AsteroidSize.Large);
        }

        [Fact]
        public void 中档小行星被击碎成两个小档并加分()
        {
            var game = new AsteroidsGame(FatRules(), seed: 3);

            // 前 4 射清掉 4 颗大档（+80），第 5 射命中首个中档。
            for (int i = 0; i < 5; i++) game.Step(0.15f, new AsteroidsInput(0f, false, true));

            Assert.Equal(9, game.Asteroids.Count);
            Assert.DoesNotContain(game.Asteroids, a => a.Size == AsteroidSize.Large);
            Assert.Equal(7, game.Asteroids.Count(a => a.Size == AsteroidSize.Medium));
            Assert.Equal(2, game.Asteroids.Count(a => a.Size == AsteroidSize.Small));
            Assert.Equal(80 + 50, game.Score);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.AsteroidSplit && e.Size == AsteroidSize.Medium);
        }

        [Fact]
        public void 小档小行星被击中直接消灭并加分()
        {
            var game = new AsteroidsGame(FatRules(), seed: 3);

            // 4 大 + 8 中共 12 射产生 16 颗小档，第 13 射命中小档后数量减一不再增加。
            for (int i = 0; i < 13; i++) game.Step(0.15f, new AsteroidsInput(0f, false, true));

            Assert.Equal(15, game.Asteroids.Count);
            Assert.All(game.Asteroids, a => Assert.Equal(AsteroidSize.Small, a.Size));
            Assert.Equal(4 * 20 + 8 * 50 + 100, game.Score);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.AsteroidSplit && e.Size == AsteroidSize.Small);
        }

        [Fact]
        public void 小行星撞飞船失命并场心重生()
        {
            var rules = CollisionRules();
            var game = new AsteroidsGame(rules, seed: 5);

            game.Step(0.5f, default);

            // 出生区内任何小行星必然撞上飞船：扣 1 命、场心重生、进入无敌期，小行星保留。
            Assert.Equal(rules.MaxLives - 1, game.Lives);
            Assert.Equal(rules.ShipSpawnPosition, game.Ship.Position);
            Assert.Equal(new Vec2(0f, 0f), game.Ship.Velocity);
            Assert.Equal(MathF.PI / 2f, game.Ship.Heading, 3);
            Assert.True(game.IsInvulnerable);
            Assert.Equal(2.01f, game.InvulnerableRemaining, 2);
            Assert.Equal(4, game.Asteroids.Count);
            Assert.Equal(AsteroidsEventType.Respawned, game.Events[^1].Type);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.ShipHit);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.LifeLost);
        }

        [Fact]
        public void 无敌期内免疫碰撞到期后恢复判定()
        {
            var game = new AsteroidsGame(CollisionRules(), seed: 5);

            // 撞击后剩余无敌 2.5 - 0.1 ≈ 2.41 秒。
            game.Step(0.1f, default);
            Assert.Equal(2, game.Lives);
            Assert.Equal(2.41f, game.InvulnerableRemaining, 2);

            // 无敌期内持续接触也不掉命，无敌倒计时继续走。
            game.Step(2.3f, default);
            Assert.Equal(2, game.Lives);
            Assert.True(game.IsInvulnerable);
            Assert.Equal(0.12f, game.InvulnerableRemaining, 2);

            // 无敌到期后恢复判定，立刻再次被撞扣命并重新进入无敌。
            game.Step(0.5f, default);
            Assert.Equal(1, game.Lives);
            Assert.True(game.IsInvulnerable);
            Assert.Equal(2.12f, game.InvulnerableRemaining, 2);
        }

        [Fact]
        public void 生命耗尽游戏结束且推进冻结()
        {
            var game = new AsteroidsGame(CollisionRules(), seed: 5);

            // 无敌 2.5 秒一撞：约 0、2.5、5.0 秒三次撞击耗尽 3 命。
            game.Step(6f, default);

            Assert.True(game.IsGameOver);
            Assert.Equal(0, game.Lives);
            Assert.Equal(AsteroidsEventType.GameOver, game.Events[^1].Type);
            Assert.Contains(game.Events, e => e.Type == AsteroidsEventType.GameOver);

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new AsteroidsInput(1f, true, true));
            Assert.Equal(elapsed, game.Elapsed, 4);
            Assert.Equal(score, game.Score);
            Assert.Empty(game.Bullets);
        }

        [Fact]
        public void 清空一波进入下一波且数量与速度提升()
        {
            var rules = TrimWaveRules();
            var game = new AsteroidsGame(rules, seed: 5);

            // 大→2 中→4 小共 7 射清空首波，同子步进入第 2 波：1 + 1 = 2 颗大档。
            for (int i = 0; i < 8 && game.Wave < 2; i++) game.Step(0.05f, new AsteroidsInput(0f, false, true));

            Assert.Equal(2, game.Wave);
            Assert.Equal(rules.AsteroidCountFor(2), game.Asteroids.Count);
            Assert.All(game.Asteroids, a => Assert.Equal(AsteroidSize.Large, a.Size));
            // 第 2 波漂移速度 2.4 * 1.1 = 2.64。
            Assert.All(game.Asteroids, a => Assert.Equal(2.64f, Speed(a.Velocity), 3));
            Assert.Equal(20 + 2 * 50 + 4 * 100, game.Score);
            Assert.Equal(2, game.Events.Count(e => e.Type == AsteroidsEventType.WaveStarted));
        }

        [Fact]
        public void 波次参数为确定性纯函数()
        {
            var rules = AsteroidsRules.Default;

            // 数量逐波 +1：4、5、6。
            Assert.Equal(4, rules.AsteroidCountFor(1));
            Assert.Equal(5, rules.AsteroidCountFor(2));
            Assert.Equal(6, rules.AsteroidCountFor(3));
            // 速度每波 +10%：2.4、2.64、2.88。
            Assert.Equal(2.4f, rules.AsteroidSpeedFor(1), 4);
            Assert.Equal(2.64f, rules.AsteroidSpeedFor(2), 4);
            Assert.Equal(2.88f, rules.AsteroidSpeedFor(3), 4);
            // 三档半径与分值。
            Assert.Equal(1.6f, rules.AsteroidRadiusFor(AsteroidSize.Large), 4);
            Assert.Equal(1f, rules.AsteroidRadiusFor(AsteroidSize.Medium), 4);
            Assert.Equal(0.55f, rules.AsteroidRadiusFor(AsteroidSize.Small), 4);
            Assert.Equal(20, rules.AsteroidScoreFor(AsteroidSize.Large));
            Assert.Equal(50, rules.AsteroidScoreFor(AsteroidSize.Medium));
            Assert.Equal(100, rules.AsteroidScoreFor(AsteroidSize.Small));
            // 场心出生与安全距离。
            Assert.Equal(new Vec2(12f, 8f), rules.ShipSpawnPosition);
            Assert.Equal(3.2f, rules.AsteroidSafeSpawnDistance, 4);
        }

        [Fact]
        public void 固定种子首波生成位置可手算()
        {
            var rules = AsteroidsRules.Default;
            var game = new AsteroidsGame(rules, seed: 7);

            // Random(7) 的 NextDouble 首值 0.38322047、次值 0.87125568，首摇即满足安全距离、无重摇：
            // x = 1.6 + 0.38322047 * 20.8 = 9.571，y = 1.6 + 0.87125568 * 12.8 = 12.752。
            var first = game.Asteroids[0];
            Assert.Equal(9.571f, first.Position.X, 3);
            Assert.Equal(12.752f, first.Position.Y, 3);
            Assert.Equal(0f, first.Rotation, 4);
            Assert.InRange(first.Spin, -rules.AsteroidSpinRange, rules.AsteroidSpinRange);
            // 后续小行星沿用同一种子链，全部在场内出生。
            Assert.Equal(4, game.Asteroids.Count);
            Assert.All(game.Asteroids, a =>
            {
                Assert.InRange(a.Position.X, rules.MinAsteroidSpawnX, rules.MaxAsteroidSpawnX);
                Assert.InRange(a.Position.Y, rules.MinAsteroidSpawnY, rules.MaxAsteroidSpawnY);
            });
        }

        [Fact]
        public void 相同种子与输入的模拟完全一致()
        {
            static string Simulate(int seed)
            {
                var game = new AsteroidsGame(AsteroidsRules.Default, seed);
                for (int i = 0; i < 24; i++)
                {
                    var turn = (i % 4) switch { 0 => 1f, 1 => 1f, 2 => -1f, _ => 0f };
                    game.Step(0.25f, new AsteroidsInput(turn, i % 2 == 0, i % 3 == 0));
                }

                return string.Join(';', game.Score, game.Lives, game.Wave, game.Ship,
                    string.Join("|", game.Bullets), string.Join("|", game.Asteroids),
                    game.Events.Count, game.Events[^1].Type);
            }

            Assert.Equal(Simulate(7), Simulate(7));
            Assert.NotEqual(Simulate(7), Simulate(8));
        }

        [Fact]
        public void 事件日志封顶裁剪()
        {
            var game = new AsteroidsGame(TrimRules(), seed: 2);

            // 0.02 秒一射、射射必中，约 600 次射击产生远超 128 条的事件，日志保留最近 128 条。
            for (int i = 0; i < 600; i++) game.Step(0.02f, new AsteroidsInput(0f, false, true));

            Assert.Equal(128, game.Events.Count);
            Assert.NotEqual(AsteroidsEventType.GameStarted, game.Events[0].Type);
        }
    }
}
