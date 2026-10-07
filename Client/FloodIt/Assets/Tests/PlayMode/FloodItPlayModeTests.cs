using System.Collections;
using Lyking.FloodIt.Core;
using Lyking.FloodIt.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.FloodIt.PlayModeTests
{
    public sealed class FloodItPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FloodItDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FloodItDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(FloodItRules.Rows, bootstrap.Board.Rows);
            Assert.AreEqual(FloodItRules.CellCount, bootstrap.Board.CellCount);
            Assert.AreEqual(0, bootstrap.Board.Steps);
            Assert.GreaterOrEqual(bootstrap.Board.FloodedCount, 1);
            Assert.IsFalse(bootstrap.Board.IsWon);
            Assert.IsFalse(bootstrap.Board.IsLost);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口选色推进且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FloodItDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<FloodItDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            var board = bootstrap!.Board;
            int pick = board.GetColor(0, 0) == 0 ? 1 : 0;
            Assert.IsTrue(bootstrap.Step(pick));
            Assert.AreEqual(1, board.Steps);
            Assert.AreEqual(pick, board.GetColor(0, 0));
            Assert.IsFalse(board.IsLost);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.Steps);
            Assert.AreEqual(FloodItRules.MaxMoves, bootstrap.Board.RemainingMoves);
            Assert.GreaterOrEqual(bootstrap.Board.FloodedCount, 1);
            Assert.IsFalse(bootstrap.Board.IsWon);
            Assert.IsFalse(bootstrap.Board.IsLost);

            bootstrap.NewPuzzle();
            Assert.AreEqual(0, bootstrap.Board.Steps);
            Assert.IsFalse(bootstrap.Board.IsWon);
            Assert.IsFalse(bootstrap.Board.IsLost);
        }
    }
}
