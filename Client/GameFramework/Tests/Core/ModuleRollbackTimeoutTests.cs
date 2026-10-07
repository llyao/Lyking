using System;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class ModuleRollbackTimeoutTests
{
    private sealed class SlowShutdownModule : ClientModule
    {
        private readonly TaskCompletionSource<bool> _shutdownGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SlowShutdownModule() : base("slow")
        {
        }

        public CancellationToken? CleanupToken { get; private set; }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            CleanupToken = cancellationToken;
            return new ValueTask(_shutdownGate.Task);
        }

        public void FinishShutdown() => _shutdownGate.TrySetResult(true);
    }

    private sealed class CancellationModule : ClientModule
    {
        private int _started;

        public CancellationModule() : base("cancel")
        {
        }

        public bool Started => Volatile.Read(ref _started) != 0;

        public override async ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _started, 1);
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class FastShutdownModule : ClientModule
    {
        public FastShutdownModule() : base("fast")
        {
        }

        public bool ShutdownAttempted { get; private set; }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            ShutdownAttempted = true;
            return default;
        }
    }

    [Fact]
    public async Task 回滚总时限到达后启动取消仍返回且清理令牌已取消()
    {
        var dispatcher = new MainThreadDispatcher();
        var host = new ModuleHost(
            new Logger("test", new DelegateLogSink((_, _) => { })),
            dispatcher,
            TimeSpan.FromMilliseconds(50));
        var slow = new SlowShutdownModule();
        var fast = new FastShutdownModule();
        var cancel = new CancellationModule();
        host.Add(fast);
        host.Add(slow);
        host.Add(cancel);
        var environment = new ModuleEnvironment(new ServiceRegistry(), new Logger("test", new DelegateLogSink((_, _) => { })));
        using var startup = new CancellationTokenSource();
        Task initialize = host.InitializeAllAsync(environment, startup.Token);

        WaitUntil(() => cancel.Started, dispatcher);
        startup.Cancel();
        PumpUntil(dispatcher, initialize);

        Assert.True(initialize.IsCanceled);
        Assert.True(slow.CleanupToken.HasValue);
        Assert.True(slow.CleanupToken!.Value.IsCancellationRequested);
        Assert.True(fast.ShutdownAttempted);

        slow.FinishShutdown();
        dispatcher.Shutdown(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize);
    }

    private static void PumpUntil(IMainThreadDispatcher dispatcher, Task task)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            dispatcher.Pump();
            Thread.SpinWait(10000);
        }

        Assert.True(task.IsCompleted, "启动回滚未在总时限内收敛。");
    }

    private static void WaitUntil(Func<bool> condition, IMainThreadDispatcher dispatcher)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            dispatcher.Pump();
            Thread.SpinWait(10000);
        }

        Assert.True(condition(), "取消模块没有开始初始化。");
    }
}
