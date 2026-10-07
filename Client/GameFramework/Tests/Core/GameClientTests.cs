using Lyking.GameFramework.Client;
using Lyking.GameFramework.Client.Core.Events;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.Scheduling;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class GameClientTests
{
    private sealed class EchoModule : ClientModule
    {
        public EchoModule() : base("Echo")
        {
        }

        public List<string> Trace { get; } = new();

        public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            Trace.Add("init");
            // 模块在初始化阶段就能解析到内置服务。
            HasEventBus = environment.Services.TryResolve<IEventBus>(out _);
            return default;
        }

        public bool HasEventBus { get; private set; }

        public override void Update(double deltaTime) => Trace.Add("update");

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            Trace.Add("shutdown");
            return default;
        }
    }

    private static GameClient CreateClient(params IClientModule[] modules) =>
        new(new Logger("test", new DelegateLogSink((_, _) => { })), modules);

    [Fact]
    public async Task 启动后注册内置服务并驱动模块()
    {
        var module = new EchoModule();
        var client = CreateClient(module);

        await client.StartAsync();
        client.Update(0.016);

        Assert.Equal(ClientState.Running, client.State);
        Assert.True(module.HasEventBus);
        Assert.Same(client.Events, client.Services.Resolve<IEventBus>());
        Assert.Same(client.Timers, client.Services.Resolve<TimerService>());
        Assert.Equal(new[] { "init", "update" }, module.Trace);
    }

    [Fact]
    public async Task 未启动时帧驱动抛出()
    {
        var client = CreateClient();

        Assert.Throws<InvalidOperationException>(() => client.Update(0.016));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ShutdownAsync());
    }

    [Fact]
    public async Task 重复启动抛出()
    {
        var client = CreateClient();
        await client.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartAsync());
    }

    [Fact]
    public async Task 模块装配重名失败后客户端仍可关闭()
    {
        var client = CreateClient(new EchoModule(), new EchoModule());

        await Assert.ThrowsAsync<ArgumentException>(() => client.StartAsync());

        Assert.Equal(ClientState.Failed, client.State);
        await client.ShutdownAsync();
        Assert.Equal(ClientState.Shutdown, client.State);
    }

    [Fact]
    public async Task 启动后不能追加模块()
    {
        var client = CreateClient();
        await client.StartAsync();

        Assert.Throws<InvalidOperationException>(() => client.AddModule(new EchoModule()));
    }

    [Fact]
    public async Task 关闭后状态与模块逆序关闭()
    {
        var module = new EchoModule();
        var client = CreateClient(module);
        await client.StartAsync();

        await client.ShutdownAsync();

        Assert.Equal(ClientState.Shutdown, client.State);
        Assert.Equal(new[] { "init", "shutdown" }, module.Trace);
    }

    [Fact]
    public async Task 关闭后帧驱动抛出()
    {
        var client = CreateClient();
        await client.StartAsync();
        await client.ShutdownAsync();

        Assert.Throws<InvalidOperationException>(() => client.Update(0.016));
    }
}
