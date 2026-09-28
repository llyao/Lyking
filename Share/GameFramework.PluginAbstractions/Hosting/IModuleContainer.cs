using System;

namespace Lyking.GameFramework.PluginAbstractions.Hosting
{
    /// <summary>
    /// 已物化的容器视图：按校验后的计划装配完成，供解析与派生作用域。
    /// 物化层（VContainer/MSDI/自有 Host）实现本接口。
    /// </summary>
    public interface IModuleContainer : IAsyncDisposable
    {
        /// <summary>解析服务；缺失时抛出 InvalidOperationException。</summary>
        object Resolve(Type serviceType);

        bool TryResolve(Type serviceType, out object? service);

        /// <summary>创建命名作用域。作用域内的 Scoped 服务独立，Singleton 与根共享。</summary>
        IModuleScope CreateScope(string scopeName);
    }

    /// <summary>命名作用域。Scoped 生命周期服务按作用域隔离。</summary>
    public interface IModuleScope : IAsyncDisposable
    {
        string Name { get; }

        object Resolve(Type serviceType);

        bool TryResolve(Type serviceType, out object? service);
    }
}
