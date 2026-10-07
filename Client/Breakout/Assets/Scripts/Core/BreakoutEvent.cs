namespace Lyking.Breakout.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum BreakoutEventType
    {
        /// <summary>对局创建。</summary>
        Spawned,

        /// <summary>发球。</summary>
        Launched,

        /// <summary>球从挡板弹起。</summary>
        PaddleBounced,

        /// <summary>球撞墙反弹。</summary>
        WallBounced,

        /// <summary>耐久砖受击未碎，Value 为剩余耐久。</summary>
        BrickDamaged,

        /// <summary>砖块被击破，Value 为该砖分值。</summary>
        BrickDestroyed,

        /// <summary>球落底失去一命，Value 为剩余生命。</summary>
        LifeLost,

        /// <summary>清空砖阵过关，Value 为进入的关卡号。</summary>
        LevelCleared,

        /// <summary>生命耗尽游戏结束。</summary>
        GameOver,
    }

    /// <summary>一条带物理子步序号与附加数值的事件记录。</summary>
    public readonly struct BreakoutEventLog
    {
        public BreakoutEventLog(int step, BreakoutEventType type, int value)
        {
            Step = step;
            Type = type;
            Value = value;
        }

        /// <summary>事件发生时的物理子步序号。</summary>
        public int Step { get; }

        public BreakoutEventType Type { get; }

        /// <summary>事件的附加数值：分值、剩余耐久、剩余生命或关卡号，无意义时为 0。</summary>
        public int Value { get; }

        public override string ToString() => $"步骤 {Step}: {Type}({Value})";
    }
}
