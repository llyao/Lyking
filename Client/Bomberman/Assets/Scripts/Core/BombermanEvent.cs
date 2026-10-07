namespace Lyking.Bomberman.Core
{
    /// <summary>战局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum BombermanEventType
    {
        /// <summary>装载或切换关卡（含开局装载第一关）。</summary>
        LevelStarted,

        /// <summary>玩家移动一格。</summary>
        Moved,

        /// <summary>移动被拒绝：目标格是硬墙、软墙或炸弹。</summary>
        MoveRejected,

        /// <summary>放置一枚炸弹。</summary>
        BombPlaced,

        /// <summary>炸弹爆炸（含被火焰连锁引爆）。</summary>
        Explosion,

        /// <summary>火焰摧毁一面软墙。</summary>
        WallDestroyed,

        /// <summary>火焰炸死一名敌人。</summary>
        EnemyKilled,

        /// <summary>火焰烧到玩家。</summary>
        PlayerBurned,

        /// <summary>玩家失去一条生命（被火焰烧到或碰到敌人），回到出生点。</summary>
        LifeLost,

        /// <summary>火焰摧毁藏有出口的软墙，出口显露。</summary>
        ExitRevealed,

        /// <summary>敌人全灭且玩家走上显露的出口，过关进入下一关。</summary>
        LevelCleared,

        /// <summary>生命耗尽，游戏结束。</summary>
        GameOver,
    }

    /// <summary>一条带战局时刻的事件记录。</summary>
    public readonly struct BombermanEventLog
    {
        public BombermanEventLog(float time, BombermanEventType type)
        {
            Time = time;
            Type = type;
        }

        /// <summary>事件发生时的战局累计时间（秒）。</summary>
        public float Time { get; }

        public BombermanEventType Type { get; }

        public override string ToString() => $"时刻 {Time:0.0}s: {Type}";
    }
}
