namespace Lyking.GameplayAbilities.Core
{
    public enum GameplayEventType
    {
        AbilityActivated,
        CostCommitted,
        EffectApplied,
        AttributeChanged,
        CooldownStarted,
        AbilityRejected,
    }

    public sealed class GameplayEvent
    {
        public GameplayEvent(
            GameplayEventType type,
            string abilityId,
            AbilityFailureReason reason = AbilityFailureReason.None,
            string? attributeName = null)
        {
            Type = type;
            AbilityId = abilityId;
            Reason = reason;
            AttributeName = attributeName;
        }

        public GameplayEventType Type { get; }

        public string AbilityId { get; }

        public AbilityFailureReason Reason { get; }

        public string? AttributeName { get; }
    }
}
