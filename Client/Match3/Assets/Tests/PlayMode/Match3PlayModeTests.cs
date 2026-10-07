using System.Collections;
using Lyking.Match3.Core;
using Lyking.Match3.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Match3.PlayModeTests
{
    public sealed class Match3PlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Match3Demo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<Match3DemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(Match3Rules.CellCount, bootstrap.Board.CellCount);
            Assert.AreEqual(Match3Rules.TotalMoves, bootstrap.Board.MovesLeft);
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.AreEqual(0, bootstrap.Board.Score);
            Assert.IsFalse(bootstrap.Board.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
            Assert.GreaterOrEqual(bootstrap.CursorIndex, 0);
            Assert.AreEqual(-1, bootstrap.SelectedIndex);
        }

        [UnityTest]
        public IEnumerator 选中两格交换推进结算且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Match3Demo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<Match3DemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 穷举相邻对执行第一个有效交换，同时验证核心推进与计分。
            var (first, second) = FindValidSwap(bootstrap!);
            Assert.AreEqual(1, bootstrap.Board.Moves);
            Assert.Greater(bootstrap.Board.Score, 0);

            // 用选中入口完成第二次交换：首次点击选中、再点异格交换。
            var (third, fourth) = FindValidSwap(bootstrap);
            bootstrap.Select(third);
            Assert.AreEqual(third, bootstrap.SelectedIndex);
            bootstrap.Select(fourth);
            Assert.AreEqual(-1, bootstrap.SelectedIndex);
            Assert.AreEqual(2, bootstrap.Board.Moves);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.AreEqual(0, bootstrap.Board.Score);
            Assert.AreEqual(Match3Rules.TotalMoves, bootstrap.Board.MovesLeft);
            Assert.IsFalse(bootstrap.Board.IsFinished);
            Assert.AreEqual(-1, bootstrap.SelectedIndex);
        }

        /// <summary>穷举相邻对试交换并执行第一个有效交换（无效交换由核心回退且无副作用）。</summary>
        private static (int First, int Second) FindValidSwap(Match3DemoBootstrap bootstrap)
        {
            var board = bootstrap.Board;
            for (int index = 0; index < board.CellCount; index++)
            {
                if (index % Match3Rules.Columns < Match3Rules.Columns - 1 && bootstrap.Swap(index, index + 1))
                {
                    return (index, index + 1);
                }

                if (index / Match3Rules.Columns < Match3Rules.Rows - 1 && bootstrap.Swap(index, index + Match3Rules.Columns))
                {
                    return (index, index + Match3Rules.Columns);
                }
            }

            if (bootstrap.Shuffle()) return FindValidSwap(bootstrap);
            Assert.Fail("初始棋盘重排后仍应存在可行交换。");
            return (0, 1);
        }
    }
}
