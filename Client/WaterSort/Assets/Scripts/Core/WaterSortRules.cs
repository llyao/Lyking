namespace Lyking.WaterSort.Core
{
    /// <summary>
    /// 水体排序规则常量：每瓶容量 4 层、默认 5 种颜色、总瓶数为颜色数加 2 个空瓶。
    /// 核心与表现层共同引用，颜色编号约定为 1..N。
    /// </summary>
    public static class WaterSortRules
    {
        /// <summary>每只瓶子的容量（颜色层数）。</summary>
        public const int TubeCapacity = 4;

        /// <summary>生成谜题时附带的空瓶数量。</summary>
        public const int EmptyTubeCount = 2;

        /// <summary>默认颜色数。</summary>
        public const int DefaultColorCount = 5;

        /// <summary>允许的最少颜色数。</summary>
        public const int MinColorCount = 2;

        /// <summary>允许的最多颜色数。</summary>
        public const int MaxColorCount = 8;

        /// <summary>生成谜题时求解器验证不可解的最大换种重试次数。</summary>
        public const int MaxGenerateAttempts = 64;

        /// <summary>求解器广度优先搜索的最大展开状态数，超出视为本轮不可解。</summary>
        public const int MaxSolverStates = 200000;

        /// <summary>总瓶数：N 只满瓶加 2 只空瓶。</summary>
        public static int TotalTubes(int colorCount) => colorCount + EmptyTubeCount;

        /// <summary>校验颜色数在允许范围内，越界抛出异常。</summary>
        public static void ValidateColorCount(int colorCount)
        {
            if (colorCount < MinColorCount || colorCount > MaxColorCount)
            {
                throw new System.ArgumentOutOfRangeException(nameof(colorCount), colorCount,
                    $"颜色数须在 {MinColorCount} 到 {MaxColorCount} 之间。");
            }
        }
    }
}
