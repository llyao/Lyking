using System.Collections;
using Lyking.Bomberman.Core;
using Lyking.Bomberman.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Bomberman.PlayModeTests
{
    public sealed class BombermanPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后战局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BombermanDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BombermanDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(1, bootstrap.Game.Level);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.AreEqual(new GridPos(1, 1), bootstrap.Game.Player);
            Assert.IsFalse(bootstrap.Game.IsGameOver);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进炸弹爆炸且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("BombermanDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<BombermanDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 放置炸弹并推进两个半秒刻度，引信到期爆炸。
            bootstrap!.Step(0.25f, new BombInput(BombermanDir.None, true));
            Assert.IsTrue(bootstrap.Game.IsBombAt(new GridPos(1, 1)));

            bootstrap.Step(1.25f, default);
            bootstrap.Step(1.25f, default);
            Assert.AreEqual(0, bootstrap.Game.Bombs.Count);
            var exploded = false;
            foreach (var e in bootstrap.Game.Events)
            {
                if (e.Type == BombermanEventType.Explosion) exploded = true;
            }

            Assert.IsTrue(exploded);

            // 重开一局：分数、生命与炸弹全部复位。
            bootstrap.StartGame();
            Assert.AreEqual(1, bootstrap.Game.Level);
            Assert.AreEqual(0, bootstrap.Game.Score);
            Assert.AreEqual(3, bootstrap.Game.Lives);
            Assert.AreEqual(new GridPos(1, 1), bootstrap.Game.Player);
            Assert.AreEqual(0, bootstrap.Game.Bombs.Count);
            Assert.IsFalse(bootstrap.Game.IsGameOver);
        }
    }
}
