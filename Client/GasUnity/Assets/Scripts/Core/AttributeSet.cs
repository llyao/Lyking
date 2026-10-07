#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class AttributeSet
    {
        private readonly Dictionary<string, double> _baseValues = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<ActiveGameplayEffectHandle, IReadOnlyDictionary<string, double>> _contributions =
            new Dictionary<ActiveGameplayEffectHandle, IReadOnlyDictionary<string, double>>();

        public event Action<string, double, double>? AttributeChanged;

        internal event Action<string, double, double, string?, int?, double?>? GameplayAttributeChanged;

        public double Get(string name)
        {
            ValidateName(name);
            return CalculateEffectiveValue(name);
        }

        public double GetBase(string name)
        {
            ValidateName(name);
            return _baseValues.TryGetValue(name, out double value) ? value : 0;
        }

        public bool Contains(string name)
        {
            ValidateName(name);
            return _baseValues.ContainsKey(name) || _contributions.Values.Any(values => values.ContainsKey(name));
        }

        public void Set(string name, double value)
        {
            ValidateName(name);
            ValidateValue(value);
            double before = Get(name);
            double after = CalculateEffectiveValue(name, value);
            _baseValues[name] = value;
            if (before != after)
            {
                NotifyChanged(name, before, after);
            }
        }

        public void ApplyDelta(string name, double delta) => Set(name, GetBase(name) + delta);

        internal void SetSilently(string name, double value)
        {
            ValidateName(name);
            ValidateValue(value);
            double baseValue = value - GetContributionTotal(name);
            ValidateValue(baseValue);
            _baseValues[name] = baseValue;
        }

        internal bool TryAddContribution(
            ActiveGameplayEffectHandle handle,
            IReadOnlyDictionary<string, double> modifiers,
            out IReadOnlyList<AttributeChange> changes)
        {
            changes = Array.Empty<AttributeChange>();
            if (!handle.IsValid || modifiers is null || _contributions.ContainsKey(handle))
            {
                return false;
            }

            var copy = new Dictionary<string, double>(StringComparer.Ordinal);
            var pending = new List<AttributeChange>(modifiers.Count);
            foreach (KeyValuePair<string, double> modifier in modifiers.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ValidateName(modifier.Key);
                ValidateValue(modifier.Value);
                double before = Get(modifier.Key);
                double after = before + modifier.Value;
                ValidateValue(after);
                copy.Add(modifier.Key, modifier.Value);
                if (before != after)
                {
                    pending.Add(new AttributeChange(modifier.Key, before, after));
                }
            }

            _contributions.Add(handle, new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(copy));
            changes = pending;
            return true;
        }

        internal bool TryRemoveContribution(
            ActiveGameplayEffectHandle handle,
            out IReadOnlyList<AttributeChange> changes)
        {
            changes = Array.Empty<AttributeChange>();
            if (!_contributions.TryGetValue(handle, out IReadOnlyDictionary<string, double>? modifiers))
            {
                return false;
            }

            var pending = new List<AttributeChange>(modifiers.Count);
            foreach (KeyValuePair<string, double> modifier in modifiers.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                double before = Get(modifier.Key);
                double after = before - modifier.Value;
                if (!IsFinite(after))
                {
                    return false;
                }

                if (before != after)
                {
                    pending.Add(new AttributeChange(modifier.Key, before, after));
                }
            }

            _contributions.Remove(handle);
            changes = pending;
            return true;
        }

        internal void NotifyChanged(
            string name,
            double before,
            double after,
            string? sourceAbilityId = null,
            int? effectHandleId = null,
            double? timestamp = null)
        {
            Delegate[] gameplayHandlers = GameplayAttributeChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Delegate handler in gameplayHandlers)
            {
                try
                {
                    ((Action<string, double, double, string?, int?, double?>)handler)(
                        name,
                        before,
                        after,
                        sourceAbilityId,
                        effectHandleId,
                        timestamp);
                }
                catch
                {
                    // 玩法事件观察者失败不能破坏属性提交。
                }
            }

            Delegate[] handlers = AttributeChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Delegate handler in handlers)
            {
                try
                {
                    ((Action<string, double, double>)handler)(name, before, after);
                }
                catch
                {
                    // 观察者属于表现层，不能破坏属性提交。
                }
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("属性名称不能为空。", nameof(name));
            }
        }

        private static void ValidateValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "属性值必须是有限数值。");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double CalculateEffectiveValue(string name)
        {
            return CalculateEffectiveValue(name, GetBase(name));
        }

        private double CalculateEffectiveValue(string name, double baseValue)
        {
            double value = baseValue;
            foreach (IReadOnlyDictionary<string, double> modifiers in _contributions.Values)
            {
                if (modifiers.TryGetValue(name, out double delta))
                {
                    value += delta;
                }
            }

            ValidateValue(value);
            return value;
        }

        private double GetContributionTotal(string name)
        {
            double total = 0;
            foreach (IReadOnlyDictionary<string, double> modifiers in _contributions.Values)
            {
                if (modifiers.TryGetValue(name, out double delta))
                {
                    total += delta;
                }
            }

            ValidateValue(total);
            return total;
        }

        internal readonly struct AttributeChange
        {
            public AttributeChange(string name, double before, double after)
            {
                Name = name;
                Before = before;
                After = after;
            }

            public string Name { get; }

            public double Before { get; }

            public double After { get; }
        }
    }
}
