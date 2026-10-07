using System.Reflection;
using Lyking.GameFramework.Client;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

/// <summary>供扫描测试使用的样例模块（仅标注 [GameModule] 的类型会被 WithModulesFrom 注册）。</summary>
[GameModule("扫描-A", priority: 5, Dependencies = new[] { "扫描-B" })]
internal sealed class ScanModuleA : ClientModule
{
}

[GameModule("扫描-B", priority: -10)]
internal sealed class ScanModuleB : ClientModule
{
}

[GameModule("改名模块", priority: 7)]
internal sealed class AttributedSample : ClientModule
{
}

public sealed class GameClientBuilderTests
{
    private static ILogger CreateLogger() => new Logger("test", new DelegateLogSink((_, _) => { }));

    [Fact]
    public async Task 特性元数据覆盖模块名与优先级()
    {
        var client = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .WithModule(new AttributedSample())
            .Build();

        await client.StartAsync();

        // Timer 为 GameClient 内置模块，加上被适配的样例模块共 2 个。
        Assert.Equal(2, client.Modules.RegisteredModules.Count);
        var module = client.Modules.RegisteredModules.Single(m => m.Name == "改名模块");
        Assert.Equal(7, module.Priority);
        await client.ShutdownAsync();
    }

    [Fact]
    public async Task 程序集扫描只注册标注特性的模块()
    {
        var client = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .WithModulesFrom(typeof(GameClientBuilderTests).Assembly)
            .Build();

        await client.StartAsync();

        var names = client.Modules.RegisteredModules.Select(m => m.Name).ToList();
        Assert.Contains("扫描-A", names);
        Assert.Contains("扫描-B", names);
        var a = client.Modules.RegisteredModules.Single(m => m.Name == "扫描-A");
        Assert.Equal(new[] { "扫描-B" }, a.Dependencies);
        // Timer 为内置模块；同程序集内标注特性的三个样例模块全部注册，
        // 未标注特性的 IClientModule（如其他测试文件的私有桩模块）被忽略。
        Assert.Equal(4, names.Count);
        await client.ShutdownAsync();
    }

    [Fact]
    public void 重复模块名在Build阶段提前报错()
    {
        var builder = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .WithModule(new ScanModuleA());

        Assert.Throws<ArgumentException>(() => builder.WithModule(new ScanModuleA()));
    }

    [Fact]
    public void 服务配置在Build时生效()
    {
        var probe = new ProbeService();
        var client = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .ConfigureServices(services => services.Register(probe))
            .Build();

        Assert.Same(probe, client.Services.Resolve<ProbeService>());
    }

    [Fact]
    public async Task Builder装配的客户端完整生命周期()
    {
        var client = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .WithModule(new AttributedSample())
            .Build();

        await client.StartAsync();
        client.Update(0.016);
        await client.ShutdownAsync();

        Assert.Equal(ClientState.Shutdown, client.State);
    }

    private sealed class ProbeService
    {
    }
}

public sealed class ModuleProfilingTests
{
    [Fact]
    public async Task 开启Profiling后统计帧耗时()
    {
        var logger = new Logger("test", new DelegateLogSink((_, _) => { }));
        var host = new ModuleHost(logger);
        host.Add(new TickModule("A"));
        host.Add(new TickModule("B"));
        await host.InitializeAllAsync(new ModuleEnvironment(new ServiceRegistry(), logger));
        host.ProfilingEnabled = true;

        host.Update(0.016);
        host.Update(0.016);

        Assert.Equal(2, host.Profiles.Count);
        var profileA = host.Profiles["A"];
        Assert.Equal(2, profileA.FrameCount);
        Assert.True(profileA.LastMs >= 0);
        Assert.True(profileA.AverageMs >= 0);
        Assert.True(profileA.MaxMs >= profileA.LastMs - 1e-9);
        Assert.True(profileA.TotalMs >= profileA.LastMs - 1e-9);

        host.ResetProfiles();
        Assert.Empty(host.Profiles);
    }

    [Fact]
    public async Task 未开启Profiling不产生统计()
    {
        var logger = new Logger("test", new DelegateLogSink((_, _) => { }));
        var host = new ModuleHost(logger);
        host.Add(new TickModule("A"));
        await host.InitializeAllAsync(new ModuleEnvironment(new ServiceRegistry(), logger));

        host.Update(0.016);

        Assert.Empty(host.Profiles);
    }

    private sealed class TickModule : ClientModule
    {
        public TickModule(string name) : base(name)
        {
        }

        public override void Update(double deltaTime)
        {
        }
    }
}
