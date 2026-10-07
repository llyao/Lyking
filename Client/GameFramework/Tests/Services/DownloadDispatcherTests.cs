using System;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Download;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class DownloadDispatcherTests
{
    private sealed class DeferredProgressAgent : IDownloadAgent
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Run { get; private set; } = Task.CompletedTask;

        public ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken)
        {
            Run = Task.Run(async () =>
            {
                onProgress(0.5);
                await _gate.Task.WaitAsync(cancellationToken);
                onProgress(1.0);
            });
            return new ValueTask(Run);
        }

        public void Complete() => _gate.TrySetResult(true);
    }

    private sealed class CancellationAgent : IDownloadAgent
    {
        public bool ObservedCancellation { get; private set; }

        public async ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }

    private sealed class SynchronousCancellationCompletionAgent : IDownloadAgent
    {
        private readonly TaskCompletionSource<bool> _completion = new();

        public Func<int>? ActiveCount { get; set; }

        public int? ActiveCountAfterCompletion { get; private set; }

        public ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                _completion.TrySetResult(true);
                ActiveCountAfterCompletion = ActiveCount?.Invoke();
            });
            return new ValueTask(_completion.Task);
        }
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    [Fact]
    public void 后台进度仅在主线程泵送时更新状态并触发事件()
    {
        int mainThread = Environment.CurrentManagedThreadId;
        var dispatcher = new MainThreadDispatcher(capacity: 8);
        var agent = new DeferredProgressAgent();
        var service = CreateService(dispatcher, agent);
        int progressThread = 0;
        int successThread = 0;
        service.DownloadProgress += _ => progressThread = Environment.CurrentManagedThreadId;
        service.DownloadSucceeded += _ => successThread = Environment.CurrentManagedThreadId;
        int id = service.AddDownload("download", "file");

        service.Update(0.016);
        WaitUntil(() => dispatcher.PendingCount > 0);

        Assert.Equal(0d, service.Find(id)!.Progress);
        Assert.Equal(0, progressThread);
        dispatcher.Pump();
        Assert.Equal(0.5, service.Find(id)!.Progress);
        Assert.Equal(mainThread, progressThread);

        agent.Complete();
        PumpUntil(dispatcher, () => service.Find(id)!.State == DownloadState.Succeeded);

        Assert.Equal(DownloadState.Succeeded, service.Find(id)!.State);
        Assert.Equal(mainThread, successThread);
    }

    [Fact]
    public async Task 关闭取消在途代理并泵送取消完成()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 8);
        var agent = new CancellationAgent();
        var service = CreateService(dispatcher, agent);
        int id = service.AddDownload("download", "file");
        service.Update(0.016);
        WaitUntil(() => service.ActiveCount == 1);

        Task shutdown = service.ShutdownAsync(CancellationToken.None).AsTask();
        PumpUntil(dispatcher, () => shutdown.IsCompleted);
        await shutdown;

        Assert.True(agent.ObservedCancellation);
        Assert.Equal(0, service.ActiveCount);
        Assert.Equal(DownloadState.Cancelled, service.Find(id)!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 关闭时取消回调同步完成代理不会破坏取消枚举(bool useDispatcher)
    {
        var dispatcher = useDispatcher ? new MainThreadDispatcher(capacity: 8) : null;
        var agent = new SynchronousCancellationCompletionAgent();
        var service = CreateService(dispatcher, agent);
        agent.ActiveCount = () => service.ActiveCount;
        int id = service.AddDownload("download", "file");

        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        try
        {
            service.Update(0.016);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Task shutdown = service.ShutdownAsync(CancellationToken.None).AsTask();
        if (dispatcher != null)
        {
            PumpUntil(dispatcher, () => shutdown.IsCompleted);
        }

        await shutdown;

        Assert.Equal(0, agent.ActiveCountAfterCompletion);
        Assert.Equal(0, service.ActiveCount);
        Assert.Equal(DownloadState.Cancelled, service.Find(id)!.State);
    }

    private static DownloadService CreateService(IMainThreadDispatcher? dispatcher, IDownloadAgent agent)
    {
        var services = new ServiceRegistry();
        if (dispatcher != null)
        {
            services.Register(dispatcher);
        }

        var environment = new ModuleEnvironment(services, new Logger("test", new DelegateLogSink((_, _) => { })));
        var service = new DownloadService(agent);
        service.InitializeAsync(environment, CancellationToken.None);
        return service;
    }

    private static void PumpUntil(IMainThreadDispatcher dispatcher, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            dispatcher.Pump();
            Thread.SpinWait(1000);
        }

        dispatcher.Pump();
        Assert.True(condition(), "下载服务没有在主线程泵送后收敛。");
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.SpinWait(1000);
        }

        Assert.True(condition(), "等待下载后台回调超时。");
    }
}
