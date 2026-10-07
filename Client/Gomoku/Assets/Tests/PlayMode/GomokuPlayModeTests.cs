using System.Collections;
using Lyking.Gomoku.Core;
using Lyking.Gomoku.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Gomoku.PlayModeTests
{
    public sealed class GomokuPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GomokuDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<GomokuDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasMatch);
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board.CurrentTurn);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
            Assert.AreEqual(GomokuRules.Center, bootstrap.SelectedRow);
            Assert.AreEqual(GomokuRules.Center, bootstrap.SelectedCol);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 玩家落子触发AI应答且重开复位棋盘()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("GomokuDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<GomokuDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            bootstrap!.PlayAt(7, 7);
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board[7, 7]);
            Assert.AreEqual(Cell.White, bootstrap.Match.Board.CurrentTurn);

            // AI 在下一帧的 Update 中立即应答。
            yield return null;
            Assert.AreEqual(1, CountStones(bootstrap.Match.Board.Cells, Cell.Black));
            Assert.AreEqual(1, CountStones(bootstrap.Match.Board.Cells, Cell.White));
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board.CurrentTurn);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(2, bootstrap.Match.Round);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
        }

        private static int CountStones(Cell[,] cells, Cell cell)
        {
            int count = 0;
            for (int row = 0; row < cells.GetLength(0); row++)
            {
                for (int col = 0; col < cells.GetLength(1); col++)
                {
                    if (cells[row, col] == cell) count++;
                }
            }

            return count;
        }
    }
}
