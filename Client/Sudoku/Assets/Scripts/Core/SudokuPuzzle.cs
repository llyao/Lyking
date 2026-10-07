using System;
using System.Collections.Generic;

namespace Lyking.Sudoku.Core
{
    /// <summary>
    /// 数独核心棋局：确定性离散回合制状态机，无 Unity 依赖、无内建时钟。
    /// 谜题由注入种子的 <see cref="SudokuGenerator"/> 生成；给定格锁定不可覆盖，
    /// 玩家数可填写、覆盖与清除，行列宫重复计入冲突状态但不阻止填写；
    /// 全盘填满且与解一致即 <see cref="IsSolved"/>。<see cref="Hint"/> 在随机空格
    /// 填入正确答案，<see cref="Reset"/> 清空全部玩家填数回到开局。
    /// </summary>
    public sealed class SudokuPuzzle
    {
        private const int MaxEventLogs = 128;

        private readonly int[,] _given = new int[SudokuRules.Size, SudokuRules.Size];
        private readonly int[,] _player = new int[SudokuRules.Size, SudokuRules.Size];
        private readonly int[,] _board = new int[SudokuRules.Size, SudokuRules.Size];
        private readonly int[,] _solution = new int[SudokuRules.Size, SudokuRules.Size];
        private readonly List<SudokuEventLog> _events = new List<SudokuEventLog>();
        private readonly int _seed;
        private readonly SudokuDifficulty _difficulty;
        private readonly int _clueCount;
        private int _moveCount;

        /// <summary>以种子与难度生成新谜题；同种子同难度的谜题与解完全一致。</summary>
        public SudokuPuzzle(int seed, SudokuDifficulty difficulty = SudokuDifficulty.Easy)
        {
            var data = SudokuGenerator.Generate(seed, difficulty);
            _seed = data.Seed;
            _difficulty = data.Difficulty;
            _clueCount = data.ClueCount;
            Array.Copy(data.Given, _given, _given.Length);
            Array.Copy(data.Solution, _solution, _solution.Length);
            RestoreGivenBoard();
            Log(SudokuEventType.NewPuzzle);
        }

        /// <summary>生成当前谜题所用的种子。</summary>
        public int Seed => _seed;

        public SudokuDifficulty Difficulty => _difficulty;

        /// <summary>谜题保留的提示数（给定格数量）。</summary>
        public int ClueCount => _clueCount;

        /// <summary>已完成的有效操作步数：成功填数、清除与提示各记 1 步。</summary>
        public int MoveCount => _moveCount;

        /// <summary>剩余空格数。</summary>
        public int EmptyCount
        {
            get
            {
                int count = 0;
                foreach (int value in _board)
                {
                    if (value == 0) count++;
                }

                return count;
            }
        }

        /// <summary>是否还有空格未填。</summary>
        public bool IsEmptyLeft => EmptyCount > 0;

        /// <summary>
        /// 是否通关：全盘填满且与生成解一致；与有效解一致同时保证盘面无行列宫冲突。
        /// </summary>
        public bool IsSolved => EmptyCount == 0 && SameBoard(_board, _solution);

        /// <summary>给定格盘面的副本，0 表示空格；修改副本不影响棋局。</summary>
        public int[,] Given => (int[,])_given.Clone();

        /// <summary>玩家填数盘面的副本，0 表示空格；修改副本不影响棋局。</summary>
        public int[,] Player => (int[,])_player.Clone();

        /// <summary>生成时完整解的副本。</summary>
        public int[,] Solution => (int[,])_solution.Clone();

        public IReadOnlyList<SudokuEventLog> Events => _events;

        /// <summary>指定格是否为给定格（生成时保留的提示）。</summary>
        public bool IsGiven(int row, int column)
        {
            EnsureCell(row, column);
            return _given[row, column] != 0;
        }

        /// <summary>合并盘面值：玩家数优先，空格为 0。</summary>
        public int ValueAt(int row, int column)
        {
            EnsureCell(row, column);
            return _board[row, column];
        }

