using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class MainThreadDispatcherTests
{
    [Fact]
    public async Task 队列满时必达派发明确失败且已接受任务仍可泵送()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 1);
        bool executed = false;
        Task accepted = Task.Run(async () => await dispatcher.InvokeAsync(() => executed = true));

        WaitUntil(() => dispatcher.PendingCount == 1);
        Task rejected = Task.Run(async () => await dispatcher.InvokeAsync(() => { }));
        WaitUntil(() => rejected.IsCompleted);

        Assert.False(executed);
        Assert.Equal(1, dispatcher.Pump());
        await accepted;
        var rejection = await Assert.ThrowsAsync<MainThreadDispatchException>(() => rejected);
        Assert.Equal(MainThreadDispatchFailure.QueueFull, rejection.Failure);
        Assert.True(executed);
    }

    [Fact]
    public async Task 关闭会排空已接受任务并拒绝新任务()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 2);
        int executed = 0;
        Task accepted = Task.Run(async () => await dispatcher.InvokeAsync(() => executed++));
        WaitUntil(() => dispatcher.PendingCount == 1);

        dispatcher.Shutdown(TimeSpan.FromSeconds(1));

        await accepted;
        Assert.Equal(1, executed);
        var rejected = Assert.Throws<MainThreadDispatchException>(() => dispatcher.Post(() => { }));
        Assert.Equal(MainThreadDispatchFailure.Closed, rejected.Failure);
    }

    [Fact]
    public async Task 关闭期间允许已接收异步操作的内部延续完成()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 2);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool continued = false;
        Task accepted = dispatcher.InvokeAsync(async () =>
        {
            await gate.Task;
            continued = true;
        });
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            gate.TrySetResult(true);
        });

        dispatcher.Shutdown(TimeSpan.FromSeconds(1));

        await accepted;
        Assert.True(continued);
        Assert.False(dispatcher.IsAccepting);
    }

    [Fact]
    public async Task 关闭取消会完成所有在途必达等待者()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 2);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task accepted = dispatcher.InvokeAsync(async () => await gate.Task);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        dispatcher.Shutdown(TimeSpan.FromSeconds(1), cancellation.Token);

        var ex = await Assert.ThrowsAsync<MainThreadDispatchException>(() => accepted);
        Assert.Equal(MainThreadDispatchFailure.ShutdownCancelled, ex.Failure);
    }

    [Fact]
    public async Task 异步调用在泵送前取消后仍可复用队列容量()
    {
        var dispatcher = new MainThreadDispatcher(capacity: 1);
        using var cancellation = new CancellationTokenSource();
        Task canceled = Task.Run(async () => await dispatcher.InvokeAsync(
            async () => await Task.Yield(), cancellation.Token));

        WaitUntil(() => dispatcher.PendingCount == 1);
        cancellation.Cancel();
        dispatcher.Pump();

        Task canceledAssertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Task accepted = Task.Run(async () => await dispatcher.InvokeAsync(async () => await Task.Yield()));
        WaitUntil(() => dispatcher.PendingCount == 1);
        dispatcher.Pump();

        await canceledAssertion;
        await accepted;
    }

    [Fact]
    public async Task 连续异步阶段每次泵送后都保留主线程上下文()
    {
        int mainThread = Environment.CurrentManagedThreadId;
        var dispatcher = new MainThreadDispatcher(capacity: 8);
        var threads = new List<int>();
        Task operation = dispatcher.InvokeAsync(async () =>
        {
            await Task.Run(() => Thread.SpinWait(10000));
            threads.Add(Environment.CurrentManagedThreadId);
            await Task.Run(() => Thread.SpinWait(10000));
            threads.Add(Environment.CurrentManagedThreadId);
        });

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!operation.IsCompleted && DateTime.UtcNow < deadline)
        {
            dispatcher.Pump();
            Thread.SpinWait(1000);
        }

        dispatcher.Pump();
        Assert.True(operation.IsCompleted, "多阶段异步操作未在泵送后完成。");
        await operation;
        Assert.Equal(new[] { mainThread, mainThread }, threads);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(2)), "等待后台操作到达派发器超时。");
    }
}
