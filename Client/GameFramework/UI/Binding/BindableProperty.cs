using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.UI.Binding
{
    /// <summary>
    /// 可绑定属性：值变化（与旧值不等）时通知订阅者。
    /// UGUI/TMP 控件绑定在 Unity 适配层通过订阅 Changed 实现；本类型保持纯逻辑可测。
    /// </summary>
    public sealed class BindableProperty<T>
    {
        private T _value;

        public BindableProperty(T initialValue = default!)
        {
            _value = initialValue;
        }

        public T Value
        {
            get => _value;
            set => Set(value);
        }

        /// <summary>值变化事件：(旧值, 新值)。</summary>
        public event Action<T, T>? Changed;

        public void Set(T newValue)
        {
            if (EqualityComparer<T>.Default.Equals(_value, newValue))
            {
                return;
            }

            T oldValue = _value;
            _value = newValue;
            Changed?.Invoke(oldValue, newValue);
        }

        /// <summary>不触发通知的强制写入（初始化/回档场景）。</summary>
        public void SetSilently(T newValue) => _value = newValue;

        /// <summary>订阅变化并立即以当前值回调一次（界面初始化的常用模式）。</summary>
        public IDisposable Bind(Action<T, T> onChanged)
        {
            if (onChanged is null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            Changed += onChanged;
            onChanged(_value, _value);
            return new Subscription(this, onChanged);
        }

        private sealed class Subscription : IDisposable
        {
            private BindableProperty<T>? _owner;
            private readonly Action<T, T> _handler;

            public Subscription(BindableProperty<T> owner, Action<T, T> handler)
            {
                _owner = owner;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Changed -= _handler;
                _owner = null;
            }
        }
    }
}
