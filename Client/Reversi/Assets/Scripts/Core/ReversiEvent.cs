namespace Lyking.Reversi.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum ReversiEventType
    {
        /// <summary>玩家（黑）落子并翻转若干白子。</summary>
        PlayerMoved,

        /// <summary>AI（白）落子并翻转若干黑子。</summary>
        AiMoved,

        /// <summary>一方无合法步，跳过回合。</summary>
        Passed,

        /// <summary>本局结束：双方均无步或棋盘下满。</summary>
        Finished,

        /// <summary>非法落点被拒绝（越界、已占或不翻转任何子），盘面保持不变。</summary>
        IllegalRejected,

        /// <summary>玩家（黑）获胜。</summary>
        PlayerWon,

        /// <summary>AI（白）获胜。</summary>
        AiWon,

        /// <summary>平局。</summary>
        Drawn,

        /// <summary>重开一局。</summary>
        Restarted,
    }

    /// <summary>一条带局数、手数、行棋方、落点与翻转数的事件记录。</summary>
    public readonly struct ReversiEventLog
    {
        public ReversiEventLog(int round, ReversiEventType type, Cell side, int move, int row, int col, int flipped)
        {
            Round = round;
            Type = type;
            Side = side;
            Move = move;
            Row = row;
            Col = col;
            Flipped = flipped;
        }

        /// <summary>事件所属局数（从 1 起）；棋盘层记录的原始事件为 0，由对局层补注。</summary>
        public int Round { get; }

        public ReversiEventType Type { get; }

        /// <summary>事件涉及的行棋方；终局、平局与重开事件为 <see cref="Cell.None"/>。</summary>
        public Cell Side { get; }

        /// <summary>事件发生时本局已落子的手数；重开事件为 0。</summary>
        public int Move { get; }

        /// <summary>落点行；非落子事件为 -1。</summary>
        public int Row { get; }

        /// <summary>落点列；非落子事件为 -1。</summary>
        public int Col { get; }

        /// <summary>本次落子翻转的对方子数；非落子事件为 0。</summary>
        public int Flipped { get; }

        /// <summary>以指定局数复制本事件（对局层镜像棋盘事件时补注局数）。</summary>
        public ReversiEventLog WithRound(int round)
        {
            return new ReversiEventLog(round, Type, Side, Move, Row, Col, Flipped);
        }

        public override string ToString()
        {
            if (Row >= 0 && Col >= 0)
            {
                return Type == ReversiEventType.IllegalRejected
                    ? $"第 {Round} 局 第 {Move} 手: {Type}({ReversiRules.ToCoordinate(Row, Col)})"
                    : $"第 {Round} 局 第 {Move} 手: {Type}({ReversiRules.ToCoordinate(Row, Col)} 翻 {Flipped} 子)";
            }

            return $"第 {Round} 局 第 {Move} 手: {Type}";
        }
    }
}
