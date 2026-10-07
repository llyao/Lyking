namespace Lyking.DungeonFighter.Core
{
    /// <summary>怪物单位：携带 AI 状态机与攻击节奏。</summary>
    public sealed class MonsterFighter : Fighter
    {
        public MonsterFighter(int id, MonsterSpec spec, Vec2 spawnPosition)
            : base(id, FighterTeam.Monster, spec.Kind)
        {
            Spec = spec;
            MaxHealth = spec.MaxHealth;
            Health = spec.MaxHealth;
            MoveSpeed = spec.MoveSpeed;
            Position = spawnPosition;
        }

        public MonsterSpec Spec { get; }

        public MonsterAiState AiState { get; set; } = MonsterAiState.Idle;

        /// <summary>再次进入前摇前的冷却剩余。</summary>
        public float AttackCooldown { get; set; }

        /// <summary>本次前摇结束的挥击是否已结算。</summary>
        public bool AttackHitApplied { get; set; }
    }
}
