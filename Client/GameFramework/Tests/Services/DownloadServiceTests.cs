using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Download;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

/// <summary>可控下载代理：按脚本逐次执行，支持门控（等待手动放行）、注入异常与取消响应。</summary>
internal sealed class StubDownloadAgent : IDownloadAgent
{
    private readonly Queue<Func<DownloadTask, Action<double>, CancellationToken, ValueTask>> _script = new();

    public List<DownloadTask> Started { get; } = new();

    /// <summary>登记一段执行脚本。</summary>
    public StubDownloadAgent Enqueue(Func<DownloadTask, Action<double>, CancellationToken, ValueTask> step)
    {
        _script.Enqueue(step);
        return this;
    }

    public ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken)
    {
        Started.Add(task);
        if (_script.Count == 0)
        {
            // 默认行为：直接成功。
            onProgress(1d);
            return default;
        }

        return _script.Dequeue()(task, onProgress, cancellationToken);
    }

    /// <summary>标准门控步骤：上报一次进度后挂起，直到 gate 放行再完成。</summary>
    public static async ValueTask Gate(TaskCompletionSource gate, DownloadTask task, Action<double> onProgress, CancellationToken token)
    {
        onProgress(0.5);
        await gate.Task.WaitAsync(token);
        onProgress(1d);
    }
}

public sealed class DownloadServiceTests
{
    private static ModuleEnvironment CreateEnvironment() => new(
        new ServiceRegistry(),
        new Logger("t", new DelegateLogSink((_, _) => { })));

    private static DownloadService CreateService(StubDownloadAgent agent, DownloadOptions? options = null)
    {
        var service = new DownloadService(agent, options);
        service.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        return service;
    }

    [Fact]
    public async Task 模拟代理完整下载与进度事件()
    {
        var simAgent = new SimulationDownloadAgent(chunks: 2);
        var service = new DownloadService(simAgent);
        await service.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        var progress = new List<double>();
        int succeeded = 0;
        service.DownloadProgress += t => progress.Add(t.Progress);
        service.DownloadSucceeded += _ => succeeded++;

        int id = service.AddDownload("res://a.bundle", "assets/a.bundle");
        service.Update(0.016);
        await WaitForAsync(() => service.Find(id)!.State == DownloadState.Succeeded);

        var task = service.Find(id)!;
        Assert.Equal(DownloadState.Succeeded, task.State);
        Assert.Equal(1, succeeded);
        Assert.Equal(1d, task.Progress);
        Assert.Equal(1024, task.TotalLength);
        Assert.Equal(new[] { 0.5, 1.0 }, progress);
    }

