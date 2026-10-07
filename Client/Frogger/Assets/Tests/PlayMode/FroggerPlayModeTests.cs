using System.Collections;
using Lyking.Frogger.Core;
using Lyking.Frogger.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Frogger.PlayModeTests
{
    public sealed class FroggerPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FroggerDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FroggerDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(3, bootstrap.Board.Lives);
            Assert.AreEqual(1, bootstrap.Board.Round);
            Assert.AreEqual(0, bootstrap.Board.FrogRow);
            Assert.IsFalse(bootstrap.Board.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进棋局且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FroggerDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FroggerDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            float before = bootstrap!.Board.Lanes[0].Positions[0];
            bootstrap.Step(0.5f, null);
            Assert.Greater(bootstrap.Board.Elapsed, 0f);
            Assert.AreNotEqual(before, bootstrap.Board.Lanes[0].Positions[0]);

            bootstrap.StartGame();
            Assert.AreEqual(0f, bootstrap.Board.Elapsed, 0.0001f);
            Assert.AreEqual(3, bootstrap.Board.Lives);
            Assert.AreEqual(1, bootstrap.Board.Round);
            Assert.AreEqual(0, bootstrap.Board.GoalsFilled);
            Assert.AreEqual(0, bootstrap.Board.FrogRow);
            Assert.AreEqual(FroggerEventType.Spawned, bootstrap.Board.Events[0].Type);
        }
    }
}
