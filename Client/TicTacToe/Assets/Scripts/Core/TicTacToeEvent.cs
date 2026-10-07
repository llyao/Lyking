namespace Lyking.TicTacToe.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum TicTacToeEventType
    {
        /// <summary>玩家（X）落子。</summary>
        PlayerMoved,

        /// <summary>AI（O）落子。</summary>
        AiMoved,

        /// <summary>玩家（X）连成三连获胜。</summary>
        PlayerWon,

        /// <summary>AI（O）连成三连获胜。</summary>
        AiWon,

        /// <summary>棋满无胜，平局。</summary>
        Drawn,

        /// <summary>重开一局。</summary>
        Restarted,
    }

    /// <summary>一条带局数、手数与落点的事件记录。</summary>
    public readonly struct TicTacToeEventLog
    {
        public TicTacToeEventLog(int round, int move, TicTacToeEventType type, int cellIndex)
        {
            Round = round;
            Move = move;
            Type = type;
            CellIndex = cellIndex;
        }

        /// <summary>事件所属局数（从 1 起）。</summary>
        public int Round { get; }

        /// <summary>事件发生时本局已落的子数；重开事件为 0。</summary>
        public int Move { get; }

        public TicTacToeEventType Type { get; }

        /// <summary>落点格索引；非落子事件为 -1。</summary>
        public int CellIndex { get; }

        public override string ToString()
        {
            return CellIndex >= 0
                ? $"第 {Round} 局 第 {Move} 手: {Type}(格 {CellIndex})"
                : $"第 {Round} 局 第 {Move} 手: {Type}";
        }
    }
}
