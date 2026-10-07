using System.Collections;
using Lyking.WhackAMole.Core;
using Lyking.WhackAMole.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.WhackAMole.PlayModeTests
{
    public sealed class WhackAMolePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WhackAMoleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WhackAMoleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(WhackAMolePhase.Ready, bootstrap.Game.Phase);
            Assert.AreEqual(9, WhackAMoleRules.HoleCount);
            Assert.AreEqual(WhackAMoleRules.RoundSeconds, bootstrap.Game.TimeRemaining, 1e-4f);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.IsFalse(bootstrap.Game.HasMole);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进倒计时且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WhackAMoleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WhackAMoleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 首次打击只负责开局，模拟入口推进一秒后倒计时开始递减。
            Assert.IsFalse(bootstrap!.Whack(4));
            Assert.AreEqual(WhackAMolePhase.Running, bootstrap.Game.Phase);

            bootstrap.Step(1f);
            Assert.AreEqual(29f, bootstrap.Game.TimeRemaining, 1e-3f);

            bootstrap.StartGame();
            Assert.AreEqual(WhackAMolePhase.Ready, bootstrap.Game.Phase);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(0, bootstrap.Game.Combo);
            Assert.IsFalse(bootstrap.Game.HasMole);
            Assert.AreEqual(WhackAMoleRules.RoundSeconds, bootstrap.Game.TimeRemaining, 1e-4f);
            Assert.IsFalse(bootstrap.Game.IsFinished);
        }
    }
}
