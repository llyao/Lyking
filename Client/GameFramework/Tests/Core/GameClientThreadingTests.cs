using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class GameClientThreadingTests
{
    private sealed class ThreadModule : ClientModule
    {
        private readonly List<int> _threads;
        private readonly bool _waitForCancellation;
        private int _initializationCount;

        public ThreadModule(string name, List<int> threads, bool waitForCancellation = false)
            : base(name)
        {
            _threads = threads;
            _waitForCancellation = waitForCancellation;
        }

        public CancellationToken? ShutdownToken { get; private set; }

        public int InitializationCount => Volatile.Read(ref _initializationCount);

        public override async ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            _threads.Add(System.Environment.CurrentManagedThreadId);
            Interlocked.Increment(ref _initializationCount);
            if (_waitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            else
            {
                await Task.Run(() => Thread.Sleep(5));
                _threads.Add(System.Environment.CurrentManagedThreadId);
            }
        }

        public override void Start() => _threads.Add(System.Environment.CurrentManagedThreadId);

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            ShutdownToken = cancellationToken;
            _threads.Add(System.Environment.CurrentManagedThreadId);
            return default;
        }
    }

    [Fact]
    public void Headless异步启动在显式泵送下回到创建线程执行模块生命周期()
    {
        int ownerThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        var client = CreateClient(new ThreadModule("async", threads), new ThreadModule("next", threads));
        Task start = client.StartAsync();

        PumpUntil(client, start);

        Assert.Equal(ClientState.Running, client.State);
        Assert.NotEmpty(threads);
        Assert.All(threads, thread => Assert.Equal(ownerThread, thread));

        Task shutdown = client.ShutdownAsync();
        PumpUntil(client, shutdown);
        Assert.Equal(ClientState.Shutdown, client.State);
    }

    [Fact]
    public void 启动取消后的回滚使用独立未取消令牌()
    {
        var threads = new List<int>();
        var initialized = new ThreadModule("ready", threads);
        var waiting = new ThreadModule("waiting", threads, waitForCancellation: true);
        var client = CreateClient(initialized, waiting);
        using var startup = new CancellationTokenSource();
        Task start = client.StartAsync(startup.Token);

        WaitUntil(() => waiting.InitializationCount > 0, client);
        startup.Cancel();
        PumpUntil(client, start, expectFailure: true);

        Assert.NotNull(initialized.ShutdownToken);
        Assert.False(initialized.ShutdownToken!.Value.IsCancellationRequested);
    }

    private static GameClient CreateClient(params IClientModule[] modules) => new(
        new Logger("test", new DelegateLogSink((_, _) => { })),
        modules);

    private static void PumpUntil(GameClient client, Task task, bool expectFailure = false)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            client.Dispatcher.Pump();
            Thread.Sleep(1);
        }

        Assert.True(task.IsCompleted, "生命周期任务未在显式主线程泵送后收敛。");
        if (expectFailure)
        {
            Assert.ThrowsAny<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        }
        else
        {
            task.GetAwaiter().GetResult();
        }
    }

    private static void WaitUntil(Func<bool> condition, GameClient client)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            client.Dispatcher.Pump();
            Thread.Sleep(1);
        }

        Assert.True(condition(), "等待生命周期状态变化超时。");
    }
}