        /// <summary>玩家在指定格填写的数，空格或给定格为 0。</summary>
        public int PlayerValue(int row, int column)
        {
            EnsureCell(row, column);
            return _player[row, column];
        }

        /// <summary>该格当前值是否与所在行、列或宫中的另一格重复；空格恒为 false。</summary>
        public bool IsConflict(int row, int column)
        {
            EnsureCell(row, column);
            int value = _board[row, column];
            return value != 0 && !SudokuSolver.IsPlacementValid(_board, row, column, value);
        }

        /// <summary>当前全部冲突格的快照：true 表示该格与其他格重复；修改副本不影响棋局。</summary>
        public bool[,] Conflicts
        {
            get
            {
                var result = new bool[SudokuRules.Size, SudokuRules.Size];
                for (int row = 0; row < SudokuRules.Size; row++)
                {
                    for (int column = 0; column < SudokuRules.Size; column++)
                    {
                        result[row, column] = IsConflict(row, column);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 在 (row, column) 填入 value：给定格拒绝，盘面已满拒绝；
        /// 冲突照常计入状态并记录事件，但不阻止填写，返回是否成功。
        /// </summary>
        public bool Fill(int row, int column, int value)
        {
            EnsureCell(row, column);
            if (value < 1 || value > SudokuRules.Size)
                throw new ArgumentOutOfRangeException(nameof(value), value, "填入数字须在 1 到 9 之间。");
            if (_given[row, column] != 0 || EmptyCount == 0) return false;

            _player[row, column] = value;
            _board[row, column] = value;
            _moveCount++;
            Log(SudokuEventType.Filled, row, column, value);
            if (IsConflict(row, column)) Log(SudokuEventType.Conflict, row, column, value);
            if (IsSolved) Log(SudokuEventType.Completed);
            return true;
        }

        /// <summary>清除指定格的玩家填数；给定格或空格无可清除内容返回 false。</summary>
        public bool Clear(int row, int column)
        {
            EnsureCell(row, column);
            if (_given[row, column] != 0 || _player[row, column] == 0) return false;

            int removed = _player[row, column];
            _player[row, column] = 0;
            _board[row, column] = 0;
            _moveCount++;
            Log(SudokuEventType.Cleared, row, column, removed);
            return true;
        }

        /// <summary>
        /// 提示：由种子在剩余空格中随机选中一格并填入正确答案，永不产生冲突；
        /// 无空格可填时返回 false。
        /// </summary>
        public bool Hint(int seed = 0)
        {
            if (!IsEmptyLeft) return false;

            var empties = new List<(int Row, int Column)>();
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    if (_board[row, column] == 0) empties.Add((row, column));
                }
            }

            var random = new Random(seed);
            (int hintRow, int hintColumn) = empties[random.Next(empties.Count)];
            int value = _solution[hintRow, hintColumn];
            _player[hintRow, hintColumn] = value;
            _board[hintRow, hintColumn] = value;
            _moveCount++;
            Log(SudokuEventType.Hint, hintRow, hintColumn, value);
            if (IsSolved) Log(SudokuEventType.Completed);
            return true;
        }

        /// <summary>重置：清空全部玩家填数并清零步数，回到谜题开局状态。</summary>
        public void Reset()
        {
            Array.Clear(_player, 0, _player.Length);
            RestoreGivenBoard();
            _moveCount = 0;
            Log(SudokuEventType.Reset);
        }

        /// <summary>把合并盘面恢复为给定格盘面。</summary>
        private void RestoreGivenBoard() => Array.Copy(_given, _board, _board.Length);

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

        private static void EnsureCell(int row, int column)
        {
            if (row < 0 || row >= SudokuRules.Size) throw new ArgumentOutOfRangeException(nameof(row), row, "行越界。");
            if (column < 0 || column >= SudokuRules.Size) throw new ArgumentOutOfRangeException(nameof(column), column, "列越界。");
        }

        private void Log(SudokuEventType type, int row = -1, int column = -1, int value = 0)
        {
            _events.Add(new SudokuEventLog(_moveCount, type, row, column, value));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
