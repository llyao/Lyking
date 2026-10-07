#nullable enable
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
            : this(modifiers, grantedTags, durationSeconds, isInfinite: false)
        {
        }

        private GameplayEffectSpec(
            IReadOnlyDictionary<string, double> modifiers,
            GameplayTagContainer? grantedTags,
            double durationSeconds,
            bool isInfinite)
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

            if (!isInfinite && (durationSeconds < 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds)))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "效果持续时间必须是非负有限数值。");
            }

            Modifiers = new ReadOnlyDictionary<string, double>(copy);
            _grantedTags = grantedTags?.Clone() ?? new GameplayTagContainer();
            DurationSeconds = isInfinite ? 0 : durationSeconds;
            IsInfinite = isInfinite;
        }

        public static GameplayEffectSpec CreateInfinite(
            IReadOnlyDictionary<string, double> modifiers,
            GameplayTagContainer? grantedTags = null)
        {
            return new GameplayEffectSpec(modifiers, grantedTags, 0, isInfinite: true);
        }

        public IReadOnlyDictionary<string, double> Modifiers { get; }

        private readonly GameplayTagContainer _grantedTags;

        public GameplayTagContainer GrantedTags => _grantedTags.Clone();

        public double DurationSeconds { get; }

        public bool IsInfinite { get; }
    }
}
