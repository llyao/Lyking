using System;
using System.Collections.Generic;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class AttributeSet
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);

        public event Action<string, double, double>? AttributeChanged;

        public double Get(string name)
        {
            ValidateName(name);
            return _values.TryGetValue(name, out double value) ? value : 0;
        }

        public void Set(string name, double value)
        {
            ValidateName(name);
            double before = Get(name);
            _values[name] = value;
            if (before != value)
            {
                AttributeChanged?.Invoke(name, before, value);
            }
        }

        public void ApplyDelta(string name, double delta) => Set(name, Get(name) + delta);

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("属性名称不能为空。", nameof(name));
            }
        }
    }
}
