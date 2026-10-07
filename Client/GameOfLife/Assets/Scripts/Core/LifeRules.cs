namespace Lyking.GameOfLife.Core
{
    /// <summary>
    /// 生命游戏规则常量：默认 48x32 网格、B3/S23 规则、环形边界与固定运行节拍；
    /// 棋盘尺寸构造可调。核心与表现层共同引用。
    /// </summary>
    public static class LifeRules
    {
        /// <summary>默认网格宽度。</summary>
        public const int DefaultWidth = 48;

        /// <summary>默认网格高度。</summary>
        public const int DefaultHeight = 32;

        /// <summary>棋盘边长下限。</summary>
        public const int MinSize = 4;

        /// <summary>棋盘边长上限。</summary>
        public const int MaxSize = 256;

        /// <summary>运行节拍：每 0.125 秒推进一代（8 代/秒）。</summary>
        public const float StepSeconds = 0.125f;

        /// <summary>死细胞复活的邻居数（B3）。</summary>
        public const int BirthNeighbors = 3;

        /// <summary>活细胞存活的最少邻居数（S23 下限）。</summary>
        public const int MinSurviveNeighbors = 2;

        /// <summary>活细胞存活的最多邻居数（S23 上限）。</summary>
        public const int MaxSurviveNeighbors = 3;

        /// <summary>默认随机撒子密度。</summary>
        public const double DefaultDensity = 0.3;
    }
}
