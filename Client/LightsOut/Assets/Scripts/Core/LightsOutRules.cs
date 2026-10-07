namespace Lyking.LightsOut.Core
{
    /// <summary>
    /// 点灯规则常量：默认 5x5 格子，构造支持 3 到 7 的棋盘尺寸；
    /// 谜题由从全灭状态反向随机按下 K 下生成，保证必然可解。核心与表现层共同引用。
    /// </summary>
    public static class LightsOutRules
    {
        /// <summary>默认棋盘边长。</summary>
        public const int BoardSize = 5;

        /// <summary>棋盘边长下限。</summary>
        public const int MinSize = 3;

        /// <summary>棋盘边长上限。</summary>
        public const int MaxSize = 7;

        /// <summary>生成谜题时反向按下的最少次数。</summary>
        public const int MinPuzzlePresses = 8;

        /// <summary>生成谜题时反向按下的最多次数。</summary>
        public const int MaxPuzzlePresses = 15;
    }
}
