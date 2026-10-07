#nullable enable
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
        ActiveEffectApplied,
        ActiveEffectExpired,
        ActiveEffectRemoved,
        TagChanged,
    }

    public sealed class GameplayEvent
    {
        public GameplayEvent(
            GameplayEventType type,
            string abilityId,
            AbilityFailureReason reason = AbilityFailureReason.None,
            string? attributeName = null,
            int? effectHandleId = null,
            double? timestamp = null,
            string? tagName = null)
        {
            Type = type;
            AbilityId = abilityId;
            Reason = reason;
            AttributeName = attributeName;
            EffectHandleId = effectHandleId;
            Timestamp = timestamp;
            TagName = tagName;
        }

        public GameplayEventType Type { get; }

        public string AbilityId { get; }

        public AbilityFailureReason Reason { get; }

        public string? AttributeName { get; }

        public int? EffectHandleId { get; }

        public double? Timestamp { get; }

        public string? TagName { get; }
    }
}
