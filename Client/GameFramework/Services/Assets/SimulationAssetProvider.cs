using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.Services.Assets
{
    /// <summary>
    /// 模拟资源提供器：不做真实 IO，通过工厂表（缺省为无参构造）创建实例，
    /// 按 key 引用计数管理句柄。用于纯逻辑测试与无头演示；
    /// 真实加载行为由 Unity 适配层（Resources/Addressables）替换。
    /// </summary>
    public sealed class SimulationAssetProvider : IAssetProvider
    {
        private readonly Dictionary<string, Func<object>> _factories;
        private readonly Dictionary<string, int> _refCounts = new Dictionary<string, int>();
        private readonly ILogger? _logger;

        public SimulationAssetProvider(
            IReadOnlyDictionary<string, Func<object>>? factories = null,
            ILogger? logger = null)
        {
            _factories = factories != null
                ? new Dictionary<string, Func<object>>(factories)
                : new Dictionary<string, Func<object>>();
            _logger = logger;
        }

        /// <summary>key 的当前活跃句柄数，测试与诊断用。</summary>
        public int GetActiveCount(string key) =>
            _refCounts.TryGetValue(key, out int count) ? count : 0;

        /// <summary>活跃 key 数，诊断用。</summary>
        public int ActiveKeyCount => _refCounts.Count;

        public ValueTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("资源 key 不能为空。", nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!_factories.TryGetValue(key, out Func<object>? factory))
            {
                factory = () => Activator.CreateInstance(typeof(T))
                    ?? throw new InvalidOperationException($"资源 {key} 的类型 {typeof(T).FullName} 无法创建实例。");
            }

            object instance;
            try
            {
                instance = factory();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"模拟加载资源 {key} 失败。", ex);
            }

            if (instance is not T typed)
            {
                throw new InvalidOperationException(
                    $"资源 {key} 的实例类型 {instance.GetType().FullName} 与请求类型 {typeof(T).FullName} 不匹配。");
            }

            _refCounts[key] = GetActiveCount(key) + 1;
            _logger?.Debug($"模拟加载资源 {key}（当前 {GetActiveCount(key)} 个句柄）。");
            return new ValueTask<IAssetHandle<T>>(new Handle<T>(this, key, typed));
        }

        private void Release(string key)
        {
            int remaining = GetActiveCount(key) - 1;
            if (remaining > 0)
            {
                _refCounts[key] = remaining;
                _logger?.Debug($"释放资源 {key}（剩余 {remaining} 个句柄）。");
                return;
            }

            _refCounts.Remove(key);
            _logger?.Debug($"释放资源 {key}（引用归零）。");
        }

        private sealed class Handle<THandle> : IAssetHandle<THandle> where THandle : class
        {
            private SimulationAssetProvider? _owner;
            private readonly string _key;

            public Handle(SimulationAssetProvider owner, string key, THandle value)
            {
                _owner = owner;
                _key = key;
                Value = value;
            }

            public THandle Value { get; }

            public bool IsLoaded => _owner != null;

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Release(_key);
                _owner = null;
            }
        }
    }
}
