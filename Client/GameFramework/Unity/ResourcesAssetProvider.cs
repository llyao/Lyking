using System;
using System.Collections.Generic;
using System.Threading;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.Unity
{
    /// <summary>
    /// Resources 目录资源提供器（示例实现）：按 key 引用计数，全部句柄释放且
    /// unloadOnRelease 开启时对最后持有的实例调用 Resources.UnloadAsset。
    /// 注意：基于 Resources.LoadAsync&lt;Object&gt;，只覆盖主资源，
    /// 子资源（如图集内 Sprite）需要项目侧扩展。
    /// </summary>
    public sealed class ResourcesAssetProvider : IAssetProvider, IDisposable
    {
        private readonly Dictionary<string, int> _refCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<UnityEngine.Object>> _pinned = new Dictionary<string, List<UnityEngine.Object>>();
        private readonly bool _unloadOnRelease;
        private bool _disposed;

        public ResourcesAssetProvider(bool unloadOnRelease = false)
        {
            _unloadOnRelease = unloadOnRelease;
        }

        public async System.Threading.Tasks.ValueTask<IAssetHandle<T>> LoadAsync<T>(
            string key, CancellationToken cancellationToken) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("资源 key 不能为空。", nameof(key));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ResourcesAssetProvider));
            }

            var operation = UnityEngine.Resources.LoadAsync<UnityEngine.Object>(key);
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await System.Threading.Tasks.Task.Yield();
            }

            UnityEngine.Object? asset = operation.asset;
            if (asset == null)
            {
                throw new InvalidOperationException($"Resources 路径 {key} 未找到资源。");
            }

            if (asset is not T typed)
            {
                throw new InvalidOperationException(
                    $"资源 {key} 的类型 {asset.GetType().FullName} 与请求类型 {typeof(T).FullName} 不匹配。");
            }

            _refCounts[key] = _refCounts.TryGetValue(key, out int count) ? count + 1 : 1;
            if (!_pinned.TryGetValue(key, out List<UnityEngine.Object>? pinned))
            {
                pinned = new List<UnityEngine.Object>();
                _pinned[key] = pinned;
            }

            pinned.Add(asset);
            return new Handle(this, key, typed, asset);
        }

        public void Dispose()
        {
            _disposed = true;
            _refCounts.Clear();
            _pinned.Clear();
        }

        private void Release(string key, UnityEngine.Object asset)
        {
            if (_disposed || !_refCounts.TryGetValue(key, out int count))
            {
                return;
            }

            if (count > 1)
            {
                _refCounts[key] = count - 1;
                _pinned[key].Remove(asset);
                return;
            }

            _refCounts.Remove(key);
            _pinned.Remove(key);
            if (_unloadOnRelease && asset != null)
            {
                UnityEngine.Resources.UnloadAsset(asset);
            }
        }

        private sealed class Handle : IAssetHandle<T> where T : class
        {
            private ResourcesAssetProvider? _owner;
            private readonly string _key;
            private readonly UnityEngine.Object _asset;

            public Handle(ResourcesAssetProvider owner, string key, T value, UnityEngine.Object asset)
            {
                _owner = owner;
                _key = key;
                Value = value;
                _asset = asset;
            }

            public T Value { get; }

            public bool IsLoaded => _owner != null;

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.Release(_key, _asset);
                _owner = null;
            }
        }
    }
}
