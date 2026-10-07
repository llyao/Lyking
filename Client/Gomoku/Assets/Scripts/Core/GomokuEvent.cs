namespace Lyking.Gomoku.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum GomokuEventType
    {
        /// <summary>玩家（黑）落子。</summary>
        PlayerMoved,

        /// <summary>AI（白）落子。</summary>
        AiMoved,

        /// <summary>玩家（黑）连成五连获胜。</summary>
        PlayerWon,

        /// <summary>AI（白）连成五连获胜。</summary>
        AiWon,

        /// <summary>棋满无胜，平局。</summary>
        Drawn,

        /// <summary>重开一局。</summary>
        Restarted,

        /// <summary>落子被拒（越界、已占、终局或非玩家回合）。</summary>
        MoveRejected,
    }

    /// <summary>一条带局数、手数与落点坐标的事件记录。</summary>
    public readonly struct GomokuEventLog
    {
        public GomokuEventLog(int round, int move, GomokuEventType type, int row, int col)
        {
            Round = round;
            Move = move;
            Type = type;
            Row = row;
            Col = col;
        }

        /// <summary>事件所属局数（从 1 起）。</summary>
        public int Round { get; }

        /// <summary>事件发生时本局已落的子数；重开事件为 0。</summary>
        public int Move { get; }

        public GomokuEventType Type { get; }

        /// <summary>落点行；非落子事件为 -1。</summary>
        public int Row { get; }

        /// <summary>落点列；非落子事件为 -1。</summary>
        public int Col { get; }

        public override string ToString()
        {
            return Row >= 0 && Col >= 0
                ? $"第 {Round} 局 第 {Move} 手: {Type}(行 {Row} 列 {Col})"
                : $"第 {Round} 局 第 {Move} 手: {Type}";
        }
    }
}
