namespace Lyking.Pong.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum PongEventType
    {
        /// <summary>对局创建。</summary>
        Spawned,

        /// <summary>发球，Value 为朝向（+1 朝右、-1 朝左）。</summary>
        Served,

        /// <summary>球从挡板弹起，Value 为挡板（+1 左、-1 右）。</summary>
        PaddleBounced,

        /// <summary>球撞上下墙反弹，Value 为墙面（+1 顶、-1 底）。</summary>
        WallBounced,

        /// <summary>球出界得分，Value 为得分方（+1 左、-1 右）。</summary>
        Scored,

        /// <summary>先到胜利分获胜，Value 为胜方（+1 左、-1 右）。</summary>
        Won,
    }

    /// <summary>一条带物理子步序号与附加数值的事件记录。</summary>
    public readonly struct PongEventLog
    {
        public PongEventLog(int step, PongEventType type, int value)
        {
            Step = step;
            Type = type;
            Value = value;
        }

        /// <summary>事件发生时的物理子步序号。</summary>
        public int Step { get; }

        public PongEventType Type { get; }

        /// <summary>事件的附加数值：朝向、挡板、墙面或得分方，无意义时为 0。</summary>
        public int Value { get; }

        public override string ToString() => $"步骤 {Step}: {Type}({Value})";
    }
}
