using System.Collections;
using System.Collections.Generic;
using Lyking.TicTacToe.Core;
using Lyking.TicTacToe.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.TicTacToe.PlayModeTests
{
    public sealed class TicTacToePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("TicTacToeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TicTacToeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasMatch);
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(Cell.X, bootstrap.Match.Board.CurrentTurn);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 玩家落子触发AI应答且重开复位棋盘()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("TicTacToeDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<TicTacToeDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            bootstrap!.PlayCell(4);
            Assert.AreEqual(Cell.X, bootstrap.Match.Board.Cells[4]);
            Assert.AreEqual(Cell.O, bootstrap.Match.Board.CurrentTurn);

            // AI 在下一帧的 Update 中立即应答。
            yield return null;
            Assert.AreEqual(1, CountCells(bootstrap.Match.Board.Cells, Cell.X));
            Assert.AreEqual(1, CountCells(bootstrap.Match.Board.Cells, Cell.O));
            Assert.AreEqual(Cell.X, bootstrap.Match.Board.CurrentTurn);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(2, bootstrap.Match.Round);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
        }

        private static int CountCells(IReadOnlyList<Cell> cells, Cell cell)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell) count++;
            }

            return count;
        }
    }
}
