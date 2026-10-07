using System.Collections;
using System.Collections.Generic;
using Lyking.GameplayAbilities.Core;
using Lyking.GameplayAbilities.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.GameplayAbilities.PlayModeTests
{
    public sealed class GasDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景施法消耗法力并在冷却后恢复()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GasDemo");
            yield return load;
            yield return null;

            var demo = Object.FindObjectOfType<GasDemoBootstrap>();
            Assert.IsNotNull(demo);
            Assert.AreEqual(100, demo!.PlayerActor.Mana);
            Assert.AreEqual(100, demo.TrainingDummyActor.Health);

            Assert.IsTrue(demo.ProcessInput(KeyCode.Space));
            Assert.AreEqual(80, demo.PlayerActor.Mana);
            Assert.AreEqual(75, demo.TrainingDummyActor.Health);
            Assert.IsFalse(demo.ProcessInput(KeyCode.Space));

            yield return new WaitForSeconds(2.1f);
            Assert.IsTrue(demo.ProcessInput(KeyCode.Space));
            Assert.AreEqual(50, demo.TrainingDummyActor.Health);
        }

        [UnityTest]
        public IEnumerator 非施法输入不会改变场景状态()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GasDemo");
            yield return load;
            yield return null;

            var demo = Object.FindObjectOfType<GasDemoBootstrap>();
            Assert.IsNotNull(demo);
            Assert.IsFalse(demo!.ProcessInput(KeyCode.Return));
            Assert.AreEqual(100, demo.PlayerActor.Mana);
            Assert.AreEqual(100, demo.TrainingDummyActor.Health);
        }

        [UnityTest]
        public IEnumerator 场景销毁后不会抛出异常()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GasDemo");
            yield return load;
            yield return null;
            var demo = Object.FindObjectOfType<GasDemoBootstrap>();
            Assert.IsNotNull(demo);
            Object.Destroy(demo!.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator 训练假人的活动效果由自身时钟到期撤销()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GasDemo");
            yield return load;
            yield return null;

            var demo = Object.FindObjectOfType<GasDemoBootstrap>();
            Assert.IsNotNull(demo);
            var tags = new GameplayTagContainer();
            tags.Add("State.Burning");
            var result = demo!.TrainingDummyActor.AbilitySystem.TryApplyEffect(
                new GameplayEffectSpec(
                    new Dictionary<string, double> { ["Health"] = -10 },
                    grantedTags: tags,
                    durationSeconds: 1),
                "PlayModeBurn");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(90, demo.TrainingDummyActor.Health);
            Assert.IsTrue(demo.TrainingDummyActor.AbilitySystem.HasTag("State.Burning"));
            Assert.AreEqual(1, demo.TrainingDummyActor.AbilitySystem.GetActiveEffects().Count);

            demo.TrainingDummyActor.Tick(1);
            yield return null;

            Assert.AreEqual(100, demo.TrainingDummyActor.Health);
            Assert.IsFalse(demo.TrainingDummyActor.AbilitySystem.HasTag("State.Burning"));
            Assert.AreEqual(0, demo.TrainingDummyActor.AbilitySystem.GetActiveEffects().Count);
        }
    }
}
