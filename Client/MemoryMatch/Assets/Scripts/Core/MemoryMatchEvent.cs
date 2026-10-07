namespace Lyking.MemoryMatch.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum MemoryMatchEventType
    {
        Dealt,
        Flipped,
        Matched,
        Mismatched,
        FlippedBack,
        Won,
    }

    /// <summary>一条带步数与格位的事件记录。</summary>
    public readonly struct MemoryMatchEventLog
    {
        public MemoryMatchEventLog(int move, MemoryMatchEventType type, int cardIndex)
        {
            Move = move;
            Type = type;
            CardIndex = cardIndex;
        }

        /// <summary>事件发生时已用的步数。</summary>
        public int Move { get; }

        public MemoryMatchEventType Type { get; }

        /// <summary>关联的卡牌格位，发牌事件为 -1。</summary>
        public int CardIndex { get; }

        public override string ToString() =>
            CardIndex >= 0 ? $"步数 {Move}: {Type} 格 {CardIndex}" : $"步数 {Move}: {Type}";
    }
}
