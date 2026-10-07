using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Sudoku.Core;
using Xunit;

namespace Lyking.Sudoku.Core.Tests
{
    /// <summary>数独核心规则测试：生成合法性与难度、填数与冲突、清除、锁定、胜负、求解器、提示、重置与确定性。</summary>
    public class SudokuPuzzleTests
    {
        [Fact]
        public void 完整解的行列与宫均为一到九且不重复()
        {
            foreach (int seed in new[] { 1, 7, 42 })
            {
                int[,] solution = new SudokuPuzzle(seed, SudokuDifficulty.Easy).Solution;

                for (int i = 0; i < SudokuRules.Size; i++)
                {
                    Assert.True(IsPermutationOfOneToNine(RowOf(solution, i)), $"种子 {seed} 解第 {i} 行不是 1-9 排列。");
                    Assert.True(IsPermutationOfOneToNine(ColumnOf(solution, i)), $"种子 {seed} 解第 {i} 列不是 1-9 排列。");
                    Assert.True(IsPermutationOfOneToNine(BoxOf(solution, i)), $"种子 {seed} 解第 {i} 宫不是 1-9 排列。");
                }
            }
        }

        [Fact]
        public void 同种子生成完全一致的谜题()
        {
            var first = SudokuGenerator.Generate(2026, SudokuDifficulty.Hard);
            var second = SudokuGenerator.Generate(2026, SudokuDifficulty.Hard);

            Assert.Equal(first.ClueCount, second.ClueCount);
            Assert.Equal(Flatten(first.Given), Flatten(second.Given));
            Assert.Equal(Flatten(first.Solution), Flatten(second.Solution));

            // 棋局层面同样一致：同种子同难度构建的棋局给定格完全相同。
            var puzzleA = new SudokuPuzzle(2026, SudokuDifficulty.Hard);
            var puzzleB = new SudokuPuzzle(2026, SudokuDifficulty.Hard);
            Assert.Equal(Flatten(puzzleA.Given), Flatten(puzzleB.Given));
            Assert.Equal(Flatten(first.Given), Flatten(puzzleA.Given));
        }

        [Fact]
        public void 各难度提示数落在难度表区间内()
        {
            var difficulties = new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard };
            foreach (int seed in new[] { 3, 11, 47, 88 })
            {
                foreach (var difficulty in difficulties)
                {
                    int clueCount = SudokuGenerator.Generate(seed, difficulty).ClueCount;
                    int target = SudokuRules.ClueCount(difficulty);
                    Assert.InRange(clueCount, target - SudokuRules.ClueTolerance, target + SudokuRules.ClueTolerance);
                }
            }
        }

        [Fact]
        public void 生成谜题可解且解与生成解一致()
        {
            var difficulties = new[] { SudokuDifficulty.Easy, SudokuDifficulty.Medium, SudokuDifficulty.Hard };
            foreach (int seed in new[] { 5, 23, 64 })
            {
                foreach (var difficulty in difficulties)
                {
                    var data = SudokuGenerator.Generate(seed, difficulty);
                    var solved = new int[SudokuRules.Size, SudokuRules.Size];

                    Assert.True(SudokuSolver.TrySolve(data.Given, solved), $"种子 {seed} 的 {difficulty} 谜题无解。");
                    Assert.Equal(Flatten(data.Solution), Flatten(solved));
                }
            }
        }

        [Fact]
        public void 填数成功并记录事件与状态()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstEmpty(puzzle);
            int value = puzzle.Solution[row, column];
            int emptyBefore = puzzle.EmptyCount;

            Assert.True(puzzle.Fill(row, column, value));

            Assert.Equal(value, puzzle.PlayerValue(row, column));
            Assert.Equal(value, puzzle.ValueAt(row, column));
            Assert.False(puzzle.IsGiven(row, column));
            Assert.Equal(emptyBefore - 1, puzzle.EmptyCount);
            Assert.Equal(1, puzzle.MoveCount);
            Assert.Equal(SudokuEventType.Filled, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 同行重复填数产生冲突()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int columnA, int columnB) = FindEmptyPairInSameRow(puzzle);

            Assert.True(puzzle.Fill(row, columnA, 5));
            Assert.True(puzzle.Fill(row, columnB, 5));

