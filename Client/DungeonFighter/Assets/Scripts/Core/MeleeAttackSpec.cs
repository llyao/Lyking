namespace Lyking.DungeonFighter.Core
{
    /// <summary>近战攻击判定规格：玩家连击段、上挑与怪物挥击共用同一套判定参数。</summary>
    public sealed class MeleeAttackSpec
    {
        /// <summary>展示名。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>基础伤害。</summary>
        public int Damage { get; set; }

        /// <summary>攻击者朝向前方的判定距离。</summary>
        public float Reach { get; set; } = 1.7f;

        /// <summary>纵深容差：目标与攻击者的 |ΔY| 超过该值不命中。</summary>
        public float HalfWidthY { get; set; } = 1.2f;

        /// <summary>命中后目标被击退的瞬时位移。</summary>
        public float Knockback { get; set; } = 0.3f;

        /// <summary>受击硬直秒数。</summary>
        public float StunSeconds { get; set; } = 0.3f;

        /// <summary>起手时攻击者向前的贴步距离。</summary>
        public float LungeDistance { get; set; }

        /// <summary>整个攻击动作时长。</summary>
        public float Duration { get; set; } = 0.3f;

        /// <summary>从起手到判定时刻的延迟（前摇）。</summary>
        public float ActiveDelay { get; set; } = 0.12f;
    }
}
