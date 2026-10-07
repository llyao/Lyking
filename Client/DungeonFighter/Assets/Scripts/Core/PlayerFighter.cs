using System;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>技能独立状态：只跟踪冷却。</summary>
    public sealed class SkillState
    {
        public float CooldownRemaining { get; set; }

        public bool IsReady => CooldownRemaining <= 0f;
    }

    /// <summary>玩家单位：法力、三段连击进度、技能冷却与跳跃状态。</summary>
    public sealed class PlayerFighter : Fighter
    {
        public PlayerFighter(int id, PlayerSpec spec)
            : base(id, FighterTeam.Player, spec.DisplayName)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            MaxHealth = spec.MaxHealth;
            Health = MaxHealth;
            MoveSpeed = spec.MoveSpeed;
            Mana = spec.MaxMana;
        }

        public PlayerSpec Spec { get; }

        public float Mana { get; set; }

        /// <summary>当前连击段：0 = 未在连击中，1..3 = 对应普攻段。</summary>
        public int ComboIndex { get; set; }

        /// <summary>连击窗口剩余秒数，归零后连击链重置。</summary>
        public float ComboWindowTimer { get; set; }

        /// <summary>当前攻击动作的判定规格，未在攻击时为 null。</summary>
        public MeleeAttackSpec? CurrentAttack { get; set; }

        public float CurrentAttackDuration { get; set; }

        /// <summary>当前攻击是否属于普攻连击链（技能攻击不算连击段）。</summary>
        public bool CurrentAttackIsCombo { get; set; }

        /// <summary>攻击判定是否已结算（每次动作只结算一次）。</summary>
        public bool AttackHitApplied { get; set; }

        /// <summary>攻击动作中再次按普攻的缓冲标记，动作结束时接下一段。</summary>
        public bool BufferedAttack { get; set; }

        public SkillState Uppercut { get; } = new SkillState();

        public SkillState FireWave { get; } = new SkillState();

        /// <summary>跳跃已进行秒数。</summary>
        public float AirElapsed { get; set; }

        public bool IsJumping { get; set; }

        public override bool IsAirborne => IsJumping;

        /// <summary>当前跳跃高度，按正弦曲线插值，供表现层换算屏幕偏移。</summary>
        public float AirHeight => IsJumping
            ? Spec.JumpHeight * MathF.Sin(MathF.PI * Math.Min(AirElapsed / MathF.Max(Spec.JumpDuration, 0.001f), 1f))
            : 0f;
    }
}
