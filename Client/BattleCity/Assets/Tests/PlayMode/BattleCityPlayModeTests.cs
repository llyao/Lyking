using System.Collections;
using Lyking.BattleCity.Core;
using Lyking.BattleCity.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.BattleCity.PlayModeTests
{
    public sealed class BattleCityPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后战局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BattleCityDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BattleCityDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(1, bootstrap.Game.Enemies.Count);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进战局且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BattleCityDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BattleCityDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            float yBefore = bootstrap!.Game.Enemies[0].Position.Y;
            bootstrap.Step(0.5f, default);
            Assert.Less(bootstrap.Game.Enemies[0].Position.Y, yBefore);
            Assert.Greater(bootstrap.Game.Elapsed, 0f);

            bootstrap.Step(0f, new TankInput(TankDir.None, true));
            Assert.AreEqual(1, bootstrap.Game.Bullets.Count);

            bootstrap.StartGame();
            Assert.AreEqual(0f, bootstrap.Game.Elapsed, 0.0001f);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.AreEqual(1, bootstrap.Game.Level);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(6, bootstrap.Game.EnemiesRemaining);
            Assert.AreEqual(BattleCityEventType.Spawned, bootstrap.Game.Events[0].Type);
        }
    }
}
