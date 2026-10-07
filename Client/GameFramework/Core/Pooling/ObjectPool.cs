using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Core.Pooling
{
    /// <summary>
    /// 单线程对象池：Rent 取出（空则用工厂创建），Return 归还并缓存到容量上限。
    /// onRent/onReturn 钩子用于复位与清理状态。主线程使用，不保证线程安全。
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T>? _onRent;
        private readonly Action<T>? _onReturn;
        private readonly int _maxPooled;
        private readonly Stack<T> _items = new Stack<T>();

        public ObjectPool(Func<T> factory, Action<T>? onRent = null, Action<T>? onReturn = null, int maxPooled = 64)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onRent = onRent;
            _onReturn = onReturn;
            if (maxPooled < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooled), "缓存容量不能为负。");
            }

            _maxPooled = maxPooled;
        }

        /// <summary>当前缓存数量（不含已借出对象）。</summary>
        public int PooledCount => _items.Count;

        /// <summary>累计创建的对象数，诊断用。</summary>
        public int CreatedCount { get; private set; }

        public T Rent()
        {
            T item = _items.Count > 0 ? _items.Pop() : Create();
            _onRent?.Invoke(item);
            return item;
        }

        public void Return(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_items.Count >= _maxPooled)
            {
                return;
            }

            _onReturn?.Invoke(item);
            _items.Push(item);
        }

        /// <summary>清空缓存（已借出对象不受影响）。</summary>
        public void Clear() => _items.Clear();

        private T Create()
        {
            CreatedCount++;
            return _factory();
        }
    }
}
