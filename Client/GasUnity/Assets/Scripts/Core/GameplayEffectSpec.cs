using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class GameplayEffectSpec
    {
        public GameplayEffectSpec(
            IReadOnlyDictionary<string, double> modifiers,
            GameplayTagContainer? grantedTags = null,
            double durationSeconds = 0)
        {
            if (modifiers is null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            var copy = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> modifier in modifiers)
            {
                if (string.IsNullOrWhiteSpace(modifier.Key))
                {
                    throw new ArgumentException("效果属性名称不能为空。", nameof(modifiers));
                }

                if (double.IsNaN(modifier.Value) || double.IsInfinity(modifier.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(modifiers), "效果增量必须是有限数值。");
                }

                copy.Add(modifier.Key, modifier.Value);
            }

            if (durationSeconds < 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "效果持续时间必须是非负有限数值。");
            }

            Modifiers = new ReadOnlyDictionary<string, double>(copy);
            GrantedTags = grantedTags?.Clone() ?? new GameplayTagContainer();
            DurationSeconds = durationSeconds;
        }

        public IReadOnlyDictionary<string, double> Modifiers { get; }

        public GameplayTagContainer GrantedTags { get; }

        public double DurationSeconds { get; }
    }
}
