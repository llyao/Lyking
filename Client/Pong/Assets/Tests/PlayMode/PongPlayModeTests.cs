using System.Collections;
using Lyking.Pong.Core;
using Lyking.Pong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Pong.PlayModeTests
{
    public sealed class PongPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("PongDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<PongDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(0, bootstrap.Game.LeftScore);
            Assert.AreEqual(0, bootstrap.Game.RightScore);
            Assert.AreEqual(PongState.Running, bootstrap.Game.State);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进挡板与球且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("PongDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<PongDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            bootstrap!.Step(0.1f, 1f);
            Assert.Greater(bootstrap.Game.LeftPaddlePosition.Y, 7.5f);
            Assert.Greater(bootstrap.Game.StepIndex, 0);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Game.LeftScore);
            Assert.AreEqual(0, bootstrap.Game.RightScore);
            Assert.AreEqual(PongState.Running, bootstrap.Game.State);
            Assert.AreEqual(12f, bootstrap.Game.BallPosition.X, 0.001f);
            Assert.AreEqual(7f, bootstrap.Game.BallPosition.Y, 0.001f);
            Assert.AreEqual(7f, bootstrap.Game.LeftPaddlePosition.Y, 0.001f);
        }
    }
}
