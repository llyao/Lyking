using System.Collections;
using Lyking.Simon.Core;
using Lyking.Simon.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Simon.PlayModeTests
{
    public sealed class SimonPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SimonDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SimonDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(SimonPhase.Ready, bootstrap.Game.Phase);
            Assert.AreEqual(0, bootstrap.Game.Level);
            Assert.AreEqual(0, bootstrap.Game.BestLevel);
            Assert.AreEqual(-1, bootstrap.Game.LitPadIndex);
            Assert.AreEqual(SimonRules.PadCount, 4);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进播放与超时且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SimonDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SimonDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 首次输入开始第 1 轮播放，大步跨过播放后进入复述。
            Assert.IsTrue(bootstrap!.Press(0));
            Assert.AreEqual(SimonPhase.Showing, bootstrap.Game.Phase);
            bootstrap.Step(SimonRules.SegmentSeconds + 0.1f);
            Assert.AreEqual(SimonPhase.Input, bootstrap.Game.Phase);

            // 完整复述第 1 轮升到第 2 轮，第 2 轮复述超时失败并刷新纪录。
            Assert.IsTrue(bootstrap.Press(bootstrap.Game.Sequence[0]));
            Assert.AreEqual(2, bootstrap.Game.Level);
            bootstrap.Step(SimonRules.SegmentSeconds * bootstrap.Game.Level + 0.1f);
            Assert.AreEqual(SimonPhase.Input, bootstrap.Game.Phase);
            bootstrap.Step(SimonRules.InputTimeoutSeconds);
            Assert.AreEqual(SimonPhase.GameOver, bootstrap.Game.Phase);
            Assert.AreEqual(1, bootstrap.Game.BestLevel);

            // 重开一局回到待开始，等级与进度复位，最长纪录跨局保留。
            bootstrap.StartGame();
            Assert.AreEqual(SimonPhase.Ready, bootstrap.Game.Phase);
            Assert.AreEqual(0, bootstrap.Game.Level);
            Assert.AreEqual(-1, bootstrap.Game.LitPadIndex);
            Assert.AreEqual(1, bootstrap.Game.BestLevel);
        }
    }
}
