using System.Collections;
using Lyking.MazeChase.Core;
using Lyking.MazeChase.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.MazeChase.PlayModeTests
{
    public sealed class MazeChasePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MazeChaseDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MazeChaseDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.AreEqual(MazeChaseRules.Default.GhostCount, bootstrap.Game.Ghosts.Count);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进玩家移动且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MazeChaseDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MazeChaseDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            float xBefore = bootstrap!.Game.PlayerX;
            bootstrap.Step(1f, MazeDir.Right);
            Assert.Greater(bootstrap.Game.PlayerX, xBefore);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(1, bootstrap.Game.Level);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.AreEqual(MazeChaseRules.Default.Map.PlayerStart, bootstrap.Game.PlayerCell);
            Assert.IsFalse(bootstrap.Game.IsFinished);
        }
    }
}
