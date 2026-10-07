using System;

namespace Lyking.Sudoku.Core
{
    /// <summary>难度档位，对应挖洞后保留的提示数：简单最多、困难最少。</summary>
    public enum SudokuDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    /// <summary>
    /// 数独规则常量：9x9 棋盘、3x3 宫划分，以及各难度保留的提示数表
    /// （简单 40、中等 32、困难 26，实际生成允许 ±2 格浮动）。核心与表现层共同引用。
    /// </summary>
    public static class SudokuRules
    {
        /// <summary>棋盘边长：9x9。</summary>
        public const int Size = 9;

        /// <summary>宫的边长：棋盘划分为 3x3 个宫。</summary>
        public const int BoxSize = 3;

        /// <summary>生成结果允许相对难度表浮动的提示数格数。</summary>
        public const int ClueTolerance = 2;

        /// <summary>难度对应的保留提示数：简单 40、中等 32、困难 26。</summary>
        public static int ClueCount(SudokuDifficulty difficulty) => difficulty switch
        {
            SudokuDifficulty.Easy => 40,
            SudokuDifficulty.Medium => 32,
            SudokuDifficulty.Hard => 26,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "未知难度。"),
        };
    }
}
