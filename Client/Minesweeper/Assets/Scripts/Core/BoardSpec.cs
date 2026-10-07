#nullable enable
using System;

namespace Lyking.Minesweeper.Core
{
    /// <summary>
    /// 棋盘规格：宽、高与地雷数量。规格在构造时校验，非法输入快速失败，
    /// 保证 <see cref="MinesweeperBoard"/> 生命周期内始终可成局。
    /// </summary>
    public sealed record BoardSpec
    {
        public const int MinWidth = 2;
        public const int MinHeight = 2;

        public BoardSpec(int width, int height, int mineCount)
        {
            if (width < MinWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "宽度不能小于 2。");
            }

            if (height < MinHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "高度不能小于 2。");
            }

            if (mineCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(mineCount), mineCount, "地雷数量不能少于 1。");
            }

            if (mineCount > width * height - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(mineCount), mineCount, "地雷数量必须至少留出一个安全格。");
            }

            Width = width;
            Height = height;
            MineCount = mineCount;
        }

        public int Width { get; }

        public int Height { get; }

        public int MineCount { get; }

        public int CellCount => Width * Height;

        public int SafeCellCount => CellCount - MineCount;

        public static BoardSpec Beginner => new(9, 9, 10);

        public static BoardSpec Intermediate => new(16, 16, 40);

        public static BoardSpec Expert => new(30, 16, 99);
    }
}
