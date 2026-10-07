namespace Lyking.Match3.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum Match3EventType
    {
        Dealt,
        Swapped,
        SwapRejected,
        Matched,
        Refilled,
        Shuffled,
        Finished,
    }

    /// <summary>一条带步数、连锁、组数与得分的事件记录；未用到的字段取中性默认值。</summary>
    public readonly struct Match3EventLog
    {
        public Match3EventLog(int move, Match3EventType type, int firstIndex = -1, int secondIndex = -1,
            int chainIndex = 0, int groupCount = 0, int cellCount = 0, int score = 0)
        {
            Move = move;
            Type = type;
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
            ChainIndex = chainIndex;
            GroupCount = groupCount;
            CellCount = cellCount;
            Score = score;
        }

        /// <summary>事件发生时已用的步数。</summary>
        public int Move { get; }

        public Match3EventType Type { get; }

        /// <summary>交换事件的第一个格位，其他事件为 -1。</summary>
        public int FirstIndex { get; }

        /// <summary>交换事件的第二个格位，其他事件为 -1。</summary>
        public int SecondIndex { get; }

        /// <summary>消除事件所属的连锁波次（1 起计），其他事件为 0。</summary>
        public int ChainIndex { get; }

        /// <summary>消除事件同波消除的组数，其他事件为 0。</summary>
        public int GroupCount { get; }

        /// <summary>消除事件消除的格数（交叠格只计一次），其他事件为 0。</summary>
        public int CellCount { get; }

        /// <summary>消除事件的得分（含连锁倍率），其他事件为 0。</summary>
        public int Score { get; }

        public override string ToString() => Type switch
        {
            Match3EventType.Swapped => $"步数 {Move}: 交换 {FirstIndex} 与 {SecondIndex}",
            Match3EventType.SwapRejected => $"步数 {Move}: 无效交换 {FirstIndex} 与 {SecondIndex}，已回退",
            Match3EventType.Matched => $"步数 {Move}: 连锁 x{ChainIndex} 消除 {GroupCount} 组 {CellCount} 格，得分 {Score}",
            Match3EventType.Refilled => $"步数 {Move}: 重力下落与顶部填充完成",
            Match3EventType.Shuffled => $"步数 {Move}: 无可行交换，重排棋盘",
            Match3EventType.Finished => $"步数 {Move}: 步数用尽，对局结束",
            _ => $"步数 {Move}: {Type}",
        };
    }
}
