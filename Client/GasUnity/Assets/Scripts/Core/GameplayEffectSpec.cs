using System;
using System.Collections.Generic;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class GameplayEffectSpec
    {
        public GameplayEffectSpec(
            IReadOnlyDictionary<string, double> modifiers,
            GameplayTagContainer? grantedTags = null,
            double durationSeconds = 0)
        {
            Modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
            GrantedTags = grantedTags?.Clone() ?? new GameplayTagContainer();
            DurationSeconds = durationSeconds;
        }

        public IReadOnlyDictionary<string, double> Modifiers { get; }

        public GameplayTagContainer GrantedTags { get; }

        public double DurationSeconds { get; }
    }
}
