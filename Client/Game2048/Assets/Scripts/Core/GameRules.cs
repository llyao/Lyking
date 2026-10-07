using System;
using System.Collections.Generic;

namespace Lyking.Game2048.Core
{
    /// <summary>2048 纯规则：滑动合并、随机生成与胜负判定，不依赖任何运行时。</summary>
    public static class GameRules
    {
        public const int WinTarget = 2048;

        /// <summary>4 出现的概率为 1/10，其余为 2。</summary>
        private const int SpawnFourThreshold = 9;

        public static MoveResult Move(GameBoard board, MoveDirection direction)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            bool vertical = direction is MoveDirection.Up or MoveDirection.Down;
            bool reversed = direction is MoveDirection.Right or MoveDirection.Down;
            int lineCount = vertical ? board.Columns : board.Rows;
            int lineLength = vertical ? board.Rows : board.Columns;

            bool changed = false;
            int gainedScore = 0;
            int mergedCount = 0;
            var values = new int[lineLength];
            var mergedLine = new int[lineLength];

            for (int line = 0; line < lineCount; line++)
            {
                int count = 0;
                for (int step = 0; step < lineLength; step++)
                {
                    (int row, int column) = GetCell(line, step);
                    int cell = board[row, column];
                    if (cell != 0)
                    {
                        values[count++] = cell;
                    }
                }

                for (int index = 0; index < lineLength; index++)
                {
                    mergedLine[index] = 0;
                }

                int write = 0;
                for (int index = 0; index < count; index++)
                {
                    if (index + 1 < count && values[index] == values[index + 1])
                    {
                        int mergedValue = values[index] * 2;
                        mergedLine[write++] = mergedValue;
                        gainedScore += mergedValue;
                        mergedCount++;
                        index++;
                    }
                    else
                    {
                        mergedLine[write++] = values[index];
                    }
                }

                for (int step = 0; step < lineLength; step++)
                {
                    (int row, int column) = GetCell(line, step);
                    if (board[row, column] != mergedLine[step])
                    {
                        changed = true;
                        board[row, column] = mergedLine[step];
                    }
                }
            }

            return new MoveResult(changed, gainedScore, mergedCount);

            (int row, int column) GetCell(int line, int step)
            {
                int targetStep = reversed ? lineLength - 1 - step : step;
                return vertical ? (targetStep, line) : (line, targetStep);
            }
        }

        /// <summary>在随机空格生成新块（90% 为 2，10% 为 4）；满盘时不生成。</summary>
        public static bool TrySpawnRandomTile(GameBoard board, Random random, out int value)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (random is null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            value = 0;
            if (!board.HasEmpty)
            {
                return false;
            }

            var emptyCells = new List<(int Row, int Column)>();
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (board[row, column] == 0)
                    {
                        emptyCells.Add((row, column));
                    }
                }
            }

            (int spawnRow, int spawnColumn) = emptyCells[random.Next(emptyCells.Count)];
            value = random.Next(10) < SpawnFourThreshold ? 2 : 4;
            board[spawnRow, spawnColumn] = value;
            return true;
        }

        /// <summary>棋盘满且任意方向都无法产生合并时判定结束。</summary>
        public static bool IsGameOver(GameBoard board)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (board.HasEmpty)
            {
                return false;
            }

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    int cell = board[row, column];
                    if (column + 1 < board.Columns && board[row, column + 1] == cell)
                    {
                        return false;
                    }

                    if (row + 1 < board.Rows && board[row + 1, column] == cell)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>棋盘上是否已合成出目标值（达到或超过）。</summary>
        public static bool ContainsTarget(GameBoard board, int target = WinTarget)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (board[row, column] >= target)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
