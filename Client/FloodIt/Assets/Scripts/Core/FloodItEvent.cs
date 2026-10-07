namespace Lyking.FloodIt.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum FloodItEventType
    {
        Generated,
        Chosen,
        Rejected,
        Won,
        Lost,
        Reset,
    }

    /// <summary>一条带步数、颜色与扩张格数的事件记录。</summary>
    public readonly struct FloodItEventLog
    {
        public FloodItEventLog(int step, FloodItEventType type, int color, int expandedCount)
        {
            Step = step;
            Type = type;
            Color = color;
            ExpandedCount = expandedCount;
        }

        /// <summary>事件发生时已用的步数。</summary>
        public int Step { get; }

        public FloodItEventType Type { get; }

        /// <summary>关联的颜色编号，发题、重置与胜负事件为 -1（胜负事件记录触发选色）。</summary>
        public int Color { get; }

        /// <summary>本次选色新增的淹没格数，仅选色事件为正值。</summary>
        public int ExpandedCount { get; }

        public override string ToString()
        {
            if (Type == FloodItEventType.Chosen) return $"步数 {Step}: {Type} 色 {Color} 扩张 {ExpandedCount}";
            return Color >= 0 ? $"步数 {Step}: {Type} 色 {Color}" : $"步数 {Step}: {Type}";
        }
    }
}
