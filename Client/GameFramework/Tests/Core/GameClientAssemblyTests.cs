using System;
using System.Threading.Tasks;
using Lyking.GameFramework.Client;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class GameClientAssemblyTests
{
    [Fact]
    public void Builder服务配置阶段可解析主线程派发器()
    {
        IMainThreadDispatcher? configuredDispatcher = null;
        var client = GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .ConfigureServices(services => configuredDispatcher = services.Resolve<IMainThreadDispatcher>())
            .Build();

        Assert.Same(client.Dispatcher, configuredDispatcher);
    }

    [Fact]
    public void 构造时注册的派发器不可由服务配置覆盖()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GameClientBuilder.Create()
            .WithLogger(CreateLogger())
            .ConfigureServices(services => services.Register<IMainThreadDispatcher>(new MainThreadDispatcher()))
            .Build());

        Assert.Contains("已注册", ex.Message);
    }

    [Fact]
    public async Task 生命周期超时中止会完成已接受的派发等待者()
    {
        var client = new GameClient(CreateLogger());
        Task accepted = Task.Run(async () => await client.Dispatcher.InvokeAsync(() => { }));
        Assert.True(SpinWait.SpinUntil(() => client.Dispatcher.PendingCount == 1, TimeSpan.FromSeconds(2)));

        client.AbortPendingOperations();

        var ex = await Assert.ThrowsAsync<MainThreadDispatchException>(() => accepted);
        Assert.Equal(MainThreadDispatchFailure.ShutdownTimeout, ex.Failure);
        Assert.False(client.Dispatcher.IsAccepting);
    }

    private static ILogger CreateLogger() => new Logger("test", new DelegateLogSink((_, _) => { }));
}
