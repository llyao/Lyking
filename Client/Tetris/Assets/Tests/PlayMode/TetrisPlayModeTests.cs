using System.Collections;
using Lyking.Tetris.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Tetris.PlayModeTests
{
    public sealed class TetrisPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("TetrisDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TetrisDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(10, bootstrap.Board.Width);
            Assert.AreEqual(20, bootstrap.Board.Height);
            Assert.AreEqual(4, bootstrap.Board.PieceCells.Count);
            Assert.IsFalse(bootstrap.Board.IsFinished);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进下落且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("TetrisDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TetrisDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            int yBefore = bootstrap!.Board.PieceOrigin.Y;
            int ticksBefore = bootstrap.Board.TickIndex;
            bootstrap.Step(1f);
            Assert.Less(bootstrap.Board.PieceOrigin.Y, yBefore);
            Assert.Greater(bootstrap.Board.TickIndex, ticksBefore);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.TickIndex);
            Assert.AreEqual(0, bootstrap.Board.Score);
            Assert.AreEqual(1, bootstrap.Board.Level);
            Assert.IsFalse(bootstrap.Board.IsFinished);
        }
    }
}
