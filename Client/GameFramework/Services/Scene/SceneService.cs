using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Scene
{
    /// <summary>
    /// 场景加载抽象。真实实现（Unity SceneManager/Addressables）由 Unity 适配层提供；
    /// 模拟实现只维护当前场景名与加载事件，用于无头测试与演示。
    /// </summary>
    public interface ISceneService
    {
        /// <summary>当前场景名；尚未加载为 null。</summary>
        string? CurrentScene { get; }

        /// <summary>加载场景（同名单场景重复加载幂等成功）。</summary>
        ValueTask LoadAsync(string sceneName, CancellationToken cancellationToken = default);

        /// <summary>场景开始加载事件。</summary>
        event Action<string>? SceneLoading;

        /// <summary>场景加载完成事件。</summary>
        event Action<string>? SceneLoaded;
    }

    /// <summary>ISceneService 模拟实现。</summary>
    public sealed class SimulationSceneService : ISceneService
    {
        public string? CurrentScene { get; private set; }

        public event Action<string>? SceneLoading;

        public event Action<string>? SceneLoaded;

        public ValueTask LoadAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("场景名不能为空。", nameof(sceneName));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(CurrentScene, sceneName, StringComparison.Ordinal))
            {
                return default;
            }

            SceneLoading?.Invoke(sceneName);
            CurrentScene = sceneName;
            SceneLoaded?.Invoke(sceneName);
            return default;
        }
    }
}
