using System;

namespace Lyking.Game2048.Core
{
    /// <summary>2048 棋盘：行列尺寸固定的整数网格，0 表示空格。</summary>
    public sealed class GameBoard
    {
        public const int DefaultSize = 4;

        private readonly int[,] cells;

        public GameBoard(int rows = DefaultSize, int columns = DefaultSize)
        {
            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "棋盘行数必须为正。");
            }

            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "棋盘列数必须为正。");
            }

            Rows = rows;
            Columns = columns;
            cells = new int[rows, columns];
        }

        public int Rows { get; }

        public int Columns { get; }

        public int this[int row, int column]
        {
            get
            {
                CheckBounds(row, column);
                return cells[row, column];
            }
            set
            {
                CheckBounds(row, column);
                cells[row, column] = value;
            }
        }

        public int CountNonEmpty
        {
            get
            {
                int count = 0;
                for (int row = 0; row < Rows; row++)
                {
                    for (int column = 0; column < Columns; column++)
                    {
                        if (cells[row, column] != 0)
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        public bool IsFull => CountNonEmpty == Rows * Columns;

        public bool HasEmpty => CountNonEmpty < Rows * Columns;

        public GameBoard Clone()
        {
            var clone = new GameBoard(Rows, Columns);
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    clone.cells[row, column] = cells[row, column];
                }
            }

            return clone;
        }

        public bool Equals(GameBoard? other)
        {
            if (other is null || other.Rows != Rows || other.Columns != Columns)
            {
                return false;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    if (cells[row, column] != other.cells[row, column])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is GameBoard other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int row = 0; row < Rows; row++)
                {
                    for (int column = 0; column < Columns; column++)
                    {
                        hash = (hash * 31) + cells[row, column];
                    }
                }

                return hash;
            }
        }

        private void CheckBounds(int row, int column)
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, $"行索引必须在 [0, {Rows}) 内。");
            }

            if (column < 0 || column >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, $"列索引必须在 [0, {Columns}) 内。");
            }
        }
    }
}
