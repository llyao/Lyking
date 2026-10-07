using System;
using System.Diagnostics.CodeAnalysis;

namespace Lyking.GameFramework.Client.Core.ServiceLocation
{
    /// <summary>
    /// 轻量服务注册表：模块间共享协作者的单一入口。
    /// 只做实例登记与查找，不做构造注入；实例生命周期由注册方负责。
    /// 支持同名服务类型下的具名注册，用于同类多实例（如多个资源提供器）。
    /// </summary>
    public interface IServiceRegistry
    {
        /// <summary>注册实例；同一（类型，名称）重复注册抛 InvalidOperationException。</summary>
        void Register(Type serviceType, object instance);

        void Register(Type serviceType, string name, object instance);

        void Register<TService>(TService instance) where TService : class;

        void Register<TService>(string name, TService instance) where TService : class;

        /// <summary>按类型解析；未注册抛 InvalidOperationException。</summary>
        TService Resolve<TService>() where TService : class;

        TService Resolve<TService>(string name) where TService : class;

        bool TryResolve<TService>([NotNullWhen(true)] out TService? service) where TService : class;

        bool TryResolve<TService>(string name, [NotNullWhen(true)] out TService? service) where TService : class;

        /// <summary>移除注册，返回是否存在。</summary>
        bool Unregister(Type serviceType);

        bool Unregister(Type serviceType, string name);

        /// <summary>清空全部注册。</summary>
        void Clear();

        int Count { get; }
    }
}
