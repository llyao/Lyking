using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Core.Modules
{
    /// <summary>
    /// 模块元数据特性：让模块类型自带名字、优先级与依赖声明，
    /// 配合 GameClientBuilder.WithModulesFrom 实现程序集级自动注册。
    /// 未标注特性时使用类型名与默认优先级。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GameModuleAttribute : Attribute
    {
        public GameModuleAttribute(string? name = null, int priority = 0)
        {
            Name = name;
            Priority = priority;
        }

        /// <summary>模块名；null 时取类型名。</summary>
        public string? Name { get; }

        public int Priority { get; }

        /// <summary>依赖的模块名列表（具名参数指定，如 Dependencies = new[]{"Timer"}）。</summary>
        public string[] Dependencies { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 模块元数据适配器：把特性声明的名字/优先级/依赖应用到既有模块实例上，
    /// 回调全部委托给内部实例。仅由 Builder 在装配时创建。
    /// </summary>
    internal sealed class ModuleMetadataAdapter : IClientModule
    {
        private readonly IClientModule _inner;

        public ModuleMetadataAdapter(IClientModule inner, string name, int priority, IReadOnlyList<string> dependencies)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Name = string.IsNullOrWhiteSpace(name) ? inner.Name : name;
            Priority = priority;
            Dependencies = dependencies;
        }

        public string Name { get; }

        public int Priority { get; }

        public IReadOnlyList<string> Dependencies { get; }

        /// <summary>被包装的原始模块，供宿主做 ClientModule 特化处理（如环境预注入）。</summary>
        internal IClientModule Inner => _inner;

        public ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
            => _inner.InitializeAsync(environment, cancellationToken);

        public void Start() => _inner.Start();

        public void Update(double deltaTime) => _inner.Update(deltaTime);

        public void FixedUpdate(double deltaTime) => _inner.FixedUpdate(deltaTime);

        public void LateUpdate(double deltaTime) => _inner.LateUpdate(deltaTime);

        public ValueTask ShutdownAsync(CancellationToken cancellationToken)
            => _inner.ShutdownAsync(cancellationToken);
    }

    /// <summary>模块元数据读取工具。</summary>
    internal static class ModuleMetadata
    {
        /// <summary>按特性包装模块；无特性时原样返回。</summary>
        public static IClientModule ApplyAttribute(IClientModule module, Type moduleType)
        {
            var attribute = (GameModuleAttribute?)Attribute.GetCustomAttribute(moduleType, typeof(GameModuleAttribute));
            if (attribute == null)
            {
                return module;
            }

            string name = attribute.Name ?? moduleType.Name;
            IReadOnlyList<string> dependencies = attribute.Dependencies ?? (IReadOnlyList<string>)Array.Empty<string>();
            return new ModuleMetadataAdapter(module, name, attribute.Priority, dependencies);
        }
    }
}
