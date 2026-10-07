namespace Lyking.FlappyBird.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum FlappyEventType
    {
        /// <summary>核心创建，小鸟就位悬停。</summary>
        Spawned,

        /// <summary>点按扇翅，竖直速度置顶为上升速度。</summary>
        Flapped,

        /// <summary>穿过管道缺口中心，得分。</summary>
        Scored,

        /// <summary>小鸟矩形与管道矩形重叠，碰撞死亡。</summary>
        HitPipe,

        /// <summary>小鸟触地死亡。</summary>
        HitGround,

        /// <summary>重开一局，核心状态全部复位。</summary>
        Reset,
    }

    /// <summary>一条带物理步序号的事件记录。</summary>
    public readonly struct FlappyEventLog
    {
        public FlappyEventLog(int step, FlappyEventType type)
        {
            Step = step;
            Type = type;
        }

        /// <summary>事件发生前已完成的物理步数。</summary>
        public int Step { get; }

        public FlappyEventType Type { get; }

        public override string ToString() => $"步骤 {Step}: {Type}";
    }
}
