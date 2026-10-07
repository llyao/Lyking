namespace Lyking.DungeonFighter.Core
{
    /// <summary>穿透弹道规格：烈焰波等飞行技能。</summary>
    public sealed class ProjectileSpec
    {
        public int Damage { get; set; }

        /// <summary>弹道速度（单位/秒）。</summary>
        public float Speed { get; set; } = 10f;

        /// <summary>最大飞行距离，超过后消失。</summary>
        public float MaxDistance { get; set; } = 16f;

        /// <summary>纵深容差：|ΔY| 超过该值不命中。</summary>
        public float HalfWidthY { get; set; } = 1f;

        public float StunSeconds { get; set; } = 0.35f;

        public float Knockback { get; set; } = 0.6f;

        /// <summary>是否穿透（命中后继续飞行）。第一版烈焰波恒为穿透。</summary>
        public bool Pierces { get; set; } = true;
    }

    /// <summary>技能规格：MP 消耗 + 冷却，并携带近战或弹道效果之一。</summary>
    public sealed class SkillSpec
    {
        public string Name { get; set; } = string.Empty;

        public int MpCost { get; set; }

        public float CooldownSeconds { get; set; }

        public MeleeAttackSpec? Melee { get; set; }

        public ProjectileSpec? Projectile { get; set; }
    }
}
