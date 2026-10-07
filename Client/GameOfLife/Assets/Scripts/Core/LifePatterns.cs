namespace Lyking.GameOfLife.Core
{
    /// <summary>内置图案定义：相对格位集合，载入时按包围盒在棋盘上居中放置。</summary>
    public static class LifePatterns
    {
        /// <summary>闪烁器（Blinker）：最小的振荡子，周期 2，横竖两态交替。</summary>
        public static readonly (int X, int Y)[] Blinker = { (1, 0), (1, 1), (1, 2) };

        /// <summary>方块（Block）：最小的静物，稳定不变。</summary>
        public static readonly (int X, int Y)[] Block = { (0, 0), (1, 0), (0, 1), (1, 1) };

        /// <summary>滑翔机（Glider）：周期 4，每 4 代沿对角线平移一格。</summary>
        public static readonly (int X, int Y)[] Glider = { (1, 0), (2, 1), (0, 2), (1, 2), (2, 2) };

        /// <summary>
        /// 滑翔机枪（Gosper Glider Gun）：36 细胞标准坐标（包围盒 36x9），
        /// 每 30 代向右下方发射一枚滑翔机。
        /// </summary>
        public static readonly (int X, int Y)[] GosperGliderGun =
        {
            (24, 0),
            (22, 1), (24, 1),
            (12, 2), (13, 2), (20, 2), (21, 2), (34, 2), (35, 2),
            (11, 3), (16, 3), (19, 3), (20, 3), (33, 3), (34, 3),
            (0, 4), (1, 4), (10, 4), (16, 4), (20, 4), (21, 4),
            (0, 5), (1, 5), (10, 5), (14, 5), (16, 5), (17, 5), (22, 5), (24, 5),
            (10, 6), (16, 6), (24, 6),
            (11, 7), (16, 7),
            (12, 8), (13, 8),
        };
    }
}
