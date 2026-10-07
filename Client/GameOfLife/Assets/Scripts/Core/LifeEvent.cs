namespace Lyking.GameOfLife.Core
{
    /// <summary>棋局关键操作的类型，供表现层日志与测试断言。逐代推进不产生事件。</summary>
    public enum LifeEventType
    {
        Randomized,
        Cleared,
        PatternLoaded,
        Paused,
        Resumed,
    }

    /// <summary>一条带代数、类型、格数与图案名的事件记录。</summary>
    public readonly struct LifeEventLog
    {
        public LifeEventLog(int generation, LifeEventType type, int cellCount = 0, string patternName = "")
        {
            Generation = generation;
            Type = type;
            CellCount = cellCount;
            PatternName = patternName;
        }

        /// <summary>事件发生时的代数；撒子、清空与载入图案会把代数归零，故对应事件记 0。</summary>
        public int Generation { get; }

        public LifeEventType Type { get; }

        /// <summary>关联的活细胞格数（随机撒子与载入图案），无关联数量的事件为 0。</summary>
        public int CellCount { get; }

        /// <summary>载入图案的名称，非载入事件为空字符串。</summary>
        public string PatternName { get; }

        public override string ToString()
        {
            string head = $"第 {Generation} 代: {Type}";
            if (!string.IsNullOrEmpty(PatternName)) return $"{head} {PatternName} {CellCount} 格";
            return CellCount > 0 ? $"{head} {CellCount} 格" : head;
        }
    }
}
