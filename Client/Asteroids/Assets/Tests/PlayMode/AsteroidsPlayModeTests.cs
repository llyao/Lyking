using System.Collections;
using Lyking.Asteroids.Core;
using Lyking.Asteroids.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Asteroids.PlayModeTests
{
    public sealed class AsteroidsPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后战局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("AsteroidsDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<AsteroidsDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(4, bootstrap.Game.Asteroids.Count);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进战局且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("AsteroidsDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<AsteroidsDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            var positionBefore = bootstrap!.Game.Asteroids[0].Position;
            bootstrap.Step(0.5f, default);
            Assert.Greater(bootstrap.Game.Elapsed, 0f);
            Assert.AreNotEqual(positionBefore, bootstrap.Game.Asteroids[0].Position);

            bootstrap.Step(0f, new AsteroidsInput(0f, false, true));
            Assert.AreEqual(1, bootstrap.Game.Bullets.Count);

            bootstrap.StartGame();
            Assert.AreEqual(0f, bootstrap.Game.Elapsed, 0.0001f);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.AreEqual(1, bootstrap.Game.Wave);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(AsteroidsEventType.GameStarted, bootstrap.Game.Events[0].Type);
        }
    }
}
