using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Assets
{
    /// <summary>资源句柄：持有加载结果，Dispose 时交还引用。</summary>
    public interface IAssetHandle<T> : IDisposable where T : class
    {
        T Value { get; }

        bool IsLoaded { get; }
    }

    /// <summary>
    /// 资源提供抽象：按 key 加载资源并返回可释放句柄。
    /// 允许同一 key 多次加载（引用计数由实现维护），全部句柄释放后实现方可回收资源本体。
    /// 真实平台实现（Resources/Addressables/AssetBundle）由 Unity 适配层提供。
    /// </summary>
    public interface IAssetProvider
    {
        ValueTask<IAssetHandle<T>> LoadAsync<T>(string key, CancellationToken cancellationToken) where T : class;
    }
}
