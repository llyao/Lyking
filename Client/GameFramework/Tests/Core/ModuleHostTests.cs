using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class ModuleHostTests
{
    /// <summary>带共享轨迹记录的测试模块。</summary>
    private sealed class TestModule : ClientModule
    {
        private readonly List<string> _trace;

        public TestModule(string name, List<string> trace, int priority = 0, IReadOnlyList<string>? dependencies = null)
            : base(name, priority, dependencies)
        {
            _trace = trace;
        }

        public Exception? ThrowOnInit { get; set; }

        public Exception? ThrowOnUpdate { get; set; }

        public Exception? ThrowOnShutdown { get; set; }

        public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            _trace.Add($"init:{Name}");
            if (ThrowOnInit != null)
            {
                throw ThrowOnInit;
            }

            return default;
        }

        public override void Start() => _trace.Add($"start:{Name}");

        public override void Update(double deltaTime)
        {
            _trace.Add($"update:{Name}");
            if (ThrowOnUpdate != null)
            {
                throw ThrowOnUpdate;
            }
        }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            _trace.Add($"shutdown:{Name}");
            if (ThrowOnShutdown != null)
            {
                throw ThrowOnShutdown;
            }

            return default;
        }
    }

    private static ModuleEnvironment CreateEnvironment()
    {
        var logger = new Logger("test", new DelegateLogSink((_, _) => { }));
        return new ModuleEnvironment(new ServiceRegistry(), logger);
    }

    [Fact]
    public async Task 依赖模块先初始化先启动先更新且关闭逆序()
    {
        var trace = new List<string>();
        var (host, env) = (new ModuleHost(CreateEnvironment().Logger), CreateEnvironment());
        var a = new TestModule("A", trace);
        var b = new TestModule("B", trace, dependencies: new[] { "A" });
        host.Add(b);
        host.Add(a);

        await host.InitializeAllAsync(env);
        host.StartAll();
        host.Update(0.016);
        await host.ShutdownAllAsync();

        Assert.Equal(
            new[] { "init:A", "init:B", "start:A", "start:B", "update:A", "update:B", "shutdown:B", "shutdown:A" },
            trace);
    }

    [Fact]
    public async Task 优先级数值小者先初始化()
    {
        var trace = new List<string>();
        var host = new ModuleHost(CreateEnvironment().Logger);
        var late = new TestModule("Late", trace, priority: 5);
        var early = new TestModule("Early", trace, priority: -1);
        var normal = new TestModule("Normal", trace, priority: 0);
        host.Add(late);
        host.Add(normal);
        host.Add(early);

        await host.InitializeAllAsync(CreateEnvironment());

        Assert.Equal(new[] { early, normal, late }, host.OrderedModules);
    }

    [Fact]
    public async Task 未知依赖抛排序异常()
    {
        var host = new ModuleHost(CreateEnvironment().Logger);
        host.Add(new TestModule("A", new List<string>(), dependencies: new[] { "不存在" }));

        var ex = await Assert.ThrowsAsync<ModuleOrderException>(() => host.InitializeAllAsync(CreateEnvironment()));

        Assert.Contains("不存在", ex.Message);
    }

    [Fact]
    public async Task 循环依赖抛排序异常()
    {
        var host = new ModuleHost(CreateEnvironment().Logger);
        host.Add(new TestModule("A", new List<string>(), dependencies: new[] { "B" }));
        host.Add(new TestModule("B", new List<string>(), dependencies: new[] { "A" }));

        await Assert.ThrowsAsync<ModuleOrderException>(() => host.InitializeAllAsync(CreateEnvironment()));
    }

    [Fact]
    public void 重名模块抛出()
    {
        var host = new ModuleHost(CreateEnvironment().Logger);
        host.Add(new TestModule("A", new List<string>()));
        Assert.Throws<ArgumentException>(() => host.Add(new TestModule("A", new List<string>())));
    }

    [Fact]
    public async Task 初始化失败时逆序回滚已初始化模块()
    {
        var trace = new List<string>();
        var host = new ModuleHost(CreateEnvironment().Logger);
        var a = new TestModule("A", trace);
        var b = new TestModule("B", trace, dependencies: new[] { "A" });
        var c = new TestModule("C", trace, dependencies: new[] { "B" }) { ThrowOnInit = new InvalidOperationException("C 爆了") };
        host.Add(a);
        host.Add(b);
        host.Add(c);

        var ex = await Assert.ThrowsAsync<ModuleStartupException>(() => host.InitializeAllAsync(CreateEnvironment()));

        Assert.Equal("C", ex.ModuleName);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(
            new[] { "init:A", "init:B", "init:C", "shutdown:B", "shutdown:A" },
            trace);
    }

    [Fact]
    public async Task 帧回调异常被隔离且后续模块继续更新()
    {
        var trace = new List<string>();
        var host = new ModuleHost(CreateEnvironment().Logger);
        var bad = new TestModule("Bad", trace) { ThrowOnUpdate = new InvalidOperationException("坏帧") };
        var good = new TestModule("Good", trace);
        host.Add(bad);
        host.Add(good);
        await host.InitializeAllAsync(CreateEnvironment());
        host.StartAll();

        var faults = new List<(IClientModule Module, Exception Exception)>();
        host.FrameFaulted += (m, e) => faults.Add((m, e));
        host.Update(0.016);

        Assert.Single(faults);
        Assert.Equal("Bad", faults[0].Module.Name);
        Assert.Equal(new[] { "update:Bad", "update:Good" }, trace.Where(t => t.StartsWith("update:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task 关闭失败聚合抛出但全部模块都被尝试关闭()
    {
        var trace = new List<string>();
        var host = new ModuleHost(CreateEnvironment().Logger);
        var a = new TestModule("A", trace) { ThrowOnShutdown = new InvalidOperationException("A 关不掉") };
        var b = new TestModule("B", trace);
        host.Add(a);
        host.Add(b);
        await host.InitializeAllAsync(CreateEnvironment());
        host.StartAll();

        var ex = await Assert.ThrowsAsync<ModuleShutdownException>(() => host.ShutdownAllAsync());

        Assert.Single(ex.Failures);
        Assert.Equal("A", ex.Failures[0].ModuleName);
        // B（逆序先关）与 A 都有 shutdown 轨迹。
        Assert.Equal(new[] { "shutdown:B", "shutdown:A" },
            trace.Where(t => t.StartsWith("shutdown:", StringComparison.Ordinal)));
    }

    [Fact]
    public void 排序前访问序列抛出()
    {
        var host = new ModuleHost(CreateEnvironment().Logger);
        Assert.Throws<InvalidOperationException>(() => host.OrderedModules);
    }

    [Fact]
    public async Task 启动序列与初始化序列一致()
    {
        var trace = new List<string>();
        var host = new ModuleHost(CreateEnvironment().Logger);
        host.Add(new TestModule("A", trace));
        host.Add(new TestModule("B", trace, dependencies: new[] { "A" }));

        await host.InitializeAllAsync(CreateEnvironment());
        host.StartAll();

        Assert.Equal(new[] { "init:A", "init:B", "start:A", "start:B" }, trace);
    }
}
