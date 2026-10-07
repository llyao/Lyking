namespace Lyking.EndlessRunner.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum RunnerEventType
    {
        /// <summary>核心创建，玩家落地待命。</summary>
        Spawned,

        /// <summary>地面起跳，速度置顶为起跳初速。</summary>
        Jumped,

        /// <summary>空中二段跳，速度置顶为二段跳初速。</summary>
        DoubleJumped,

        /// <summary>空中按住快落键，重力按倍率放大。</summary>
        FastFallStarted,

        /// <summary>落回地面，速度清零、跳段重置。</summary>
        Landed,

        /// <summary>障碍整体越到玩家身后，记入日志。</summary>
        ClearedObstacle,

        /// <summary>玩家矩形与障碍矩形重叠，碰撞死亡。</summary>
        HitObstacle,

        /// <summary>重开一局，核心状态全部复位。</summary>
        Reset,
    }

    /// <summary>一条带物理步序号的事件记录。</summary>
    public readonly struct RunnerEventLog
    {
        public RunnerEventLog(int step, RunnerEventType type)
        {
            Step = step;
            Type = type;
        }

        /// <summary>事件发生时所在的物理步序号（从 1 开始）。</summary>
        public int Step { get; }

        public RunnerEventType Type { get; }

        public override string ToString() => $"步骤 {Step}: {Type}";
    }
}
