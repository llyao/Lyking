namespace Lyking.Frogger.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum FroggerEventType
    {
        Spawned,      // 开局青蛙在起点就位
        Hopped,       // 跳跃成功
        HopRejected,  // 跳跃被边界或对岸非空闲荷花位拒绝
        RodeLog,      // 落上木排开始随排移动
        HitByCar,     // 在马路上被车撞
        Drowned,      // 落入河道未踩到木排
        CarriedOut,   // 站在木排上被带出左右边界
        GoalReached,  // 到达对岸荷花位进位，Index 为荷花位次
        RoundCleared, // 荷花位全满过关并进入下一轮
        LifeLost,     // 失去一条生命并回到起点
        GameOver,     // 生命用尽游戏结束
    }

    /// <summary>一条带棋局时刻的事件记录；<see cref="Index"/> 仅供带位次的事件（如进位）使用。</summary>
    public readonly struct FroggerEventLog
    {
        public FroggerEventLog(float time, FroggerEventType type, int index = -1)
        {
            Time = time;
            Type = type;
            Index = index;
        }

        /// <summary>事件发生时的棋局累计时间（秒）。</summary>
        public float Time { get; }

        public FroggerEventType Type { get; }

        /// <summary>事件相关的位次（如到达的荷花位序号），无位次时为 -1。</summary>
        public int Index { get; }

        public override string ToString() => Index >= 0
            ? $"时刻 {Time:0.0}s: {Type} #{Index}"
            : $"时刻 {Time:0.0}s: {Type}";
    }
}
