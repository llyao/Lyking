namespace Lyking.BattleCity.Core
{
    /// <summary>战局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum BattleCityEventType
    {
        /// <summary>战局创建。</summary>
        Spawned,

        /// <summary>玩家射击。</summary>
        Shot,

        /// <summary>敌方坦克射击。</summary>
        EnemyShot,

        /// <summary>敌方坦克从顶部出生格入场。</summary>
        EnemySpawned,

        /// <summary>砖墙被炮火摧毁。</summary>
        BrickDestroyed,

        /// <summary>敌我子弹迎面对消。</summary>
        BulletClashed,

        /// <summary>玩家子弹命中敌人，消灭计分。</summary>
        EnemyKilled,

        /// <summary>玩家坦克中弹。</summary>
        PlayerHit,

        /// <summary>玩家失去一条生命。</summary>
        LifeLost,

        /// <summary>基地被炮火击中。</summary>
        BaseHit,

        /// <summary>全歼本关敌人，进入下一关。</summary>
        LevelCleared,

        /// <summary>游戏结束（生命耗尽或基地被毁）。</summary>
        GameOver,
    }

    /// <summary>一条带战局时刻的事件记录。</summary>
    public readonly struct BattleCityEventLog
    {
        public BattleCityEventLog(float time, BattleCityEventType type)
        {
            Time = time;
            Type = type;
        }

        /// <summary>事件发生时的战局累计时间（秒）。</summary>
        public float Time { get; }

        public BattleCityEventType Type { get; }

        public override string ToString() => $"时刻 {Time:0.0}s: {Type}";
    }
}
