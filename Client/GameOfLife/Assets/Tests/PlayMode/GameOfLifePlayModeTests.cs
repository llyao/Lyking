using System.Collections;
using Lyking.GameOfLife.Core;
using Lyking.GameOfLife.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.GameOfLife.PlayModeTests
{
    public sealed class GameOfLifePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GameOfLifeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<GameOfLifeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(LifeRules.DefaultWidth, bootstrap.Board.Width);
            Assert.AreEqual(LifeRules.DefaultHeight, bootstrap.Board.Height);
            Assert.Greater(bootstrap.Board.AliveCount, 0);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 单步与节拍推进且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GameOfLifeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<GameOfLifeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 先暂停冻结固定节拍，断言不受帧间自动推进干扰。
            bootstrap!.SetPaused(true);
            var board = bootstrap.Board;
            int baseline = board.Generation;

            bootstrap.StepOnce();
            Assert.AreEqual(baseline + 1, board.Generation);

            bootstrap.SetPaused(false);
            bootstrap.Step(1f);                     // 1 秒按 0.125 秒/代 = 恰好 8 代
            Assert.AreEqual(baseline + 9, board.Generation);

            bootstrap.SetPaused(true);
            bootstrap.Step(1f);                     // 暂停时节拍不推进
            Assert.AreEqual(baseline + 9, board.Generation);
            bootstrap.StepOnce();                   // 单步不受暂停限制
            Assert.AreEqual(baseline + 10, board.Generation);

            bootstrap.StartGame();                  // 重开：新棋局代数归零、恢复运行
            Assert.AreEqual(0, bootstrap.Board.Generation);
            Assert.Greater(bootstrap.Board.AliveCount, 0);
            Assert.IsFalse(bootstrap.Board.IsPaused);
            Assert.AreEqual(LifeRules.DefaultWidth, bootstrap.Board.Width);
        }
    }
}
