#nullable enable
namespace Lyking.GameplayAbilities.Core
{
    public sealed class ActiveGameplayEffect
    {
        internal ActiveGameplayEffect(
            ActiveGameplayEffectHandle handle,
            string? sourceAbilityId,
            GameplayEffectSpec spec,
            double startTime,
            double? endTime)
        {
            Handle = handle;
            SourceAbilityId = sourceAbilityId;
            Spec = spec;
            StartTime = startTime;
            EndTime = endTime;
        }

        public ActiveGameplayEffectHandle Handle { get; }

        public string? SourceAbilityId { get; }

        public GameplayEffectSpec Spec { get; }

        public double StartTime { get; }

        public double? EndTime { get; }

        public bool IsInfinite => !EndTime.HasValue;
    }
}
