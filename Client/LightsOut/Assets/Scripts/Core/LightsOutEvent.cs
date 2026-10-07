namespace Lyking.LightsOut.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum LightsOutEventType
    {
        Generated,
        Pressed,
        Won,
        Reset,
        Regenerated,
    }

    /// <summary>一条带步数、格位与翻转格数的事件记录。</summary>
    public readonly struct LightsOutEventLog
    {
        public LightsOutEventLog(int move, LightsOutEventType type, int row = -1, int column = -1, int flippedCount = 0)
        {
            Move = move;
            Type = type;
            Row = row;
            Column = column;
            FlippedCount = flippedCount;
        }

        /// <summary>事件发生时已用的步数。</summary>
        public int Move { get; }

        public LightsOutEventType Type { get; }

        /// <summary>关联格位的行，无关联格位的事件为 -1。</summary>
        public int Row { get; }

        /// <summary>关联格位的列，无关联格位的事件为 -1。</summary>
        public int Column { get; }

        /// <summary>本次按下实际翻转的格子数（含自身与四邻），非按下事件为 0。</summary>
        public int FlippedCount { get; }

        public override string ToString()
        {
            if (Row < 0) return $"步数 {Move}: {Type}";
            return FlippedCount > 0 ? $"步数 {Move}: {Type} 格 ({Row},{Column}) 翻转 {FlippedCount}" : $"步数 {Move}: {Type} 格 ({Row},{Column})";
        }
    }
}
