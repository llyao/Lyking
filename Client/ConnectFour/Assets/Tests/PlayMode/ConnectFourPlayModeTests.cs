using System.Collections;
using Lyking.ConnectFour.Core;
using Lyking.ConnectFour.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.ConnectFour.PlayModeTests
{
    public sealed class ConnectFourPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("ConnectFourDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<ConnectFourDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasMatch);
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(Cell.Red, bootstrap.Match.Board.CurrentTurn);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
            Assert.AreEqual(ConnectFourRules.Columns / 2, bootstrap.SelectedColumn);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 玩家落子触发AI应答且重开复位棋盘()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("ConnectFourDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<ConnectFourDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            bootstrap!.PlayColumn(3);
            Assert.AreEqual(Cell.Red, bootstrap.Match.Board[3, 0]);
            Assert.AreEqual(Cell.Yellow, bootstrap.Match.Board.CurrentTurn);

            // AI 在下一帧的 Update 中立即应答。
            yield return null;
            Assert.AreEqual(1, CountStones(bootstrap.Match.Board.Cells, Cell.Red));
            Assert.AreEqual(1, CountStones(bootstrap.Match.Board.Cells, Cell.Yellow));
            Assert.AreEqual(Cell.Red, bootstrap.Match.Board.CurrentTurn);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(2, bootstrap.Match.Round);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
        }

        private static int CountStones(Cell[,] cells, Cell cell)
        {
            int count = 0;
            for (int col = 0; col < cells.GetLength(0); col++)
            {
                for (int row = 0; row < cells.GetLength(1); row++)
                {
                    if (cells[col, row] == cell) count++;
                }
            }

            return count;
        }
    }
}
