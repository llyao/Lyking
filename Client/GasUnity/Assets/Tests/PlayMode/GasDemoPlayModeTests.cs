using System.Collections;
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

            Assert.IsTrue(demo.TryCastFireball().Succeeded);
            Assert.AreEqual(80, demo.PlayerActor.Mana);
            Assert.AreEqual(75, demo.TrainingDummyActor.Health);
            Assert.IsFalse(demo.TryCastFireball().Succeeded);

            yield return new WaitForSeconds(2.1f);
            Assert.IsTrue(demo.TryCastFireball().Succeeded);
            Assert.AreEqual(50, demo.TrainingDummyActor.Health);
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
    }
}
