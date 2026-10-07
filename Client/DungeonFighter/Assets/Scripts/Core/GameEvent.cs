namespace Lyking.DungeonFighter.Core
{
    /// <summary>对局事件类型，供表现层日志与测试断言。</summary>
    public enum GameEventType
    {
        AttackHit = 0,
        SkillUsed = 1,
        SkillDenied = 2,
        MonsterKilled = 3,
        PlayerHurt = 4,
        PlayerDied = 5,
        RoomEntered = 6,
        RoomCleared = 7,
        DoorOpened = 8,
        Victory = 9,
    }

    /// <summary>只读对局事件快照。</summary>
    public readonly struct GameEvent
    {
        public GameEvent(GameEventType type, string message, float timeSeconds)
        {
            Type = type;
            Message = message;
            TimeSeconds = timeSeconds;
        }

        public GameEventType Type { get; }

        public string Message { get; }

        public float TimeSeconds { get; }
    }
}
