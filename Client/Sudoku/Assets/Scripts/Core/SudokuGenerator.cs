using System;

namespace Lyking.Sudoku.Core
{
    /// <summary>一次生成的产物：挖洞后的给定盘面、生成时的完整解与保留的提示数。</summary>
    public sealed class SudokuPuzzleData
    {
        public SudokuPuzzleData(int seed, SudokuDifficulty difficulty, int[,] given, int[,] solution, int clueCount)
        {
            Seed = seed;
            Difficulty = difficulty;
            Given = given;
            Solution = solution;
            ClueCount = clueCount;
        }

        /// <summary>生成所用的随机种子。</summary>
        public int Seed { get; }

        public SudokuDifficulty Difficulty { get; }

        /// <summary>挖洞后的给定盘面，0 表示空格。</summary>
        public int[,] Given { get; }

        /// <summary>生成时回溯填满得到的完整解。</summary>
        public int[,] Solution { get; }

        /// <summary>挖洞后保留的提示数（给定格数量）。</summary>
        public int ClueCount { get; }
    }

    /// <summary>
    /// 数独生成器：先用注入种子的随机数回溯填满整盘得到完整解（每格候选 1-9
    /// 经 Fisher-Yates 洗牌后按序尝试），再按难度把全部格位随机排列逐个尝试挖洞，
    /// 每次挖洞后用求解器确认仍然可解且解与原解一致，否则回填；不强制唯一解，
    /// 但同一格位一旦挖洞失败便永久失败，故一遍扫描即收敛，同种子结果完全一致。
    /// </summary>
    public static class SudokuGenerator
    {
        public static SudokuPuzzleData Generate(int seed, SudokuDifficulty difficulty)
        {
            int target = SudokuRules.ClueCount(difficulty);
            var random = new Random(seed);
            var solution = new int[SudokuRules.Size, SudokuRules.Size];
            var given = new int[SudokuRules.Size, SudokuRules.Size];

            if (!TryFill(solution, 0, random))
                throw new InvalidOperationException("数独全盘回溯填充失败，不应发生。");

            Array.Copy(solution, given, solution.Length);
            int clueCount = DigHoles(given, solution, target, random);
            return new SudokuPuzzleData(seed, difficulty, given, solution, clueCount);
        }

        /// <summary>按行优先逐格回溯填满整盘：候选值洗牌后按序尝试，保持种子确定性。</summary>
        private static bool TryFill(int[,] board, int cellIndex, Random random)
        {
            if (cellIndex == SudokuRules.Size * SudokuRules.Size) return true;

            int row = cellIndex / SudokuRules.Size;
            int column = cellIndex % SudokuRules.Size;
            var candidates = new int[SudokuRules.Size];
            for (int i = 0; i < candidates.Length; i++) candidates[i] = i + 1;
            Shuffle(candidates, random);

            foreach (int value in candidates)
            {
                if (!SudokuSolver.IsPlacementValid(board, row, column, value)) continue;

                board[row, column] = value;
                if (TryFill(board, cellIndex + 1, random)) return true;
                board[row, column] = 0;
            }

            return false;
        }

        /// <summary>Fisher-Yates 原地洗牌。</summary>
        private static void Shuffle(int[] items, Random random)
        {
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>
        /// 按随机排列逐格尝试挖洞：挖后必须可解且解与原解一致才保留，达到目标提示数即停，
        /// 返回最终保留的提示数（剩余格位均不可再挖时允许高于目标）。
        /// </summary>
        private static int DigHoles(int[,] given, int[,] solution, int target, Random random)
        {
            var positions = new int[SudokuRules.Size * SudokuRules.Size];
            for (int i = 0; i < positions.Length; i++) positions[i] = i;
            Shuffle(positions, random);

            var found = new int[SudokuRules.Size, SudokuRules.Size];
            int clueCount = positions.Length;
            foreach (int position in positions)
            {
                if (clueCount <= target) break;

                int row = position / SudokuRules.Size;
                int column = position % SudokuRules.Size;
                int backup = given[row, column];
                given[row, column] = 0;
                bool removable = SudokuSolver.TrySolve(given, found) && SameBoard(found, solution);
                if (removable) clueCount--;
                else given[row, column] = backup;
            }

            return clueCount;
        }

        private static bool SameBoard(int[,] left, int[,] right)
        {
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (left[row, column] != right[row, column]) return false;
                }
            }

            return true;
        }
    }
}
