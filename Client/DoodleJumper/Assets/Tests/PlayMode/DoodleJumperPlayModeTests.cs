using System.Collections;
using Lyking.DoodleJumper.Core;
using Lyking.DoodleJumper.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.DoodleJumper.PlayModeTests
{
    public sealed class DoodleJumperPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("DoodleJumperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<DoodleJumperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.IsFalse(bootstrap.Game.IsFinished);
            Assert.AreEqual(JumperRules.Default.JumpSpeed, bootstrap.Game.VelocityY);
            Assert.AreEqual(0f, bootstrap.Game.MaxHeight);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.GreaterOrEqual(bootstrap.Game.Platforms.Count, 5);
            Assert.AreEqual(2.5f, bootstrap.Game.CameraCenterY, 0.001f);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进弹跳计分且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("DoodleJumperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<DoodleJumperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 无输入推进 1 秒（120 个子步）：玩家完成整段弹跳弧线，高度与分数随之增长。
            bootstrap!.Step(1f, JumperInput.None);
            Assert.AreEqual(120, bootstrap.Game.StepIndex);
            Assert.Greater(bootstrap.Game.MaxHeight, 2f);
            Assert.Greater(bootstrap.Game.Score, 0);
            Assert.IsFalse(bootstrap.Game.IsFinished);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Game.StepIndex);
            Assert.AreEqual(0f, bootstrap.Game.MaxHeight);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(JumperRules.Default.JumpSpeed, bootstrap.Game.VelocityY);
            Assert.GreaterOrEqual(bootstrap.Game.Platforms.Count, 5);
            Assert.IsFalse(bootstrap.Game.IsFinished);
        }
    }
}
