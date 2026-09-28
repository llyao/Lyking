using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;

namespace Lyking.GameFramework.PluginHost.Tests.Samples
{
    // ── 样例合同（HostAdapter / SimulationModule / PresentationAdapter 各一） ──

    public interface IGameClock
    {
        long Tick { get; }
    }

    public sealed class ManualClock : IGameClock
    {
        public long Tick { get; set; }
    }

    public sealed record CounterChanged(long Tick, int Amount, int NewValue);

    public interface IEventSink
    {
        void Publish(object fact);

        IReadOnlyList<object> Facts { get; }
    }

    public interface ICounterStore
    {
        int Value { get; }

        void Submit(int amount);

        /// <summary>消费全部待处理命令（FIFO），每条产生一个事实事件。</summary>
        void Step();

        void Drain();
    }

    // ── 样例实现 ──

    public sealed class EventLogSink : IEventSink
    {
        private readonly List<object> _facts = new List<object>();

        public IReadOnlyList<object> Facts => _facts;

        public void Publish(object fact) => _facts.Add(fact);
    }

    public sealed class CounterStore : ICounterStore
    {
        private readonly IEventSink _sink;
        private readonly IGameClock _clock;
        private readonly Queue<int> _pending = new Queue<int>();
        private bool _drained;

        public CounterStore(IEventSink sink, IGameClock clock)
        {
            _sink = sink;
            _clock = clock;
        }

        public int Value { get; private set; }

        public void Submit(int amount)
        {
            if (_drained)
            {
                throw new InvalidOperationException("计数器已排空，拒绝新命令。");
            }

            _pending.Enqueue(amount);
        }

        public void Step()
        {
            while (_pending.Count > 0)
            {
                var amount = _pending.Dequeue();
                Value += amount;
                _sink.Publish(new CounterChanged(_clock.Tick, amount, Value));
            }
        }

        public void Drain() => _drained = true;
    }

    // ── 样例模块 ──

    /// <summary>HostAdapter 样例：提供 IGameClock 能力。</summary>
    public sealed class ClockModule : IGameModule
    {
        private readonly ManualClock _clock;
        private readonly TraceLog? _trace;

        public ClockModule(ManualClock? clock = null, TraceLog? trace = null)
        {
            _clock = clock ?? new ManualClock();
            _trace = trace;
        }

        public ModuleDescriptor Describe() => new ModuleDescriptor(
            "framework.clock",
            new ModuleVersion(1, 0),
            ModuleKind.HostAdapter,
            "framework.clock",
            capabilities: new ModuleCapabilities("clock"),
            description: "提供逻辑时钟的宿主适配样例");

        public void Contribute(IModuleRegistrar registrar) =>
            registrar.RegisterInstance<IGameClock>(_clock);

        public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("start:framework.clock");
            return ValueTask.CompletedTask;
        }

        public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("drain:framework.clock");
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("stop:framework.clock");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// SimulationModule 样例：贡献 ICounterStore；
    /// Start 时通过上下文解析协作者，验证构造函数注入链。
    /// 事实事件发布到 IEventSink（由表现模块贡献），模拟不关心表现实现。
    /// </summary>
    public sealed class CounterModule : IGameModule
    {
        private readonly TraceLog? _trace;

        public CounterModule(TraceLog? trace = null)
        {
            _trace = trace;
        }

        public ModuleDescriptor Describe() => new ModuleDescriptor(
            "game.counter",
            new ModuleVersion(1, 2),
            ModuleKind.SimulationModule,
            "game.counter",
            dependencies: new[]
            {
                new ModuleDependency("framework.clock", new ModuleVersion(1, 0)),
            },
            description: "命令队列 + FIFO 推进的计数模拟样例");

        public void Contribute(IModuleRegistrar registrar) =>
            registrar.Register<ICounterStore, CounterStore>(ModuleLifetime.Singleton);

        public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            // 启动时解析一次并持有；运行期不得反复 Resolve（热路径规则）。
            _ = context.Resolve<ICounterStore>();
            _trace?.Add("start:game.counter");
            return ValueTask.CompletedTask;
        }

        public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            context.Resolve<ICounterStore>().Drain();
            _trace?.Add("drain:game.counter");
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("stop:game.counter");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>PresentationAdapter 样例：贡献 IEventSink，只消费事实事件，不反写模拟状态。</summary>
    public sealed class EventLogModule : IGameModule
    {
        private readonly EventLogSink _sink;
        private readonly TraceLog? _trace;

