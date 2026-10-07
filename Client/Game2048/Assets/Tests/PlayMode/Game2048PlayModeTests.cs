using System.Collections;
using Lyking.Game2048.Core;
using Lyking.Game2048.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Game2048.PlayModeTests
{
    public sealed class Game2048PlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且界面已构建()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Game2048");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<Game2048Bootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(bootstrap!.Game);
            Assert.AreEqual(GameStatus.Playing, bootstrap.Game.Status);
            Assert.AreEqual(2, bootstrap.Game.Board.CountNonEmpty);
            Assert.IsNotNull(bootstrap.View);
        }

        [UnityTest]
        public IEnumerator 滑动输入驱动对局且重新开始复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Game2048");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<Game2048Bootstrap>();
            Assert.IsNotNull(bootstrap);

            int tilesBefore = bootstrap!.Game.Board.CountNonEmpty;
            bool changed = false;
            foreach (MoveDirection direction in new[] { MoveDirection.Left, MoveDirection.Right, MoveDirection.Up, MoveDirection.Down })
            {
                if (bootstrap.ProcessInput(direction))
                {
                    changed = true;
                    break;
                }
            }

            Assert.IsTrue(changed);
            Assert.AreEqual(tilesBefore + 1, bootstrap.Game.Board.CountNonEmpty);

            bootstrap.Restart();
            Assert.AreEqual(2, bootstrap.Game.Board.CountNonEmpty);
            Assert.AreEqual(0, bootstrap.Game.Score);
        }
    }
}
