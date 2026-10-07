namespace Lyking.Tetris.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum TetrisEventType
    {
        /// <summary>新方块进入井口。</summary>
        Spawned,

        /// <summary>方块落定并写入井格。</summary>
        Locked,

        /// <summary>整行被消除，记录附带行数。</summary>
        LineCleared,

        /// <summary>累计消行达到升级门槛。</summary>
        LevelUp,

        /// <summary>新方块与堆叠重叠，游戏结束。</summary>
        GameOver,
    }

    /// <summary>一条带重力步序号的事件记录。</summary>
    public readonly struct TetrisEventLog
    {
        public TetrisEventLog(int tick, TetrisEventType type, int lines = 0)
        {
            Tick = tick;
            Type = type;
            Lines = lines;
        }

        /// <summary>事件发生前已完成的重力步数。</summary>
        public int Tick { get; }

        public TetrisEventType Type { get; }

        /// <summary>消行事件的行数，其余事件为 0。</summary>
        public int Lines { get; }

        public override string ToString() => Lines > 0 ? $"步序 {Tick}: {Type}（{Lines} 行）" : $"步序 {Tick}: {Type}";
    }
}
