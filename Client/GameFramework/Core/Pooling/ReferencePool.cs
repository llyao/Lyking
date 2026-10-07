using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Core.Pooling
{
    /// <summary>可回池对象合同：归还前自清状态，避免脏数据跨租用泄漏。</summary>
    public interface IReference
    {
        void Clear();
    }

    /// <summary>
    /// 引用池：按类型缓存可复用类实例， Rent 无参构造创建或取缓存。
    /// 与 <see cref="ObjectPool{T}"/> 的区别：ObjectPool 面向特定对象的工厂+钩子，
    /// ReferencePool 面向"任意实现 IReference 的类"的通用租借（参考 GameFramework 引用池）。
    /// 重复归还被显式拒绝（按类型集合去重校验），杜绝双归还造成的状态串扰。
    /// 主线程使用，不保证线程安全。
    /// </summary>
    public sealed class ReferencePool
    {
        private readonly Dictionary<Type, Stack<object>> _pools = new Dictionary<Type, Stack<object>>();
        private readonly Dictionary<Type, HashSet<object>> _pooled = new Dictionary<Type, HashSet<object>>();

        /// <summary>累计租借次数。</summary>
        public long TotalRentCount { get; private set; }

        /// <summary>累计归还次数。</summary>
        public long TotalReturnCount { get; private set; }

        public T Rent<T>() where T : class, new()
        {
            T reference;
            if (_pools.TryGetValue(typeof(T), out Stack<object>? pool) && pool.Count > 0)
            {
                reference = (T)pool.Pop();
                _pooled[typeof(T)].Remove(reference);
            }
            else
            {
                reference = new T();
            }

            TotalRentCount++;
            return reference;
        }

        /// <summary>归还引用：先执行 IReference.Clear 再入池；重复归还抛 InvalidOperationException。</summary>
        public void Return<T>(T reference) where T : class
        {
            if (reference is null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            Type type = reference.GetType();
            if (_pooled.TryGetValue(type, out HashSet<object>? pooled) && !pooled.Add(reference))
            {
                throw new InvalidOperationException($"引用 {type.Name} 重复归还。");
            }

            if (reference is IReference clearable)
            {
                clearable.Clear();
            }

            if (!_pools.TryGetValue(type, out Stack<object>? pool))
            {
                pool = new Stack<object>();
                _pools[type] = pool;
                _pooled[type] = new HashSet<object>();
            }

            pool.Push(reference);
            _pooled[type].Add(reference);
            TotalReturnCount++;
        }

        /// <summary>指定类型的当前缓存数量。</summary>
        public int PooledCount<T>() where T : class =>
            _pools.TryGetValue(typeof(T), out Stack<object>? pool) ? pool.Count : 0;

        /// <summary>清空全部缓存（已租出对象不受影响）。</summary>
        public void Clear()
        {
            _pools.Clear();
            _pooled.Clear();
        }
    }

    /// <summary>
    /// 列表池：租借 List 并在归还时清空。静态全局共享，
    /// 归还前请确认没有长期持有该列表的引用。
    /// </summary>
    public static class ListPool<T>
    {
        [ThreadStatic]
        private static Stack<List<T>>? _pool;

        [ThreadStatic]
        private static HashSet<List<T>>? _pooled;

        public static List<T> Rent()
        {
            _pool ??= new Stack<List<T>>();
            if (_pool.Count > 0)
            {
                var list = _pool.Pop();
                _pooled!.Remove(list);
                return list;
            }

            return new List<T>();
        }

        public static void Return(List<T> list)
        {
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            _pool ??= new Stack<List<T>>();
            _pooled ??= new HashSet<List<T>>();
            if (!_pooled.Add(list))
            {
                throw new InvalidOperationException("列表重复归还。");
            }

            list.Clear();
            _pool.Push(list);
        }

        public static void Clear()
        {
            _pool?.Clear();
            _pooled?.Clear();
        }
    }
}
