using System;

namespace Lyking.Sudoku.Core
{
    /// <summary>
    /// 数独求解器：回溯搜索，空格按行优先取第一个、候选按 1 到 9 升序尝试，
    /// 因此对同一盘面返回的解唯一确定（多解时为行优先字典序最小解）。
    /// </summary>
    public static class SudokuSolver
    {
        /// <summary>
        /// 求解盘面：可解时把解写入 <paramref name="solution"/> 并返回 true；
        /// 盘面自身存在行列宫重复或无解时返回 false。
        /// </summary>
        public static bool TrySolve(int[,] puzzle, int[,] solution)
        {
            ValidateGrid(puzzle, nameof(puzzle));
            ValidateGrid(solution, nameof(solution));
            if (!IsConsistent(puzzle)) return false;

            var board = (int[,])puzzle.Clone();
            if (!Solve(board)) return false;

            Array.Copy(board, solution, board.Length);
            return true;
        }

        /// <summary>检查在 (row, column) 放置 value 是否与盘面其他已填格冲突；忽略该格自身当前值。</summary>
        public static bool IsPlacementValid(int[,] grid, int row, int column, int value)
        {
            for (int i = 0; i < SudokuRules.Size; i++)
            {
                if (i != column && grid[row, i] == value) return false;
                if (i != row && grid[i, column] == value) return false;
            }

            int boxRow = row / SudokuRules.BoxSize * SudokuRules.BoxSize;
            int boxColumn = column / SudokuRules.BoxSize * SudokuRules.BoxSize;
            for (int r = boxRow; r < boxRow + SudokuRules.BoxSize; r++)
            {
                for (int c = boxColumn; c < boxColumn + SudokuRules.BoxSize; c++)
                {
                    if ((r != row || c != column) && grid[r, c] == value) return false;
                }
            }

            return true;
        }

        /// <summary>盘面上已填格之间是否无行列宫重复；0 视为空格跳过。</summary>
        public static bool IsConsistent(int[,] grid)
        {
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    int value = grid[row, column];
                    if (value != 0 && !IsPlacementValid(grid, row, column, value)) return false;
                }
            }

            return true;
        }

        /// <summary>回溯求解：取第一个空格按 1 到 9 升序尝试，无空格即已填满成功。</summary>
        private static bool Solve(int[,] board)
        {
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (board[row, column] != 0) continue;

                    for (int value = 1; value <= SudokuRules.Size; value++)
                    {
                        if (!IsPlacementValid(board, row, column, value)) continue;

                        board[row, column] = value;
                        if (Solve(board)) return true;
                        board[row, column] = 0;
                    }

                    return false;
                }
            }

            return true;
        }

        private static void ValidateGrid(int[,] grid, string paramName)
        {
            if (grid is null) throw new ArgumentNullException(paramName);
            if (grid.GetLength(0) != SudokuRules.Size || grid.GetLength(1) != SudokuRules.Size)
                throw new ArgumentException($"盘面必须是 {SudokuRules.Size}x{SudokuRules.Size}。", paramName);

            foreach (int value in grid)
            {
                if (value < 0 || value > SudokuRules.Size)
                    throw new ArgumentException($"盘面数值 {value} 超出 0 到 {SudokuRules.Size} 的范围。", paramName);
            }
        }
    }
}
