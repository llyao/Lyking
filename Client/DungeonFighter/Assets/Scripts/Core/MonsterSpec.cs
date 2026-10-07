namespace Lyking.DungeonFighter.Core
{
    /// <summary>怪物数值与 AI 参数规格。</summary>
    public sealed class MonsterSpec
    {
        /// <summary>怪物类型名，同时作为展示名与出生表主键。</summary>
        public string Kind { get; set; } = string.Empty;

        public int MaxHealth { get; set; }

        public float MoveSpeed { get; set; }

        public int Damage { get; set; }

        /// <summary>察觉玩家的 X 距离阈值。</summary>
        public float AggroRange { get; set; } = 7f;

        /// <summary>触发攻击的 X 距离阈值。</summary>
        public float AttackRangeX { get; set; } = 1.5f;

        /// <summary>触发攻击与判定的纵深容差。</summary>
        public float AttackHalfWidthY { get; set; } = 1.1f;

        /// <summary>前摇结束时的近战判定距离。</summary>
        public float MeleeReach { get; set; } = 1.5f;

        public float WindupSeconds { get; set; } = 0.55f;

        public float RecoverSeconds { get; set; } = 0.7f;

        public float AttackCooldownSeconds { get; set; } = 1.4f;

        /// <summary>渲染体宽（世界单位），供表现层使用。</summary>
        public float BodyWidth { get; set; } = 1.1f;

        /// <summary>渲染体高（世界单位），供表现层使用。</summary>
        public float BodyHeight { get; set; } = 1.5f;

        public static MonsterSpec CreateSlime() => new MonsterSpec
        {
            Kind = "史莱姆",
            MaxHealth = 30,
            MoveSpeed = 2.4f,
            Damage = 8,
            AttackRangeX = 1.2f,
            MeleeReach = 1.3f,
            BodyWidth = 1f,
            BodyHeight = 0.9f,
        };

        public static MonsterSpec CreateGoblin() => new MonsterSpec
        {
            Kind = "哥布林",
            MaxHealth = 50,
            MoveSpeed = 3.4f,
            Damage = 12,
            BodyWidth = 1.1f,
            BodyHeight = 1.5f,
        };

        public static MonsterSpec CreateGoblinKing() => new MonsterSpec
        {
            Kind = "哥布林之王",
            MaxHealth = 260,
            MoveSpeed = 2.8f,
            Damage = 20,
            AggroRange = 9f,
            AttackRangeX = 2.2f,
            MeleeReach = 2.4f,
            AttackHalfWidthY = 1.4f,
            WindupSeconds = 0.75f,
            RecoverSeconds = 0.9f,
            AttackCooldownSeconds = 1.8f,
            BodyWidth = 1.8f,
            BodyHeight = 2.4f,
        };
    }
}
