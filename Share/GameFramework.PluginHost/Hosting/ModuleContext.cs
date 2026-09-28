using System;
using System.Threading;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginHost.Hosting
{
    /// <summary>模块上下文实现：解析走根容器，健康上报走登记表。不向模块暴露容器实例。</summary>
    internal sealed class ModuleContext : IModuleContext
    {
        private readonly IModuleContainer _container;
        private readonly ModuleHealthRegistry _health;

        public ModuleContext(
            ModuleDescriptor descriptor,
            IModuleContainer container,
            ModuleHealthRegistry health,
            CancellationToken hostCancellationToken)
        {
            Module = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            HostCancellationToken = hostCancellationToken;
        }

        public ModuleDescriptor Module { get; }

        public CancellationToken HostCancellationToken { get; }

        public object Resolve(Type serviceType) => _container.Resolve(serviceType);

        public bool TryResolve(Type serviceType, out object? service) =>
            _container.TryResolve(serviceType, out service);

        public void ReportHealth(ModuleHealthState state, string? message = null) =>
            _health.SetState(Module.Id, state, message);
    }
}
