using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Core.Dispatching
{
    /// <summary>主线程派发失败原因。</summary>
    public enum MainThreadDispatchFailure
    {
        QueueFull = 0,
        Closed = 1,
        ShutdownTimeout = 2,
        ShutdownCancelled = 3,
    }

    /// <summary>主线程派发未能保证执行时抛出。</summary>
    public sealed class MainThreadDispatchException : InvalidOperationException
    {
        public MainThreadDispatchException(MainThreadDispatchFailure failure, string message)
            : base(message)
        {
            Failure = failure;
        }

        public MainThreadDispatchFailure Failure { get; }
    }

    /// <summary>由创建线程显式泵送的有界主线程工作队列。</summary>
    public interface IMainThreadDispatcher
    {
        bool IsMainThread { get; }

        bool IsAccepting { get; }

        int PendingCount { get; }

        /// <summary>将操作排入队列；队列满或已关闭时同步抛出明确错误。</summary>
        void Post(Action action);

        /// <summary>执行必达操作；队列满或已关闭时返回失败任务。</summary>
        Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

        /// <summary>在主线程启动异步操作，并由显式泵送其捕获的异步延续。</summary>
        Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default);

        /// <summary>在创建线程执行最多 maxItems 个排队操作。</summary>
        int Pump(int maxItems = 256);

        /// <summary>同步限时关闭，要求由创建线程调用。</summary>
        void Shutdown(TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>停止接收新操作，并在限时内排空已经接受的操作。</summary>
        Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
