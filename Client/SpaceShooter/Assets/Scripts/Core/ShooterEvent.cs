namespace Lyking.SpaceShooter.Core
{
    /// <summary>战局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum ShooterEventType
    {
        Spawned,
        EnemySpawned,
        Shot,
        EnemyKilled,
        EnemyEscaped,
        PlayerHit,
        LifeLost,
        WaveUp,
        GameOver,
    }

    /// <summary>一条带战局时刻的事件记录。</summary>
    public readonly struct ShooterEventLog
    {
        public ShooterEventLog(float time, ShooterEventType type)
        {
            Time = time;
            Type = type;
        }

        /// <summary>事件发生时的战局累计时间（秒）。</summary>
        public float Time { get; }

        public ShooterEventType Type { get; }

        public override string ToString() => $"时刻 {Time:0.0}s: {Type}";
    }
}
