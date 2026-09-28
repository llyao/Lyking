// 模块管理中间件全流程演示（design.md 3.9 节验收）。
// 运行：dotnet run --project Share/GameFramework.ModuleDemo
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using Lyking.GameFramework.PluginHost;
using Lyking.GameFramework.PluginHost.Hosting;
using Lyking.GameFramework.PluginHost.VContainer;

// ── 样例合同 ──
public interface IGameClock { long Tick { get; } }
public sealed class DemoClock : IGameClock { public long Tick { get; set; } }
public sealed record CounterChanged(long Tick, int Amount, int NewValue);
public interface IEventSink { void Publish(object fact); }
public sealed class ConsoleSink : IEventSink
{
    public void Publish(object fact) => Console.WriteLine($"      事件 → {fact}");
}
public interface ICounterStore
{
    int Value { get; }
    void Submit(int amount);
    void Step();
    void Drain();
}
public sealed class DemoCounterStore : ICounterStore
{
    private readonly IEventSink _sink;
    private readonly IGameClock _clock;
    private readonly Queue<int> _pending = new();
    private bool _drained;

    public DemoCounterStore(IEventSink sink, IGameClock clock) { _sink = sink; _clock = clock; }
    public int Value { get; private set; }

    public void Submit(int amount)
    {
        ObjectDisposedException.ThrowIf(_drained, "CounterStore");
        Console.WriteLine($"      命令 ← Submit({amount})");
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

// ── 样例模块（四类中的三类 + 一个必然失败的可选模块） ──
public sealed class ClockModule : IGameModule
{
    public ModuleDescriptor Describe() => new("framework.clock", new ModuleVersion(1, 0),
        ModuleKind.HostAdapter, "framework.clock", capabilities: new ModuleCapabilities("clock"));
    public void Contribute(IModuleRegistrar r) => r.RegisterInstance<IGameClock>(new DemoClock());
    public ValueTask StartAsync(IModuleContext c, CancellationToken ct) { Console.WriteLine("      [framework.clock] 启动（HostAdapter：提供逻辑时钟）"); return ValueTask.CompletedTask; }
    public ValueTask DrainAsync(IModuleContext c, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask StopAsync(IModuleContext c, CancellationToken ct) { Console.WriteLine("      [framework.clock] 停止"); return ValueTask.CompletedTask; }
}

public sealed class CounterModule : IGameModule
{
    public ModuleDescriptor Describe() => new("game.counter", new ModuleVersion(1, 2),
        ModuleKind.SimulationModule, "game.counter",
        dependencies: [new ModuleDependency("framework.clock", new ModuleVersion(1, 0))]);
    public void Contribute(IModuleRegistrar r) => r.Register<ICounterStore, DemoCounterStore>(ModuleLifetime.Singleton);
    public ValueTask StartAsync(IModuleContext c, CancellationToken ct) { _ = c.Resolve<ICounterStore>(); Console.WriteLine("      [game.counter] 启动（SimulationModule：构造函数注入 IGameClock + IEventSink）"); return ValueTask.CompletedTask; }
    public ValueTask DrainAsync(IModuleContext c, CancellationToken ct) { c.Resolve<ICounterStore>().Drain(); Console.WriteLine("      [game.counter] 排空：拒绝新命令"); return ValueTask.CompletedTask; }
    public ValueTask StopAsync(IModuleContext c, CancellationToken ct) { Console.WriteLine("      [game.counter] 停止"); return ValueTask.CompletedTask; }
}

public sealed class EventLogModule : IGameModule
{
    public ModuleDescriptor Describe() => new("presentation.eventlog", new ModuleVersion(1, 0),
        ModuleKind.PresentationAdapter, "presentation.eventlog",
        dependencies: [new ModuleDependency("game.counter", new ModuleVersion(1, 0))], isRequired: false);
    public void Contribute(IModuleRegistrar r) => r.RegisterInstance<IEventSink>(new ConsoleSink());
    public ValueTask StartAsync(IModuleContext c, CancellationToken ct) { Console.WriteLine("      [presentation.eventlog] 启动（PresentationAdapter：只消费事实事件）"); return ValueTask.CompletedTask; }
    public ValueTask DrainAsync(IModuleContext c, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask StopAsync(IModuleContext c, CancellationToken ct) { Console.WriteLine("      [presentation.eventlog] 停止"); return ValueTask.CompletedTask; }
}

public sealed class BrokenMetricsModule : IGameModule
{
    public ModuleDescriptor Describe() => new("demo.metrics", new ModuleVersion(1, 0),
        ModuleKind.ToolPlugin, "demo.metrics", isRequired: false);
    public void Contribute(IModuleRegistrar r) { }
    public ValueTask StartAsync(IModuleContext c, CancellationToken ct) => throw new InvalidOperationException("指标后端不可用（演示可选模块降级）");
    public ValueTask DrainAsync(IModuleContext c, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask StopAsync(IModuleContext c, CancellationToken ct) => ValueTask.CompletedTask;
}

// ── 生命周期中间件：记录每个模块的启动/停止耗时 ──
public sealed class ConsoleMiddleware : IModuleLifecycleMiddleware
{
    public ValueTask OnModuleStarting(PlannedModule m, CancellationToken ct) { Console.WriteLine($"      → 正在启动 {m.Descriptor.Id}"); return ValueTask.CompletedTask; }
    public ValueTask OnModuleStarted(PlannedModule m, TimeSpan elapsed, CancellationToken ct) { Console.WriteLine($"      ✓ {m.Descriptor.Id} 就绪（{elapsed.TotalMilliseconds:F1} ms）"); return ValueTask.CompletedTask; }
    public ValueTask OnModuleFault(PlannedModule m, Exception f, CancellationToken ct) { Console.WriteLine($"      ✗ {m.Descriptor.Id} 故障：{f.Message}"); return ValueTask.CompletedTask; }
    public ValueTask OnModuleStopped(PlannedModule m, TimeSpan elapsed, CancellationToken ct) { Console.WriteLine($"      ✓ {m.Descriptor.Id} 已停止"); return ValueTask.CompletedTask; }
}

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("== 阶段 1：Describe → Contribute → Plan → Validate → Apply(VContainer) ==");
        var host = ModuleHost.Build(
            new IGameModule[] { new BrokenMetricsModule(), new EventLogModule(), new CounterModule(), new ClockModule() },
            new VContainerModuleMaterializer(),
            new ConsoleMiddleware());
        Console.WriteLine($"   计划：{host.Plan}");

        Console.WriteLine();
        Console.WriteLine("== 阶段 2：Start（依赖正序；可选模块失败进入 Degraded）==");
        await host.StartAsync();
        foreach (var health in host.Health)
        {
            Console.WriteLine($"   健康：{health}");
        }

        Console.WriteLine();
        Console.WriteLine("== 阶段 3：Command → Simulation Step → Event ==");
        var store = host.Resolve<ICounterStore>();
        store.Submit(10);
        store.Submit(-3);
        store.Submit(7);
        Console.WriteLine("      Step()：按 FIFO 消费命令");
        store.Step();
        Console.WriteLine($"      当前权威状态 Value = {store.Value}");

        Console.WriteLine();
        Console.WriteLine("== 阶段 4：CreateScope（会话作用域共享根单例）==");
        await using var session = host.CreateScope("session-1");
        Console.WriteLine($"      作用域内 ICounterStore 与根一致：{ReferenceEquals(store, session.Resolve(typeof(ICounterStore)))}");

        Console.WriteLine();
        Console.WriteLine("== 阶段 5：Drain → Stop（逆拓扑序）==");
        await host.DrainAsync();
        try
        {
            store.Submit(1);
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("      排空后提交被拒绝 ✓");
        }

        await host.StopAsync();
        Console.WriteLine($"   宿主最终状态：{host.State}");
        Console.WriteLine();
        Console.WriteLine("全流程结束。对应测试：Share/GameFramework.PluginHost.Tests（22 个用例）。");
    }
}
