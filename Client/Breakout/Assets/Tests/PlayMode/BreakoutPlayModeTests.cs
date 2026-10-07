using System.Collections;
using Lyking.Breakout.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Breakout.PlayModeTests
{
    public sealed class BreakoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BreakoutDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BreakoutDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(40, bootstrap.Game.AliveBrickCount);
            Assert.IsFalse(bootstrap.Game.IsGameOver);
            Assert.IsFalse(bootstrap.Game.BallLaunched);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进球上升且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BreakoutDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BreakoutDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            Assert.IsTrue(bootstrap!.Game.Launch(0f));
            float yBefore = bootstrap.Game.BallPosition.Y;
            bootstrap.Step(0.5f);
            Assert.Greater(bootstrap.Game.BallPosition.Y, yBefore);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(1, bootstrap.Game.Level);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.IsFalse(bootstrap.Game.BallLaunched);
            Assert.AreEqual(40, bootstrap.Game.AliveBrickCount);
        }
    }
}
