using System.Collections;
using Lyking.DungeonFighter.Core;
using Lyking.DungeonFighter.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.DungeonFighter.PlayModeTests
{
    public sealed class DungeonFighterPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("DungeonFighterDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<DungeonFighterBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(GamePhase.InProgress, bootstrap.Game.Phase);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进对局且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("DungeonFighterDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<DungeonFighterBootstrap>();
            Assert.IsNotNull(bootstrap);

            float xBefore = bootstrap!.Game.Player.Position.X;
            var right = new PlayerInputFrame { MoveX = 1f };
            for (int i = 0; i < 30; i++) bootstrap.Step(1f / 60f, right);
            Assert.Greater(bootstrap.Game.Player.Position.X, xBefore);
            Assert.Greater(bootstrap.Game.ElapsedSeconds, 0.4f);

            bootstrap.StartGame();
            Assert.AreEqual(0f, bootstrap.Game.ElapsedSeconds, 0.0001f);
            Assert.AreEqual(0, bootstrap.Game.RoomIndex);
            Assert.AreEqual(2.5f, bootstrap.Game.Player.Position.X, 0.01f);
        }
    }
}