        public EventLogModule(EventLogSink sink, TraceLog? trace = null)
        {
            _sink = sink;
            _trace = trace;
        }

        public ModuleDescriptor Describe() => new ModuleDescriptor(
            "presentation.eventlog",
            new ModuleVersion(1, 0),
            ModuleKind.PresentationAdapter,
            "presentation.eventlog",
            dependencies: new[]
            {
                new ModuleDependency("game.counter", new ModuleVersion(1, 0)),
            },
            isRequired: false,
            description: "事件日志表现适配样例");

        public void Contribute(IModuleRegistrar registrar) =>
            registrar.RegisterInstance<IEventSink>(_sink);

        public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _ = context.Resolve<IEventSink>();
            _trace?.Add("start:presentation.eventlog");
            return ValueTask.CompletedTask;
        }

        public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("drain:presentation.eventlog");
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add("stop:presentation.eventlog");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>可配置失败模块：用于降级、回滚、drain/stop 失败等路径。</summary>
    public sealed class FailingModule : IGameModule
    {
        private readonly string _id;
        private readonly bool _isRequired;
        private readonly string _failPhase;
        private readonly IReadOnlyList<ModuleDependency>? _dependencies;
        private readonly TraceLog? _trace;

        public FailingModule(
            string id,
            bool isRequired = true,
            string failPhase = "start",
            ModuleDependency[]? dependencies = null,
            TraceLog? trace = null)
        {
            _id = id;
            _isRequired = isRequired;
            _failPhase = failPhase;
            _dependencies = dependencies;
            _trace = trace;
        }

        public ModuleDescriptor Describe() => new ModuleDescriptor(
            _id,
            new ModuleVersion(1, 0),
            ModuleKind.HostAdapter,
            $"test.{_id}",
            dependencies: _dependencies,
            isRequired: _isRequired);

        public void Contribute(IModuleRegistrar registrar)
        {
        }

        public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add($"start:{_id}");
            if (_failPhase == "start")
            {
                throw new InvalidOperationException($"{_id} 启动失败（测试注入）");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add($"drain:{_id}");
            if (_failPhase == "drain")
            {
                throw new InvalidOperationException($"{_id} 排空失败（测试注入）");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            _trace?.Add($"stop:{_id}");
            if (_failPhase == "stop")
            {
                throw new InvalidOperationException($"{_id} 停止失败（测试注入）");
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>无行为模块：仅用于描述符级校验（重复 Id、能力冲突、命名空间冲突、拓扑）。</summary>
    public sealed class DescriptorOnlyModule : IGameModule
    {
        private readonly ModuleDescriptor _descriptor;

        public DescriptorOnlyModule(ModuleDescriptor descriptor) => _descriptor = descriptor;

        public ModuleDescriptor Describe() => _descriptor;

        public void Contribute(IModuleRegistrar registrar)
        {
        }

        public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>生命周期中间件：记录钩子顺序与耗时。</summary>
    public sealed class RecordingMiddleware : IModuleLifecycleMiddleware
    {
        private readonly TraceLog _trace;

        public RecordingMiddleware(TraceLog trace)
        {
            _trace = trace;
        }

        public ValueTask OnModuleStarting(PlannedModule module, CancellationToken cancellationToken)
        {
            _trace.Add($"middleware:starting:{module.Descriptor.Id.Value}");
            return ValueTask.CompletedTask;
        }

        public ValueTask OnModuleStarted(PlannedModule module, TimeSpan elapsed, CancellationToken cancellationToken)
        {
            _trace.Add($"middleware:started:{module.Descriptor.Id.Value}");
            return ValueTask.CompletedTask;
        }

        public ValueTask OnModuleFault(PlannedModule module, Exception failure, CancellationToken cancellationToken)
        {
            _trace.Add($"middleware:fault:{module.Descriptor.Id.Value}");
            return ValueTask.CompletedTask;
        }

        public ValueTask OnModuleStopped(PlannedModule module, TimeSpan elapsed, CancellationToken cancellationToken)
        {
            _trace.Add($"middleware:stopped:{module.Descriptor.Id.Value}");
            return ValueTask.CompletedTask;
        }
    }
}
