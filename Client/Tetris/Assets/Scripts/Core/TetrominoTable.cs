using System;
using System.Collections.Generic;

namespace Lyking.Tetris.Core
{
    /// <summary>
    /// 四格方块静态偏移表：每种方块 4 个旋转态，每态 4 个包围盒内整数偏移（Y 向上）。
    /// 旋转态按顺时针排列，相邻两态互为绕包围盒中心的顺时针旋转；Core 内不使用任何引擎类型。
    /// </summary>
    public static class TetrominoTable
    {
        public const int KindCount = 7;

        public const int RotationCount = 4;

        public const int CellsPerPiece = 4;

        /// <summary>各包围盒边长，顺序与 <see cref="TetrominoKind"/> 一致：I O T S Z J L。</summary>
        private static readonly int[] BoxSizes = { 4, 2, 3, 3, 3, 3, 3 };

        /// <summary>偏移表：[种类][旋转态][4 个格子]，旋转态 0 为出场朝向。</summary>
        private static readonly GridPos[][][] ShapeTable =
        {
            // I：横放占上起第二行，竖放占左起第三列。
            new[]
            {
                new[] { new GridPos(0, 2), new GridPos(1, 2), new GridPos(2, 2), new GridPos(3, 2) },
                new[] { new GridPos(2, 0), new GridPos(2, 1), new GridPos(2, 2), new GridPos(2, 3) },
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(3, 1) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2), new GridPos(1, 3) },
            },
            // O：2x2 方块，四个旋转态形状相同。
            new[]
            {
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1) },
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1) },
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1) },
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1) },
            },
            // T：出场平底朝上凸，之后凸起依次转向右、下、左。
            new[]
            {
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(1, 2) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 1) },
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(1, 0) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2), new GridPos(0, 1) },
            },
            // S：出场为上右下左两横格。
            new[]
            {
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 2) },
                new[] { new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 0), new GridPos(2, 1) },
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1), new GridPos(2, 1) },
                new[] { new GridPos(0, 1), new GridPos(0, 2), new GridPos(1, 0), new GridPos(1, 1) },
            },
            // Z：出场为上左下右两横格。
            new[]
            {
                new[] { new GridPos(0, 2), new GridPos(1, 2), new GridPos(1, 1), new GridPos(2, 1) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(2, 1), new GridPos(2, 2) },
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 0), new GridPos(2, 0) },
                new[] { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 2) },
            },
            // J：出场横排带左上角块。
            new[]
            {
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(0, 2) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 2) },
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(2, 0) },
                new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2) },
            },
            // L：出场横排带右上角块。
            new[]
            {
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(2, 2) },
                new[] { new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 0) },
                new[] { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) },
                new[] { new GridPos(0, 2), new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2) },
            },
        };

        /// <summary>读取某方块某旋转态的 4 个包围盒内偏移。</summary>
        public static IReadOnlyList<GridPos> Cells(TetrominoKind kind, int rotation)
        {
            int kindIndex = KindIndex(kind);
            if (rotation < 0 || rotation >= RotationCount) throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "旋转态必须在 0 到 3 之间。");
            return ShapeTable[kindIndex][rotation];
        }

        public static int BoxWidth(TetrominoKind kind) => BoxSizes[KindIndex(kind)];

        public static int BoxHeight(TetrominoKind kind) => BoxSizes[KindIndex(kind)];

        private static int KindIndex(TetrominoKind kind)
        {
            int kindIndex = (int)kind;
            if (kindIndex < 0 || kindIndex >= KindCount) throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的方块种类。");
            return kindIndex;
        }
    }
}
