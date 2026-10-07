namespace Lyking.Snake.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum SnakeEventType
    {
        Spawned,
        Turned,
        AteFood,
        SelfCollision,
        WallCollision,
        Won,
    }

    /// <summary>一条带步序号的事件记录。</summary>
    public readonly struct SnakeEventLog
    {
        public SnakeEventLog(int step, SnakeEventType type)
        {
            Step = step;
            Type = type;
        }

        /// <summary>事件发生前已完成的步数。</summary>
        public int Step { get; }

        public SnakeEventType Type { get; }

        public override string ToString() => $"步骤 {Step}: {Type}";
    }
}
