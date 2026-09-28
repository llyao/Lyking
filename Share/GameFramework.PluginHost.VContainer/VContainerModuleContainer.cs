using System;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using VContainer;

namespace Lyking.GameFramework.PluginHost.VContainer
{
    /// <summary>根容器视图。服务缺失时抛出 InvalidOperationException，保留 VContainer 原始异常。</summary>
    internal sealed class VContainerModuleContainer : IModuleContainer
    {
        private readonly IObjectResolver _resolver;

        public VContainerModuleContainer(IObjectResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public object Resolve(Type serviceType)
        {
            try
            {
                return _resolver.Resolve(serviceType);
            }
            catch (VContainerException failure)
            {
                throw new InvalidOperationException(
                    $"解析服务 {serviceType.FullName} 失败：{failure.Message}", failure);
            }
        }

        public bool TryResolve(Type serviceType, out object? service)
        {
            try
            {
                service = _resolver.Resolve(serviceType);
                return true;
            }
            catch (VContainerException)
            {
                service = null;
                return false;
            }
        }

        public IModuleScope CreateScope(string scopeName) =>
            new VContainerModuleScope(scopeName, _resolver.CreateScope());

        public ValueTask DisposeAsync()
        {
            _resolver.Dispose();
            return default;
        }
    }

    /// <summary>命名作用域视图。Scoped 服务按作用域独立，Singleton 与根共享。</summary>
    internal sealed class VContainerModuleScope : IModuleScope
    {
        private readonly IObjectResolver _resolver;

        public VContainerModuleScope(string name, IObjectResolver resolver)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string Name { get; }

        public object Resolve(Type serviceType)
        {
            try
            {
                return _resolver.Resolve(serviceType);
            }
            catch (VContainerException failure)
            {
                throw new InvalidOperationException(
                    $"作用域 {Name} 解析服务 {serviceType.FullName} 失败：{failure.Message}", failure);
            }
        }

        public bool TryResolve(Type serviceType, out object? service)
        {
            try
            {
                service = _resolver.Resolve(serviceType);
                return true;
            }
            catch (VContainerException)
            {
                service = null;
                return false;
            }
        }

        public ValueTask DisposeAsync()
        {
            _resolver.Dispose();
            return default;
        }
    }
}
