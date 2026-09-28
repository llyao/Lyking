using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginHost.Composition;

namespace Lyking.GameFramework.PluginHost.Hosting
{
    /// <summary>
    /// 模块宿主：应用已校验计划 → 依赖正序启动 → Ready → Drain → 逆序停止。
    /// 本类型不保证线程安全；宿主生命周期调用应来自单一 owner（与运行内核一致）。
    /// 生命周期不依赖任何容器入口点，容器只负责物化对象图。
    /// </summary>
    public sealed class ModuleHost
    {
        private readonly CompositionPlan _plan;
        private readonly IModuleContainerMaterializer _materializer;
        private readonly IReadOnlyList<IModuleLifecycleMiddleware> _middleware;
        private readonly ModuleHealthRegistry _health = new ModuleHealthRegistry();
        private readonly CancellationTokenSource _hostCts = new CancellationTokenSource();
        private readonly Dictionary<string, IGameModule> _moduleById;

        private IModuleContainer? _container;
        private HostState _state = HostState.Created;

        private ModuleHost(
            CompositionPlan plan,
            IReadOnlyList<IGameModule> modules,
            IModuleContainerMaterializer materializer,
            IReadOnlyList<IModuleLifecycleMiddleware> middleware)
        {
            _plan = plan;
            _materializer = materializer;
            _middleware = middleware;
            _moduleById = new Dictionary<string, IGameModule>(StringComparer.Ordinal);
            for (var index = 0; index < plan.Modules.Count; index++)
            {
                _moduleById[plan.Modules[index].Descriptor.Id.Value] = modules[index];
            }
        }

        public CompositionPlan Plan => _plan;

        public HostState State => _state;

        public IReadOnlyList<ModuleHealthReport> Health
        {
            get
            {
                var order = _plan.Modules
                    .Select(m => new ModuleHealthRegistry.PlannedModuleRef(m.Descriptor.Id.Value))
                    .ToList();
                return _health.Snapshot(order);
            }
        }

        /// <summary>组合 → 校验 → 物化。校验失败抛出 ModuleCompositionException，不创建容器。</summary>
        public static ModuleHost Build(
            IEnumerable<IGameModule> modules,
            IModuleContainerMaterializer materializer,
            params IModuleLifecycleMiddleware[] middleware)
        {
            if (materializer is null)
            {
                throw new ArgumentNullException(nameof(materializer));
            }

            var moduleList = (modules ?? throw new ArgumentNullException(nameof(modules))).ToList();
            var composition = ModuleComposer.ComposeWithModules(moduleList);
            var host = new ModuleHost(composition.Plan, composition.Modules, materializer, middleware);
            host._container = materializer.Apply(composition.Plan);
            return host;
        }

        public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            EnsureState(HostState.Created, nameof(StartAsync));
            _state = HostState.Starting;

            var started = new List<StartedModule>();
            try
            {
                for (var ordinal = 0; ordinal < _plan.Modules.Count; ordinal++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var planned = _plan.Modules[ordinal];
                    var module = _moduleById[planned.Descriptor.Id.Value];
                    var descriptor = planned.Descriptor;
                    _health.Register(descriptor.Id);
                    _health.SetState(descriptor.Id, ModuleHealthState.Starting);

                    var context = new ModuleContext(descriptor, _container!, _health, _hostCts.Token);
                    await Notify(m => m.OnModuleStarting(planned, cancellationToken));

                    var clock = Stopwatch.StartNew();
                    try
                    {
                        await module.StartAsync(context, cancellationToken);
                    }
                    catch (Exception failure)
                    {
                        clock.Stop();
                        _health.SetState(descriptor.Id, ModuleHealthState.Failed, failure.Message);
                        await Notify(m => m.OnModuleFault(planned, failure, cancellationToken));

                        if (descriptor.IsRequired)
                        {
                            var rollback = await RollbackAsync(started, cancellationToken);
                            await DisposeContainerAsync();
                            _state = HostState.Failed;
                            throw new ModuleHostException(
                                descriptor.Id.Value,
                                $"必需模块 {descriptor.Id} 启动失败：{failure.Message}",
                                failure,
                                rollback);
                        }

                        // 可选模块进入显式降级，核心继续运行。
                        _health.SetState(descriptor.Id, ModuleHealthState.Degraded, failure.Message);
                        continue;
                    }

                    clock.Stop();
                    _health.SetReady(descriptor.Id, clock.Elapsed);
                    started.Add(new StartedModule(module, planned));
                    await Notify(m => m.OnModuleStarted(planned, clock.Elapsed, cancellationToken));
                }

                _state = HostState.Ready;
            }
            catch (ModuleHostException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                var rollback = await RollbackAsync(started, CancellationToken.None);
                await DisposeContainerAsync();
                _state = HostState.Failed;
                throw new ModuleHostException(
                    moduleId: null,
                    "模块启动被取消。",
                    new OperationCanceledException(cancellationToken),
                    rollback);
            }
        }

