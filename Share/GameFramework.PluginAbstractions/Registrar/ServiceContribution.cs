using System;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginAbstractions.Registrar
{
    /// <summary>一条服务贡献记录。ContributedBy 用于服务冲突与归属校验。</summary>
    public sealed class ServiceContribution
    {
        public ServiceContribution(
            ModuleId contributedBy,
            Type serviceType,
            Type implementationType,
            ModuleLifetime lifetime)
        {
            ContributedBy = contributedBy ?? throw new ArgumentNullException(nameof(contributedBy));
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
            Lifetime = lifetime;
            Instance = null;
        }

        public ServiceContribution(ModuleId contributedBy, Type serviceType, object instance)
        {
            ContributedBy = contributedBy ?? throw new ArgumentNullException(nameof(contributedBy));
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            ImplementationType = instance.GetType();
            Lifetime = ModuleLifetime.Singleton;
        }

        public ModuleId ContributedBy { get; }

        public Type ServiceType { get; }

        public Type ImplementationType { get; }

        public ModuleLifetime Lifetime { get; }

        /// <summary>实例贡献非空；此时 Lifetime 恒为 Singleton。</summary>
        public object? Instance { get; }
    }
}
