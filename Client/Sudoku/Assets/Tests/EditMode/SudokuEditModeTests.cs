using System;
using Lyking.Sudoku.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Sudoku.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class SudokuEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建并填入一格()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);

            Assert.AreEqual(SudokuRules.Size, 9);
            Assert.Greater(puzzle.ClueCount, 0);
            Assert.AreEqual(81 - puzzle.ClueCount, puzzle.EmptyCount);
            Assert.IsFalse(puzzle.IsSolved);
            Assert.AreEqual(SudokuEventType.NewPuzzle, puzzle.Events[0].Type);

            (int row, int column) = FirstEmpty(puzzle);
            int value = puzzle.Solution[row, column];
            Assert.IsTrue(puzzle.Fill(row, column, value));
            Assert.AreEqual(value, puzzle.ValueAt(row, column));
            Assert.AreEqual(1, puzzle.MoveCount);
            Assert.AreEqual(SudokuEventType.Filled, puzzle.Events[puzzle.Events.Count - 1].Type);
        }

        [Test]
        public void 给定格填充被拒绝且盘面不变()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstGiven(puzzle);
            int givenValue = puzzle.ValueAt(row, column);

            Assert.IsFalse(puzzle.Fill(row, column, givenValue % 9 + 1));

            Assert.AreEqual(givenValue, puzzle.ValueAt(row, column));
            Assert.AreEqual(0, puzzle.MoveCount);
            Assert.AreEqual(81 - puzzle.ClueCount, puzzle.EmptyCount);
        }

        [Test]
        public void 同行重复填数产生冲突事件()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int columnA, int columnB) = FindEmptyPairInSameRow(puzzle);

            Assert.IsTrue(puzzle.Fill(row, columnA, 5));
            Assert.IsTrue(puzzle.Fill(row, columnB, 5));

            Assert.IsTrue(puzzle.IsConflict(row, columnA));
            Assert.IsTrue(puzzle.IsConflict(row, columnB));
            Assert.IsTrue(puzzle.Conflicts[row, columnA]);
            Assert.AreEqual(SudokuEventType.Conflict, puzzle.Events[puzzle.Events.Count - 1].Type);
        }

        [Test]
        public void 重置清空玩家填数并保留给定格()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstEmpty(puzzle);
            int[,] givenBefore = puzzle.Given;

            Assert.IsTrue(puzzle.Fill(row, column, 3));
            Assert.AreEqual(3, puzzle.PlayerValue(row, column));

            puzzle.Reset();

            Assert.AreEqual(0, puzzle.MoveCount);
            Assert.AreEqual(0, puzzle.PlayerValue(row, column));
            Assert.AreEqual(81 - puzzle.ClueCount, puzzle.EmptyCount);
            CollectionAssert.AreEqual(givenBefore, puzzle.Given);
            Assert.AreEqual(SudokuEventType.Reset, puzzle.Events[puzzle.Events.Count - 1].Type);
            Assert.IsFalse(puzzle.IsSolved);
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

        /// <summary>行优先找出第一个给定格。</summary>
        private static (int Row, int Column) FirstGiven(SudokuPuzzle puzzle)
        {
            int[,] given = puzzle.Given;
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (given[row, column] != 0) return (row, column);
                }
            }

            throw new InvalidOperationException("谜题没有给定格。");
        }

        /// <summary>找出同一行内的一对空格；简单难度提示数有限，必然存在。</summary>
        private static (int Row, int ColumnA, int ColumnB) FindEmptyPairInSameRow(SudokuPuzzle puzzle)
        {
            int[,] given = puzzle.Given;
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                int? first = null;
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (given[row, column] != 0) continue;

                    if (first is int firstColumn) return (row, firstColumn, column);
                    first = column;
                }
            }

            throw new InvalidOperationException("没有找到同一行内的一对空格。");
        }
    }
}