        /// <summary>按依赖正序排空：上游先停止产生新输入，下游随后收敛。排空后宿主拒绝新作用域。</summary>
        public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
        {
            if (_state == HostState.Draining)
            {
                return;
            }

            EnsureState(HostState.Ready, nameof(DrainAsync));
            _state = HostState.Draining;
            _hostCts.Cancel();

            var failures = new List<Exception>();
            for (var ordinal = 0; ordinal < _plan.Modules.Count; ordinal++)
            {
                var planned = _plan.Modules[ordinal];
                var module = _moduleById[planned.Descriptor.Id.Value];
                _health.SetState(planned.Descriptor.Id, ModuleHealthState.Draining);

                try
                {
                    await module.DrainAsync(CreateContext(planned), cancellationToken);
                }
                catch (Exception failure)
                {
                    _health.SetState(planned.Descriptor.Id, ModuleHealthState.Failed, failure.Message);
                    failures.Add(new ModuleHostException(
                        planned.Descriptor.Id.Value,
                        $"模块 {planned.Descriptor.Id} 排空失败：{failure.Message}",
                        failure,
                        rollback: null));
                }
            }

            if (failures.Count > 0)
            {
                throw new ModuleHostException(
                    moduleId: null,
                    $"排空阶段有 {failures.Count} 个模块失败。",
                    failures[0],
                    rollback: null);
            }
        }

        /// <summary>按逆拓扑序停止并释放容器。单模块停止失败不阻断其余模块，最后汇总。</summary>
        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            if (_state == HostState.Stopped)
            {
                return;
            }

            if (_state == HostState.Failed)
            {
                await DisposeContainerAsync();
                return;
            }

            if (_state != HostState.Ready && _state != HostState.Draining)
            {
                throw new InvalidOperationException($"当前状态 {_state} 不允许 {nameof(StopAsync)}。");
            }

            var failures = new List<Exception>();
            for (var ordinal = _plan.Modules.Count - 1; ordinal >= 0; ordinal--)
            {
                var planned = _plan.Modules[ordinal];
                var module = _moduleById[planned.Descriptor.Id.Value];
                var clock = Stopwatch.StartNew();
                try
                {
                    await module.StopAsync(CreateContext(planned), cancellationToken);
                    _health.SetState(planned.Descriptor.Id, ModuleHealthState.Stopped);
                    await Notify(m => m.OnModuleStopped(planned, clock.Elapsed, cancellationToken));
                }
                catch (Exception failure)
                {
                    clock.Stop();
                    _health.SetState(planned.Descriptor.Id, ModuleHealthState.Stopped, $"停止失败：{failure.Message}");
                    await Notify(m => m.OnModuleStopped(planned, clock.Elapsed, cancellationToken));
                    failures.Add(new ModuleHostException(
                        planned.Descriptor.Id.Value,
                        $"模块 {planned.Descriptor.Id} 停止失败：{failure.Message}",
                        failure,
                        rollback: null));
                }
            }

            await DisposeContainerAsync();
            _state = HostState.Stopped;

            if (failures.Count > 0)
            {
                throw new ModuleHostException(
                    moduleId: null,
                    $"停止阶段有 {failures.Count} 个模块失败。",
                    failures[0],
                    rollback: null);
            }
        }

        /// <summary>创建命名作用域。仅 Ready 状态允许；Drain 之后拒绝新作用域。</summary>
        public IModuleScope CreateScope(string scopeName)
        {
            EnsureState(HostState.Ready, nameof(CreateScope));
            if (string.IsNullOrWhiteSpace(scopeName))
            {
                throw new ArgumentException("作用域名称不能为空。", nameof(scopeName));
            }

            return _container!.CreateScope(scopeName);
        }

        public object Resolve(Type serviceType)
        {
            EnsureState(HostState.Ready, nameof(Resolve));
            return _container!.Resolve(serviceType);
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        private ModuleContext CreateContext(PlannedModule planned) =>
            new ModuleContext(planned.Descriptor, _container!, _health, _hostCts.Token);

        private async Task<IReadOnlyList<string>> RollbackAsync(
            IReadOnlyList<StartedModule> started,
            CancellationToken cancellationToken)
        {
            var rollback = new List<string>();
            for (var index = started.Count - 1; index >= 0; index--)
            {
                var planned = started[index].Planned;
                try
                {
                    await started[index].Module.StopAsync(
                        new ModuleContext(planned.Descriptor, _container!, _health, CancellationToken.None),
                        cancellationToken);
                    _health.SetState(planned.Descriptor.Id, ModuleHealthState.Stopped, "启动失败回滚");
                }
                catch (Exception failure)
                {
                    _health.SetState(
                        planned.Descriptor.Id,
                        ModuleHealthState.Failed,
                        $"回滚停止失败：{failure.Message}");
                }

                rollback.Add(planned.Descriptor.Id.Value);
            }

            return rollback;
        }

        private readonly struct StartedModule
        {
            public StartedModule(IGameModule module, PlannedModule planned)
            {
                Module = module;
                Planned = planned;
            }

            public IGameModule Module { get; }

            public PlannedModule Planned { get; }
        }

        private async Task Notify(Func<IModuleLifecycleMiddleware, ValueTask> hook)
        {
            foreach (var middleware in _middleware)
            {
                await hook(middleware);
            }
        }

        private async ValueTask DisposeContainerAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }

        private void EnsureState(HostState expected, string operation)
        {
            if (_state != expected)
            {
                throw new InvalidOperationException($"当前状态 {_state} 不允许 {operation}（需要 {expected}）。");
            }
        }
    }
}
