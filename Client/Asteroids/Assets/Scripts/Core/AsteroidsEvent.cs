namespace Lyking.Asteroids.Core
{
    /// <summary>战局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum AsteroidsEventType
    {
        GameStarted,
        WaveStarted,
        Shot,
        AsteroidSplit,
        ShipHit,
        LifeLost,
        Respawned,
        GameOver,
    }

    /// <summary>一条带战局时刻的事件记录；碎裂事件额外携带被毁小行星的档位。</summary>
    public readonly struct AsteroidsEventLog
    {
        public AsteroidsEventLog(float time, AsteroidsEventType type, AsteroidSize size)
        {
            Time = time;
            Type = type;
            Size = size;
        }

        /// <summary>事件发生时的战局累计时间（秒）。</summary>
        public float Time { get; }

        public AsteroidsEventType Type { get; }

        /// <summary>事件关联的小行星档位，仅 <see cref="AsteroidsEventType.AsteroidSplit"/> 使用，其余事件忽略。</summary>
        public AsteroidSize Size { get; }

        public override string ToString() =>
            Type == AsteroidsEventType.AsteroidSplit ? $"时刻 {Time:0.0}s: {Type}({Size})" : $"时刻 {Time:0.0}s: {Type}";
    }
}
