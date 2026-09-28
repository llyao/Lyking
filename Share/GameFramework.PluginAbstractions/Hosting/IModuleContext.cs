using System;
using System.Threading;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginAbstractions.Hosting
{
    /// <summary>
    /// 模块生命周期执行上下文。模块通过它解析协作服务与上报健康，
    /// 禁止把容器本身暴露给模块。
    /// </summary>
    public interface IModuleContext
    {
        ModuleDescriptor Module { get; }

        /// <summary>解析协作服务。必需服务缺失时抛出，暴露装配错误而不是静默为 null。</summary>
        object Resolve(Type serviceType);

        bool TryResolve(Type serviceType, out object? service);

        /// <summary>上报本模块健康变化；不影响其他模块的状态命名空间。</summary>
        void ReportHealth(ModuleHealthState state, string? message = null);

        /// <summary>宿主级取消令牌：宿主开始关闭后触发。</summary>
        CancellationToken HostCancellationToken { get; }
    }

    public static class ModuleContextExtensions
    {
        public static T Resolve<T>(this IModuleContext context) =>
            (T)context.Resolve(typeof(T));

        public static bool TryResolve<T>(this IModuleContext context, out T? service)
        {
            if (context.TryResolve(typeof(T), out var serviceObject))
            {
                service = (T)serviceObject!;
                return true;
            }

            service = default;
            return false;
        }
    }
}
