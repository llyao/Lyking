using System.Collections;
using Lyking.Snake.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Snake.PlayModeTests
{
    public sealed class SnakePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SnakeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SnakeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(3, bootstrap.Board.Length);
            Assert.IsFalse(bootstrap.Board.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进蛇前进且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SnakeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SnakeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            int xBefore = bootstrap!.Board.Head.X;
            bootstrap.Step(1f);
            Assert.Greater(bootstrap.Board.Head.X, xBefore);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.StepIndex);
            Assert.AreEqual(3, bootstrap.Board.Length);
            Assert.IsFalse(bootstrap.Board.IsFinished);
        }
    }
}
