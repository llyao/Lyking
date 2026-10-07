using System;
using System.Collections;
using Lyking.LightsOut.Core;
using Lyking.LightsOut.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.LightsOut.PlayModeTests
{
    public sealed class LightsOutPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("LightsOutDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<LightsOutDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(LightsOutRules.BoardSize, bootstrap.Board.Size);
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.Greater(bootstrap.Board.LightCount, 0);
            Assert.IsFalse(bootstrap.Board.IsSolved);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口按下翻转十字且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("LightsOutDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<LightsOutDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            var board = bootstrap!.Board;
            bool[,] before = board.Lights;
            int center = LightsOutRules.BoardSize / 2;

            Assert.IsTrue(bootstrap.Step(center, center));
            Assert.AreEqual(1, board.Moves);
            Assert.AreEqual(5, board.Events[^1].FlippedCount);
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    bool inCross = Math.Abs(row - center) + Math.Abs(column - center) <= 1;
                    Assert.AreEqual(inCross ? !before[row, column] : before[row, column], board.IsLit(row, column));
                }
            }

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.AreEqual(LightsOutRules.BoardSize, bootstrap.Board.Size);
            Assert.AreEqual(1, bootstrap.Board.PuzzleNumber);
            Assert.IsFalse(bootstrap.Board.IsSolved);
        }
    }
}
