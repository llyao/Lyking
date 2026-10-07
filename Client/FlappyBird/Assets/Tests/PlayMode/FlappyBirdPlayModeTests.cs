using System.Collections;
using Lyking.FlappyBird.Core;
using Lyking.FlappyBird.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.FlappyBird.PlayModeTests
{
    public sealed class FlappyBirdPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FlappyBirdDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FlappyBirdDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(FlappyPhase.Ready, bootstrap.Board.Phase);
            Assert.AreEqual(0, bootstrap.Board.Score);
            Assert.AreEqual(FlappyRules.Default.HoverHeight, bootstrap.Board.Position.Y);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进小鸟下落且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FlappyBirdDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FlappyBirdDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 点按起飞后下落 1 秒，高度低于悬停线。
            bootstrap!.Step(1f, flap: true);
            Assert.AreEqual(FlappyPhase.Running, bootstrap.Board.Phase);
            Assert.Less(bootstrap.Board.Position.Y, FlappyRules.Default.HoverHeight);

            // 再下落 1 秒必然触地结束。
            bootstrap.Step(1f, flap: false);
            Assert.IsTrue(bootstrap.Board.IsFinished);
            Assert.AreEqual(FlappyEventType.HitGround, bootstrap.Board.Events[bootstrap.Board.Events.Count - 1].Type);

            bootstrap.StartGame();
            Assert.AreEqual(FlappyPhase.Ready, bootstrap.Board.Phase);
            Assert.AreEqual(0, bootstrap.Board.Score);
            Assert.IsEmpty(bootstrap.Board.Pipes);
            Assert.AreEqual(FlappyRules.Default.HoverHeight, bootstrap.Board.Position.Y);
        }
    }
}
