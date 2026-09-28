using System;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginAbstractions.Registrar
{
    /// <summary>
    /// 贡献中间表示。模块在 Contribute 阶段只写这里，不直接触碰 VContainer/MSDI，
    /// 保证同一份组合计划可以物化到不同容器。
    /// </summary>
    public interface IModuleRegistrar
    {
        /// <summary>注册一个由容器构造的类型服务。implementationType 必须可赋给 serviceType。</summary>
        void Register(Type serviceType, Type implementationType, ModuleLifetime lifetime);

        /// <summary>注册一个已构造的实例（Singleton 语义）。</summary>
        void RegisterInstance(Type serviceType, object instance);

        void Register<TService, TImplementation>(ModuleLifetime lifetime)
            where TImplementation : TService
            => Register(typeof(TService), typeof(TImplementation), lifetime);

        void RegisterInstance<TService>(TService instance)
            where TService : notnull
            => RegisterInstance(typeof(TService), instance!);
    }
}
