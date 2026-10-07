using System.Collections;
using Lyking.EndlessRunner.Core;
using Lyking.EndlessRunner.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.EndlessRunner.PlayModeTests
{
    public sealed class EndlessRunnerPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("EndlessRunnerDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<EndlessRunnerDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.IsTrue(bootstrap.Game.IsGrounded);
            Assert.AreEqual(0f, bootstrap.Game.Distance);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.IsEmpty(bootstrap.Game.Obstacles);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进世界滚动且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("EndlessRunnerDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<EndlessRunnerDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 无输入推进 1 秒：世界以约 5 米/秒滚动，距离与分数随之增长。
            bootstrap!.Step(1f, RunnerInput.None);
            Assert.Greater(bootstrap.Game.Distance, 4f);
            Assert.Greater(bootstrap.Game.Score, 0);
            Assert.IsTrue(bootstrap.Game.IsGrounded);
            Assert.IsFalse(bootstrap.Game.IsFinished);

            bootstrap.StartGame();
            Assert.AreEqual(0f, bootstrap.Game.Distance);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.IsEmpty(bootstrap.Game.Obstacles);
            Assert.IsTrue(bootstrap.Game.IsGrounded);
            Assert.IsFalse(bootstrap.Game.IsFinished);
        }
    }
}