            Assert.True(puzzle.IsConflict(row, columnA));
            Assert.True(puzzle.IsConflict(row, columnB));
            Assert.True(puzzle.Conflicts[row, columnA]);
            Assert.True(puzzle.Conflicts[row, columnB]);
            Assert.Contains(puzzle.Events, e => e.Type == SudokuEventType.Conflict);
        }

        [Fact]
        public void 同列重复填数产生冲突()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int rowA, int rowB, int column) = FindEmptyPairInSameColumn(puzzle);

            Assert.True(puzzle.Fill(rowA, column, 3));
            Assert.True(puzzle.Fill(rowB, column, 3));

            Assert.True(puzzle.IsConflict(rowA, column));
            Assert.True(puzzle.IsConflict(rowB, column));
            Assert.Contains(puzzle.Events, e => e.Type == SudokuEventType.Conflict);
        }

        [Fact]
        public void 同宫不同行列重复填数产生冲突()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int rowA, int columnA, int rowB, int columnB) = FindEmptyPairInSameBox(puzzle);

            Assert.True(puzzle.Fill(rowA, columnA, 8));
            Assert.True(puzzle.Fill(rowB, columnB, 8));

            Assert.True(puzzle.IsConflict(rowA, columnA));
            Assert.True(puzzle.IsConflict(rowB, columnB));
            Assert.Contains(puzzle.Events, e => e.Type == SudokuEventType.Conflict);
        }

        [Fact]
        public void 清除玩家数恢复为空且清除给定格被拒()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstEmpty(puzzle);
            puzzle.Fill(row, column, 3);

            Assert.True(puzzle.Clear(row, column));

            Assert.Equal(0, puzzle.PlayerValue(row, column));
            Assert.Equal(0, puzzle.ValueAt(row, column));
            Assert.Equal(2, puzzle.MoveCount);
            Assert.Equal(SudokuEventType.Cleared, puzzle.Events[^1].Type);

            // 给定格与空格无可清除内容，一律拒绝。
            (int givenRow, int givenColumn) = FirstGiven(puzzle);
            Assert.False(puzzle.Clear(givenRow, givenColumn));
            Assert.False(puzzle.Clear(row, column));
        }

        [Fact]
        public void 给定格锁定填充被拒绝()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstGiven(puzzle);
            int givenValue = puzzle.ValueAt(row, column);

            Assert.False(puzzle.Fill(row, column, givenValue % 9 + 1));

            Assert.Equal(givenValue, puzzle.ValueAt(row, column));
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(SudokuEventType.NewPuzzle, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 全部填对即通关并记录完成事件()
        {
            var puzzle = new SudokuPuzzle(11, SudokuDifficulty.Easy);
            FillAllCorrect(puzzle);

            Assert.True(puzzle.IsSolved);
            Assert.False(puzzle.IsEmptyLeft);
            Assert.Equal(0, puzzle.EmptyCount);
            Assert.Equal(SudokuEventType.Completed, puzzle.Events[^1].Type);
            Assert.False(puzzle.Hint());
        }

        [Fact]
        public void 填满但存在重复错误不判胜()
        {
            var puzzle = new SudokuPuzzle(11, SudokuDifficulty.Easy);
            (int row, int columnA, int columnB) = FindEmptyPairInSameRow(puzzle);
            int valueA = puzzle.Solution[row, columnA];
            int valueB = puzzle.Solution[row, columnB];
            int wrong = Enumerable.Range(1, 9).First(v => v != valueA && v != valueB);

            FillSolutionWith(puzzle, (row, columnA), wrong, (row, columnB), wrong);

            Assert.Equal(0, puzzle.EmptyCount);
            Assert.False(puzzle.IsSolved);
            Assert.True(puzzle.IsConflict(row, columnA));
            Assert.DoesNotContain(puzzle.Events, e => e.Type == SudokuEventType.Completed);
        }

        [Fact]
        public void 求解器对无解盘面返回false并可解全空盘()
        {
            var solved = new int[SudokuRules.Size, SudokuRules.Size];

            // 给定格自身重复：第一行两处都是 5。
            var duplicated = new int[SudokuRules.Size, SudokuRules.Size];
            duplicated[0, 0] = 5;
            duplicated[0, 8] = 5;
            Assert.False(SudokuSolver.TrySolve(duplicated, solved));

            // 盘面无重复但无解：第一行缺 9 只能填 (0,8)，而第 8 列已有 9。
            var tricky = new int[SudokuRules.Size, SudokuRules.Size];
            for (int i = 0; i < 8; i++) tricky[0, i] = i + 1;
            tricky[1, 8] = 9;
            Assert.False(SudokuSolver.TrySolve(tricky, solved));

            // 全空盘面可解，解本身行列宫均合法。
            Assert.True(SudokuSolver.TrySolve(new int[SudokuRules.Size, SudokuRules.Size], solved));
            for (int i = 0; i < SudokuRules.Size; i++)
            {
                Assert.True(IsPermutationOfOneToNine(RowOf(solved, i)));
                Assert.True(IsPermutationOfOneToNine(ColumnOf(solved, i)));
                Assert.True(IsPermutationOfOneToNine(BoxOf(solved, i)));
            }

            Assert.Throws<ArgumentNullException>(() => SudokuSolver.TrySolve(null!, solved));
            Assert.Throws<ArgumentException>(() => SudokuSolver.TrySolve(new int[8, 8], solved));
        }

        [Fact]
        public void 提示在空格填入正确答案直至通关()
        {
            var puzzle = new SudokuPuzzle(9, SudokuDifficulty.Easy);
            int emptyBefore = puzzle.EmptyCount;

            Assert.True(puzzle.Hint(5));
            Assert.Equal(emptyBefore - 1, puzzle.EmptyCount);
            Assert.Equal(SudokuEventType.Hint, puzzle.Events[^1].Type);

            // 每一个被提示填入的格值都与解一致。
            int[,] given = puzzle.Given;
            int[,] player = puzzle.Player;
            int[,] solution = puzzle.Solution;
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (given[row, column] == 0 && player[row, column] != 0)
                        Assert.Equal(solution[row, column], player[row, column]);
                }
            }

            // 连续提示直到填满：提示填的总是正确答案，最终必然通关。
            while (puzzle.IsEmptyLeft) Assert.True(puzzle.Hint(5));
            Assert.True(puzzle.IsSolved);
            Assert.Equal(SudokuEventType.Completed, puzzle.Events[^1].Type);
            Assert.False(puzzle.Hint(5));
        }

        [Fact]
        public void 重置清空玩家数并保留给定格()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstEmpty(puzzle);
            puzzle.Fill(row, column, 3);
            puzzle.Hint(1);
            Assert.True(puzzle.MoveCount > 0);

            puzzle.Reset();

            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(81 - puzzle.ClueCount, puzzle.EmptyCount);
            Assert.Equal(SudokuEventType.Reset, puzzle.Events[^1].Type);
            foreach (int value in puzzle.Player) Assert.Equal(0, value);
            Assert.Equal(Flatten(new SudokuPuzzle(7, SudokuDifficulty.Easy).Given), Flatten(puzzle.Given));
            Assert.False(puzzle.IsSolved);
        }

        [Fact]
        public void 相同种子与操作序列两次模拟完全一致()
        {
            static string Simulate()
            {
                var puzzle = new SudokuPuzzle(2026, SudokuDifficulty.Medium);
                var cells = new[] { (0, 0), (4, 4), (8, 8), (2, 3), (6, 7) };
                foreach (var (row, column) in cells)
                {
                    puzzle.Fill(row, column, puzzle.Solution[row, column]);
                }

                puzzle.Hint(3);
                puzzle.Clear(4, 4);
                return $"{Flatten(puzzle.Player)}|{puzzle.MoveCount}|{puzzle.Events.Count}|{puzzle.EmptyCount}";
            }

            Assert.Equal(Simulate(), Simulate());
        }

        [Fact]
        public void 事件日志封顶一百二十八条裁剪()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);
            (int row, int column) = FirstEmpty(puzzle);

            for (int i = 0; i < 300; i++) puzzle.Fill(row, column, i % 9 + 1);

            Assert.Equal(128, puzzle.Events.Count);
        }

        [Fact]
        public void 越界或非法填数抛出异常()
        {
            var puzzle = new SudokuPuzzle(7, SudokuDifficulty.Easy);

            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.Fill(-1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.Fill(0, 9, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.Fill(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.Fill(0, 0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.Clear(9, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.PlayerValue(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => puzzle.ValueAt(0, -1));
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

        /// <summary>找出同一列内的一对空格；简单难度提示数有限，必然存在。</summary>
        private static (int RowA, int RowB, int Column) FindEmptyPairInSameColumn(SudokuPuzzle puzzle)
        {
            int[,] given = puzzle.Given;
            for (int column = 0; column < SudokuRules.Size; column++)
            {
                int? first = null;
                for (int row = 0; row < SudokuRules.Size; row++)
                {
                    if (given[row, column] != 0) continue;

                    if (first is int firstRow) return (firstRow, row, column);
                    first = row;
                }
            }

            throw new InvalidOperationException("没有找到同一列内的一对空格。");
        }

        /// <summary>找出同一宫内行列均不同的一对空格；简单难度提示数有限，必然存在。</summary>
        private static (int RowA, int ColumnA, int RowB, int ColumnB) FindEmptyPairInSameBox(SudokuPuzzle puzzle)
        {
            int[,] given = puzzle.Given;
            for (int boxRow = 0; boxRow < SudokuRules.Size; boxRow += SudokuRules.BoxSize)
            {
                for (int boxColumn = 0; boxColumn < SudokuRules.Size; boxColumn += SudokuRules.BoxSize)
                {
                    var empties = new List<(int Row, int Column)>();
                    for (int row = boxRow; row < boxRow + SudokuRules.BoxSize; row++)
                    {
                        for (int column = boxColumn; column < boxColumn + SudokuRules.BoxSize; column++)
                        {
                            if (given[row, column] == 0) empties.Add((row, column));
                        }
                    }

                    foreach (var (rowA, columnA) in empties)
                    {
                        foreach (var (rowB, columnB) in empties)
                        {
                            if (rowA != rowB && columnA != columnB) return (rowA, columnA, rowB, columnB);
                        }
                    }
                }
            }

            throw new InvalidOperationException("没有找到同一宫内行列均不同的一对空格。");
        }

        /// <summary>把全部空格按生成解填满。</summary>
        private static void FillAllCorrect(SudokuPuzzle puzzle)
        {
            FillSolutionWith(puzzle, (-1, -1), 0, (-1, -1), 0);
        }

        /// <summary>把全部空格按生成解填满，仅 cellA 与 cellB 两格改用指定值。</summary>
        private static void FillSolutionWith(
            SudokuPuzzle puzzle, (int Row, int Column) cellA, int valueA, (int Row, int Column) cellB, int valueB)
        {
            int[,] given = puzzle.Given;
            int[,] solution = puzzle.Solution;
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (given[row, column] != 0) continue;

                    int value = solution[row, column];
                    if ((row, column) == cellA) value = valueA;
                    if ((row, column) == cellB) value = valueB;
                    Assert.True(puzzle.Fill(row, column, value), $"填充 ({row},{column}) 意外失败。");
                }
            }
        }

        private static bool IsPermutationOfOneToNine(int[] values)
        {
            if (values.Length != SudokuRules.Size) return false;

            var seen = new bool[SudokuRules.Size + 1];
            foreach (int value in values)
            {
                if (value < 1 || value > SudokuRules.Size || seen[value]) return false;
                seen[value] = true;
            }

            return true;
        }

        private static int[] RowOf(int[,] grid, int row)
        {
            var values = new int[SudokuRules.Size];
            for (int column = 0; column < SudokuRules.Size; column++) values[column] = grid[row, column];
            return values;
        }

        private static int[] ColumnOf(int[,] grid, int column)
        {
            var values = new int[SudokuRules.Size];
            for (int row = 0; row < SudokuRules.Size; row++) values[row] = grid[row, column];
            return values;
        }

        private static int[] BoxOf(int[,] grid, int boxIndex)
        {
            int boxRow = boxIndex / SudokuRules.BoxSize * SudokuRules.BoxSize;
            int boxColumn = boxIndex % SudokuRules.BoxSize * SudokuRules.BoxSize;
            var values = new int[SudokuRules.Size];
            int index = 0;
            for (int row = boxRow; row < boxRow + SudokuRules.BoxSize; row++)
            {
                for (int column = boxColumn; column < boxColumn + SudokuRules.BoxSize; column++)
                {
                    values[index++] = grid[row, column];
                }
            }

            return values;
        }

        private static string Flatten(int[,] grid) => string.Join(',', grid.Cast<int>());
    }
}