    [Fact]
    public async Task 并发上限与排队调度()
    {
        var agent = new StubDownloadAgent();
        var service = CreateService(agent, new DownloadOptions { MaxConcurrent = 2 });
        var gates = new List<TaskCompletionSource>
        {
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        int gateIndex = 0;
        agent.Enqueue((task, onProgress, token) => StubDownloadAgent.Gate(gates[gateIndex++], task, onProgress, token));
        agent.Enqueue((task, onProgress, token) => StubDownloadAgent.Gate(gates[gateIndex++], task, onProgress, token));
        agent.Enqueue((task, onProgress, token) => StubDownloadAgent.Gate(gates[gateIndex++], task, onProgress, token));

        service.AddDownload("a", "a");
        service.AddDownload("b", "b");
        service.AddDownload("c", "c");
        service.Update(0.016);

        Assert.Equal(2, service.ActiveCount);
        Assert.Equal(1, service.PendingCount);

        gates[0].SetResult();
        await WaitForAsync(() => service.ActiveCount == 1);
        service.Update(0.016);
        Assert.Equal(2, service.ActiveCount);
        Assert.Equal(0, service.PendingCount);

        gates[1].SetResult();
        gates[2].SetResult();
        await WaitForAsync(() => service.ActiveCount == 0);
        Assert.Equal(0, service.ActiveCount);
    }

    [Fact]
    public async Task 优先级高的任务先调度()
    {
        var agent = new StubDownloadAgent();
        var service = CreateService(agent, new DownloadOptions { MaxConcurrent = 1 });
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Enqueue((task, p, token) => StubDownloadAgent.Gate(gate, task, p, token));
        agent.Enqueue((task, p, token) => StubDownloadAgent.Gate(new TaskCompletionSource(), task, p, token));

        int low = service.AddDownload("low", "low", priority: 1);
        int high = service.AddDownload("high", "high", priority: 9);
        service.Update(0.016);

        Assert.Equal(high, agent.Started[0].Id);
        gate.SetResult();
        await WaitForAsync(() => service.ActiveCount == 0);
        service.Update(0.016);
        Assert.Equal(low, agent.Started[1].Id);
    }

    [Fact]
    public async Task 失败后自动重试直至成功()
    {
        var agent = new StubDownloadAgent();
        agent.Enqueue((task, p, token) => throw new InvalidOperationException("网络抖动"));
        var service = CreateService(agent, new DownloadOptions { MaxRetry = 2 });
        var failures = new List<DownloadTask>();
        service.DownloadFailed += failures.Add;

        int id = service.AddDownload("flaky", "flaky");
        service.Update(0.016);
        service.Update(0.016);
        await WaitForAsync(() => service.Find(id)!.State == DownloadState.Succeeded);

        var task = service.Find(id)!;
        Assert.Equal(DownloadState.Succeeded, task.State);
        Assert.Equal(1, task.RetryCount);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task 重试耗尽后最终失败()
    {
        var agent = new StubDownloadAgent();
        agent.Enqueue((task, p, token) => throw new InvalidOperationException("永远失败"));
        agent.Enqueue((task, p, token) => throw new InvalidOperationException("永远失败"));
        var service = CreateService(agent, new DownloadOptions { MaxRetry = 1 });
        var failures = new List<DownloadTask>();
        service.DownloadFailed += failures.Add;

        int id = service.AddDownload("dead", "dead");
        service.Update(0.016);
        service.Update(0.016);
        service.Update(0.016);
        await WaitForAsync(() => service.Find(id)!.State == DownloadState.Failed);

        var task = service.Find(id)!;
        Assert.Equal(DownloadState.Failed, task.State);
        Assert.Single(failures);
        Assert.Contains("永远失败", task.ErrorMessage);
    }

    [Fact]
    public async Task 排队任务可移除()
    {
        var agent = new StubDownloadAgent();
        var service = CreateService(agent, new DownloadOptions { MaxConcurrent = 1 });
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Enqueue((task, p, token) => StubDownloadAgent.Gate(gate, task, p, token));
        int first = service.AddDownload("a", "a");
        int second = service.AddDownload("b", "b");

        service.Update(0.016);
        Assert.True(service.Remove(second));
        Assert.False(service.Remove(second));

        gate.SetResult();
        service.Update(0.016);
        await WaitForAsync(() => service.ActiveCount == 0);
        Assert.Equal(DownloadState.Cancelled, service.Find(second)!.State);
    }

    [Fact]
    public async Task 在途任务可取消且不重试()
    {
        var agent = new StubDownloadAgent();
        var service = CreateService(agent, new DownloadOptions { MaxRetry = 3 });
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Enqueue((task, p, token) => GateAwait(gate, token));
        var cancelled = new List<DownloadTask>();
        service.DownloadCancelled += t => cancelled.Add(t);

        int id = service.AddDownload("slow", "slow");
        service.Update(0.016);
        Assert.True(service.Cancel(id));

        await WaitForAsync(() => cancelled.Count == 1);
        Assert.Single(cancelled);
        Assert.Equal(DownloadState.Cancelled, service.Find(id)!.State);
        Assert.Equal(0, service.ActiveCount);
    }

    [Fact]
    public async Task 关闭时清空队列并取消在途()
    {
        var agent = new StubDownloadAgent();
        var service = CreateService(agent, new DownloadOptions { MaxConcurrent = 1 });
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Enqueue((task, p, token) => GateAwait(gate, token));
        service.AddDownload("a", "a");
        service.AddDownload("b", "b");
        service.Update(0.016);

        await service.ShutdownAsync(CancellationToken.None);

        Assert.Equal(0, service.PendingCount);
    }

    [Fact]
    public void 空地址抛出()
    {
        var service = CreateService(new StubDownloadAgent());
        Assert.Throws<ArgumentException>(() => service.AddDownload(" ", "a"));
    }

    private static ValueTask GateAwait(TaskCompletionSource gate, CancellationToken token)
        => new(GateAwaitTask(gate, token));

    private static async Task GateAwaitTask(TaskCompletionSource gate, CancellationToken token)
    {
        await gate.Task.WaitAsync(token);
    }

    /// <summary>确定性等待：轮询直到条件满足（xunit 并行下异步续延在到达线程池后执行）。</summary>
    private static async Task WaitForAsync(Func<bool> condition, string? message = null)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(1);
        }

        Assert.True(condition(), message ?? "等待异步收敛超时。");
    }
}
