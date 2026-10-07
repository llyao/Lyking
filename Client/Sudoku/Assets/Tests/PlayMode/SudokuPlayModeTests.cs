using System;
using System.Collections;
using Lyking.Sudoku.Core;
using Lyking.Sudoku.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Sudoku.PlayModeTests
{
    public sealed class SudokuPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SudokuDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SudokuDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasPuzzle);
            Assert.Greater(bootstrap.Puzzle.ClueCount, 0);
            Assert.Greater(bootstrap.Puzzle.EmptyCount, 0);
            Assert.IsFalse(bootstrap.Puzzle.IsSolved);
            Assert.AreEqual(0, bootstrap.Puzzle.MoveCount);
            Assert.AreEqual(0, bootstrap.CursorRow);
            Assert.AreEqual(0, bootstrap.CursorColumn);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 填数推进后重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SudokuDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SudokuDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 在第一个空格填入正确答案，核心正常推进。
            var puzzle = bootstrap!.Puzzle;
            (int row, int column) = FirstEmpty(puzzle);
            int value = puzzle.Solution[row, column];
            Assert.IsTrue(bootstrap.Fill(row, column, value));
            Assert.AreEqual(value, puzzle.ValueAt(row, column));
            Assert.AreEqual(1, puzzle.MoveCount);
            Assert.AreEqual(SudokuEventType.Filled, puzzle.Events[puzzle.Events.Count - 1].Type);

            // 重开一局：玩家数全部清空，步数与光标复位。
            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Puzzle.MoveCount);
            Assert.AreEqual(81 - bootstrap.Puzzle.ClueCount, bootstrap.Puzzle.EmptyCount);
            Assert.IsFalse(bootstrap.Puzzle.IsSolved);
            Assert.AreEqual(0, bootstrap.CursorRow);
            Assert.AreEqual(0, bootstrap.CursorColumn);
            foreach (int playerValue in bootstrap.Puzzle.Player) Assert.AreEqual(0, playerValue);
        }

        /// <summary>行优先找出第一个空格。</summary>
        private static (int Row, int Column) FirstEmpty(SudokuPuzzle puzzle)
        {
            int[,] given = puzzle.Given;
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (given[row, column] == 0) return (row, column);
                }
            }

            throw new InvalidOperationException("谜题没有空格。");
        }
    }
}
