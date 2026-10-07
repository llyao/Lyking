using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.ServiceLocation;

namespace Lyking.GameFramework.Client.Core.Modules
{
    /// <summary>模块初始化时可用的环境协作者。</summary>
    public sealed class ModuleEnvironment
    {
        public ModuleEnvironment(IServiceRegistry services, ILogger logger)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IServiceRegistry Services { get; }

        public ILogger Logger { get; }
    }

    /// <summary>
    /// 客户端模块合同。回调序列固定：InitializeAsync → Start → Update/FixedUpdate/LateUpdate（帧循环）→ ShutdownAsync。
    /// 初始化按依赖拓扑序执行，关闭严格逆序；帧回调按同一拓扑序执行。
    /// </summary>
    public interface IClientModule
    {
        /// <summary>模块名，唯一，用于依赖声明与诊断。</summary>
        string Name { get; }

        /// <summary>排序优先级：数值小者先初始化、先更新。依赖关系优先于优先级。</summary>
        int Priority { get; }

        /// <summary>依赖的模块名列表；必须指向已注册模块。</summary>
        IReadOnlyList<string> Dependencies { get; }

        ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken);

        void Start();

        void Update(double deltaTime);

        void FixedUpdate(double deltaTime);

        void LateUpdate(double deltaTime);

        ValueTask ShutdownAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 模块基类：默认全部回调为空实现，子类按需覆写；
    /// 初始化后可通过 <see cref="Environment"/> 与 <see cref="Resolve{T}"/> 访问协作者。
    /// 无参构造供特性发现（GameClientBuilder.WithModulesFrom）使用，模块名取类型名；
    /// 带参构造允许自定义模块名、优先级与依赖。
    /// </summary>
    public abstract class ClientModule : IClientModule
    {
        /// <summary>无参构造：模块名取类型名。</summary>
        protected ClientModule()
        {
            Name = GetType().Name;
            Dependencies = Array.Empty<string>();
        }

        protected ClientModule(string name, int priority = 0, IReadOnlyList<string>? dependencies = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("模块名不能为空。", nameof(name)) : name;
            Priority = priority;
            Dependencies = dependencies ?? Array.Empty<string>();
        }

        public string Name { get; }

        public int Priority { get; }

        public IReadOnlyList<string> Dependencies { get; }

        /// <summary>初始化阶段注入；初始化前访问抛 InvalidOperationException（带明确指引）。</summary>
        protected ModuleEnvironment Environment =>
            _environment ?? throw new InvalidOperationException(
                $"模块 {Name} 尚未初始化：Environment 由模块宿主在 InitializeAsync 前注入，请检查模块是否已随 GameClient 启动。");

        private ModuleEnvironment? _environment;

        protected ILogger Log => Environment.Logger;

        protected T Resolve<T>() where T : class => Environment.Services.Resolve<T>();

        protected bool TryResolve<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? service) where T : class
            => Environment.Services.TryResolve(out service);

        /// <summary>由模块宿主在用户初始化回调之前调用，保证回调内可直接使用日志与服务。</summary>
        internal void AttachEnvironment(ModuleEnvironment environment) => _environment = environment;

        public virtual ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken = default)
        {
            _environment ??= environment;
            return default;
        }

        public virtual void Start()
        {
        }

        public virtual void Update(double deltaTime)
        {
        }

        public virtual void FixedUpdate(double deltaTime)
        {
        }

        public virtual void LateUpdate(double deltaTime)
        {
        }

        public virtual ValueTask ShutdownAsync(CancellationToken cancellationToken = default) => default;
    }
}
