namespace Lyking.DoodleJumper.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum JumperEventType
    {
        /// <summary>核心创建，玩家从起始平台弹起待命。</summary>
        Spawned,

        /// <summary>下落踩上普通平台，速度置顶为弹跳速度。</summary>
        Bounced,

        /// <summary>下落踩上移动平台，速度置顶为弹跳速度。</summary>
        BouncedOnMoving,

        /// <summary>下落踩上易碎平台，平台碎裂消失且不弹起。</summary>
        FragileBroken,

        /// <summary>玩家底部跌出坠落判定线，一局结束。</summary>
        FellOut,

        /// <summary>重开一局，核心状态全部复位。</summary>
        Reset,
    }

    /// <summary>一条带物理步序号的事件记录；涉及平台时附带平台序号，否则为 -1。</summary>
    public readonly struct JumperEventLog
    {
        public JumperEventLog(int step, JumperEventType type, int platformIndex = -1)
        {
            Step = step;
            Type = type;
            PlatformIndex = platformIndex;
        }

        /// <summary>事件发生时所在的物理步序号（从 1 开始）。</summary>
        public int Step { get; }

        public JumperEventType Type { get; }

        /// <summary>事件涉及的平台序号；与平台无关的事件为 -1。</summary>
        public int PlatformIndex { get; }

        public override string ToString() =>
            PlatformIndex >= 0 ? $"步骤 {Step}: {Type}(平台{PlatformIndex})" : $"步骤 {Step}: {Type}";
    }
}
