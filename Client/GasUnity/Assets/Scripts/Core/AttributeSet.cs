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

        public bool Contains(string name)
        {
            ValidateName(name);
            return _values.ContainsKey(name);
        }

        public void Set(string name, double value)
        {
            ValidateName(name);
            ValidateValue(value);
            double before = Get(name);
            _values[name] = value;
            if (before != value)
            {
                NotifyChanged(name, before, value);
            }
        }

        public void ApplyDelta(string name, double delta) => Set(name, Get(name) + delta);

        internal void SetSilently(string name, double value)
        {
            ValidateName(name);
            ValidateValue(value);
            _values[name] = value;
        }

        internal void NotifyChanged(string name, double before, double after)
        {
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
    }
}
