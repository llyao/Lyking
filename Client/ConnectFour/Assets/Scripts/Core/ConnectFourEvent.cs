namespace Lyking.ConnectFour.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum ConnectFourEventType
    {
        /// <summary>玩家（红）落子。</summary>
        PlayerDropped,

        /// <summary>AI（黄）落子。</summary>
        AiDropped,

        /// <summary>玩家（红）连成四连获胜。</summary>
        PlayerWon,

        /// <summary>AI（黄）连成四连获胜。</summary>
        AiWon,

        /// <summary>棋满无胜，平局。</summary>
        Drawn,

        /// <summary>重开一局。</summary>
        Restarted,

        /// <summary>落子被拒（越界列、满列、终局或非玩家回合）。</summary>
        MoveRejected,
    }

    /// <summary>一条带局数、手数与落点列的事件记录。</summary>
    public readonly struct ConnectFourEventLog
    {
        public ConnectFourEventLog(int round, int move, ConnectFourEventType type, int column)
        {
            Round = round;
            Move = move;
            Type = type;
            Column = column;
        }

        /// <summary>事件所属局数（从 1 起）。</summary>
        public int Round { get; }

        /// <summary>事件发生时本局已落的子数；重开事件为 0。</summary>
        public int Move { get; }

        public ConnectFourEventType Type { get; }

        /// <summary>落点列；非落子事件为 -1。</summary>
        public int Column { get; }

        public override string ToString()
        {
            return Column >= 0
                ? $"第 {Round} 局 第 {Move} 手: {Type}(列 {Column})"
                : $"第 {Round} 局 第 {Move} 手: {Type}";
        }
    }
}
