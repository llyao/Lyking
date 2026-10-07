using System.Linq;
using Lyking.Asteroids.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Asteroids.EditModeTests
{
    /// <summary>编辑模式冒烟：核心战局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class AsteroidsEditModeTests
    {
        /// <summary>远离飞船规则：大档半径 5 使出生点必然距飞船 6.6 以上、距竖直子弹路径 5.88 以上，冻结慢速排除一切碰撞干扰。</summary>
        private static AsteroidsRules FarRules() => new AsteroidsRules(
            width: 24f, height: 16f,
            shipRadius: 0.6f, shipTurnSpeed: 4f, shipThrustAcceleration: 10f, shipMaxSpeed: 9f, shipDampingPerSecond: 0f,
            bulletRadius: 0.15f, bulletSpeed: 16f, bulletLifetimeSeconds: 1.1f, fireCooldownSeconds: 0.3f,
            asteroidLargeRadius: 5f, asteroidMediumRadius: 3f, asteroidSmallRadius: 1f,
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

        [Test]
        public void 战局在引擎外可创建并推进()
        {
            var game = new AsteroidsGame(FarRules(), seed: 7);

            Assert.AreEqual(new Vec2(12f, 8f), game.Ship.Position);
            Assert.AreEqual(Mathf.PI / 2f, game.Ship.Heading, 0.001f);
            Assert.AreEqual(1, game.Asteroids.Count);
            Assert.IsFalse(game.IsFinished);
            // 出生点整颗在场内且避开飞船，几何上保证测试期间互不接触。
            var asteroid = game.Asteroids[0];
            Assert.GreaterOrEqual(asteroid.Position.X, game.Rules.MinAsteroidSpawnX);
            Assert.LessOrEqual(asteroid.Position.X, game.Rules.MaxAsteroidSpawnX);
            Assert.GreaterOrEqual(asteroid.Position.Y, game.Rules.MinAsteroidSpawnY);
            Assert.LessOrEqual(asteroid.Position.Y, game.Rules.MaxAsteroidSpawnY);

            game.Step(0.5f, new AsteroidsInput(1f, false, false));
            Assert.Greater(game.Elapsed, 0f);
            Assert.Less(game.Ship.Heading, Mathf.PI / 2f);
            // 无推进则速度仍为零，飞船位置不动，仅朝向右转 2 弧度。
            Assert.AreEqual(new Vec2(0f, 0f), game.Ship.Velocity);
            Assert.AreEqual(new Vec2(12f, 8f), game.Ship.Position);
            Assert.AreEqual(Mathf.PI / 2f - 2f, game.Ship.Heading, 0.001f);
        }

        [Test]
        public void 射击沿出膛且冷却内被拒绝()
        {
            var game = new AsteroidsGame(FarRules(), seed: 14);

            game.Step(0f, new AsteroidsInput(0f, false, true));
            Assert.AreEqual(1, game.Bullets.Count);
            Assert.AreEqual(8.75f, game.Bullets[0].Position.Y, 0.001f);

            // 冷却 0.3 秒：0.2 秒与 0.4 秒时刻的按下沿都在帧首被拒，冷却归零后的下一次调用放行。
            game.Step(0.2f, new AsteroidsInput(0f, false, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.2f, new AsteroidsInput(0f, false, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.1f, new AsteroidsInput(0f, false, true));
            Assert.AreEqual(2, game.Bullets.Count);
        }

        [Test]
        public void 大档小行星被击碎成两个中档并加分()
        {
            var rules = FatRules();
            var game = new AsteroidsGame(rules, seed: 3);

            game.Step(0.15f, new AsteroidsInput(0f, false, true));

            Assert.AreEqual(rules.AsteroidScoreFor(AsteroidSize.Large), game.Score);
            Assert.AreEqual(5, game.Asteroids.Count);
            Assert.AreEqual(3, game.Asteroids.Count(a => a.Size == AsteroidSize.Large));
            Assert.AreEqual(2, game.Asteroids.Count(a => a.Size == AsteroidSize.Medium));
            Assert.IsTrue(game.Events.Any(e => e.Type == AsteroidsEventType.AsteroidSplit && e.Size == AsteroidSize.Large));
        }

        [Test]
        public void 游戏结束后推进保持状态不变()
        {
            var game = new AsteroidsGame(CollisionRules(), seed: 5);

            game.Step(6f, default);
            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(0, game.Lives);
            Assert.AreEqual(AsteroidsEventType.GameOver, game.Events[game.Events.Count - 1].Type);

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new AsteroidsInput(1f, true, true));
            Assert.AreEqual(elapsed, game.Elapsed, 0.0001f);
            Assert.AreEqual(score, game.Score);
            Assert.IsEmpty(game.Bullets);
        }
    }
}
