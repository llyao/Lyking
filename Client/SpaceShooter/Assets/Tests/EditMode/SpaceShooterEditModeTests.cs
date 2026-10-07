using System.Linq;
using Lyking.SpaceShooter.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.SpaceShooter.EditModeTests
{
    /// <summary>编辑模式冒烟：核心战局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SpaceShooterEditModeTests
    {
        private static ShooterRules FatRules() => new ShooterRules(
            width: 8f, height: 10f,
            playerHalfWidth: 0.5f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 3.5f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 100f, spawnMinInterval: 0.7f, spawnShrinkPerWave: 0.15f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        private static ShooterRules GameOverRules() => new ShooterRules(
            width: 4f, height: 10f,
            playerHalfWidth: 1f, playerHalfHeight: 0.5f, playerSpeed: 8f, playerStartY: 1f,
            bulletHalfWidth: 0.15f, bulletHalfHeight: 0.45f, bulletSpeed: 12f, fireCooldownSeconds: 0.35f,
            enemyHalfWidth: 0.8f, enemyHalfHeight: 0.6f, enemyBaseFallSpeed: 2.2f,
            enemySwayAmplitude: 0f, enemySwayFrequencyHz: 0.5f,
            spawnBaseInterval: 0.5f, spawnMinInterval: 0.5f, spawnShrinkPerWave: 0.05f,
            maxLives: 3, scorePerKill: 100, killsPerWave: 10, waveSpeedGain: 0.1f);

        [Test]
        public void 战局在引擎外可创建并推进()
        {
            var game = new SpaceShooterGame(ShooterRules.Default, seed: 7);

            Assert.AreEqual(new Vec2(10f, 1f), game.Player);
            Assert.AreEqual(1, game.Enemies.Count);
            Assert.IsFalse(game.IsFinished);
            Assert.GreaterOrEqual(game.Enemies[0].Position.X, game.Rules.MinEnemySpawnX);
            Assert.LessOrEqual(game.Enemies[0].Position.X, game.Rules.MaxEnemySpawnX);

            float yBefore = game.Enemies[0].Position.Y;
            game.Step(0.5f, new ShooterInput(1f, false));
            Assert.Greater(game.Elapsed, 0f);
            Assert.Less(game.Enemies[0].Position.Y, yBefore);
            Assert.Greater(game.Player.X, 10f);
        }

        [Test]
        public void 射击沿出膛且冷却内被拒绝()
        {
            var game = new SpaceShooterGame(ShooterRules.Default, seed: 14);

            game.Step(0f, new ShooterInput(0f, true));
            Assert.AreEqual(1, game.Bullets.Count);
            Assert.AreEqual(1.95f, game.Bullets[0].Position.Y, 0.001f);

            game.Step(0.2f, new ShooterInput(0f, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.2f, new ShooterInput(0f, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.1f, new ShooterInput(0f, true));
            Assert.AreEqual(2, game.Bullets.Count);
        }

        [Test]
        public void 子弹命中敌人加分并写入击杀事件()
        {
            var rules = FatRules();
            var game = new SpaceShooterGame(rules, seed: 3);

            game.Step(0.5f, new ShooterInput(0f, true));
            Assert.AreEqual(0, game.Score);
            game.Step(0.2f, default);

            Assert.AreEqual(rules.ScorePerKill, game.Score);
            Assert.AreEqual(1, game.Kills);
            Assert.IsEmpty(game.Enemies);
            Assert.IsTrue(game.Events.Any(e => e.Type == ShooterEventType.EnemyKilled));
        }

        [Test]
        public void 游戏结束后推进保持状态不变()
        {
            var game = new SpaceShooterGame(GameOverRules(), seed: 5);

            game.Step(6f, default);
            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(0, game.Lives);
            Assert.AreEqual(ShooterEventType.GameOver, game.Events[game.Events.Count - 1].Type);

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new ShooterInput(1f, true));
            Assert.AreEqual(elapsed, game.Elapsed, 0.0001f);
            Assert.AreEqual(score, game.Score);
            Assert.IsEmpty(game.Bullets);
        }
    }
}
