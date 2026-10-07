using System.Collections.Generic;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>玩家数值与技能配置，第一版固定为鬼剑士单体配置。</summary>
    public sealed class PlayerSpec
    {
        public string DisplayName { get; set; } = "鬼剑士";

        public int MaxHealth { get; set; } = 100;

        public int MaxMana { get; set; } = 100;

        public float ManaRegenPerSecond { get; set; } = 6f;

        public float MoveSpeed { get; set; } = 6f;

        /// <summary>跳跃总时长（秒），空中期间免疫怪物近战。</summary>
        public float JumpDuration { get; set; } = 0.5f;

        /// <summary>跳跃最高点高度，仅供表现层换算屏幕偏移。</summary>
        public float JumpHeight { get; set; } = 1.5f;

        /// <summary>连击窗口：上一段命中后该时间内再按普攻进入下一段。</summary>
        public float ComboWindowSeconds { get; set; } = 0.9f;

        /// <summary>玩家受击后的无敌帧，避免同帧多次结算。</summary>
        public float HurtInvulnSeconds { get; set; } = 0.4f;

        /// <summary>三段普攻规格。</summary>
        public IReadOnlyList<MeleeAttackSpec> ComboSteps { get; set; } = CreateDefaultCombo();

        /// <summary>上挑：近战技能，长硬直高伤害。</summary>
        public SkillSpec Uppercut { get; set; } = CreateUppercut();

        /// <summary>烈焰波：穿透弹道技能。</summary>
        public SkillSpec FireWave { get; set; } = CreateFireWave();

        private static List<MeleeAttackSpec> CreateDefaultCombo() => new List<MeleeAttackSpec>
        {
            new MeleeAttackSpec
            {
                Name = "普攻一段",
                Damage = 12,
                Reach = 1.7f,
                HalfWidthY = 1.2f,
                Knockback = 0.3f,
                StunSeconds = 0.3f,
                LungeDistance = 0.45f,
                Duration = 0.3f,
                ActiveDelay = 0.12f,
            },
            new MeleeAttackSpec
            {
                Name = "普攻二段",
                Damage = 14,
                Reach = 1.7f,
                HalfWidthY = 1.2f,
                Knockback = 0.35f,
                StunSeconds = 0.3f,
                LungeDistance = 0.5f,
                Duration = 0.3f,
                ActiveDelay = 0.12f,
            },
            new MeleeAttackSpec
            {
                Name = "普攻三段",
                Damage = 22,
                Reach = 1.9f,
                HalfWidthY = 1.2f,
                Knockback = 1.3f,
                StunSeconds = 0.45f,
                LungeDistance = 0.6f,
                Duration = 0.38f,
                ActiveDelay = 0.15f,
            },
        };

        private static SkillSpec CreateUppercut() => new SkillSpec
        {
            Name = "上挑",
            MpCost = 15,
            CooldownSeconds = 5f,
            Melee = new MeleeAttackSpec
            {
                Name = "上挑",
                Damage = 26,
                Reach = 1.9f,
                HalfWidthY = 1.6f,
                Knockback = 0.8f,
                StunSeconds = 1.4f,
                LungeDistance = 0.35f,
                Duration = 0.45f,
                ActiveDelay = 0.16f,
            },
        };

        private static SkillSpec CreateFireWave() => new SkillSpec
        {
            Name = "烈焰波",
            MpCost = 25,
            CooldownSeconds = 8f,
            Projectile = new ProjectileSpec
            {
                Damage = 38,
                Speed = 10f,
                MaxDistance = 16f,
                HalfWidthY = 1f,
                StunSeconds = 0.35f,
                Knockback = 0.6f,
                Pierces = true,
            },
        };
    }
}
