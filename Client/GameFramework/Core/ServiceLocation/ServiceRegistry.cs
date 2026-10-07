using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Lyking.GameFramework.Client.Core.ServiceLocation
{
    /// <summary>IServiceRegistry 默认实现。主线程使用为主，不保证线程安全。</summary>
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly Dictionary<(Type ServiceType, string Name), object> _instances =
            new Dictionary<(Type, string), object>();

        public int Count => _instances.Count;

        public void Register(Type serviceType, object instance)
        {
            Register(serviceType, string.Empty, instance);
        }

        public void Register(Type serviceType, string name, object instance)
        {
            if (serviceType is null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!serviceType.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"实例类型 {instance.GetType().FullName} 未实现服务类型 {serviceType.FullName}。",
                    nameof(instance));
            }

            var key = (serviceType, name);
            if (_instances.ContainsKey(key))
            {
                string label = string.IsNullOrEmpty(name) ? serviceType.FullName : $"{serviceType.FullName}({name})";
                throw new InvalidOperationException($"服务 {label} 已注册，不允许覆盖。");
            }

            _instances[key] = instance;
        }

        public void Register<TService>(TService instance) where TService : class
            => Register(typeof(TService), string.Empty, instance!);

        public void Register<TService>(string name, TService instance) where TService : class
            => Register(typeof(TService), name, instance!);

        public TService Resolve<TService>() where TService : class
            => Resolve<TService>(string.Empty);

        public TService Resolve<TService>(string name) where TService : class
        {
            if (TryResolve(name, out TService? service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"服务 {typeof(TService).FullName}({name}) 未注册。");
        }

        public bool TryResolve<TService>([NotNullWhen(true)] out TService? service) where TService : class
            => TryResolve(string.Empty, out service);

        public bool TryResolve<TService>(string name, [NotNullWhen(true)] out TService? service) where TService : class
        {
            if (_instances.TryGetValue((typeof(TService), name), out object? found) && found is TService typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public bool Unregister(Type serviceType) => Unregister(serviceType, string.Empty);

        public bool Unregister(Type serviceType, string name)
        {
            if (serviceType is null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            return _instances.Remove((serviceType, name ?? throw new ArgumentNullException(nameof(name))));
        }

        public void Clear() => _instances.Clear();
    }
}
