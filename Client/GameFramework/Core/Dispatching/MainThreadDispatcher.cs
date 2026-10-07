using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Core.Dispatching
{
    /// <summary>
    /// 容量受限的主线程队列。创建线程负责调用 Pump；异步委托捕获专属同步上下文，
    /// 后续延续也进入同一队列，因此无 Unity 同步上下文的宿主必须显式泵送。
    /// </summary>
    public sealed class MainThreadDispatcher : IMainThreadDispatcher
    {
        private sealed class WorkItem
        {
            public WorkItem(Action action, TaskCompletionSource<bool>? completion, CancellationToken cancellationToken, bool completesAsynchronously = false)
            {
                Action = action;
                Completion = completion;
                CancellationToken = cancellationToken;
                CompletesAsynchronously = completesAsynchronously;
            }

            public Action Action { get; }

            public TaskCompletionSource<bool>? Completion { get; }

            public CancellationToken CancellationToken { get; }

            public bool CompletesAsynchronously { get; }
        }

        private sealed class DispatcherSynchronizationContext : SynchronizationContext
        {
            private readonly MainThreadDispatcher _dispatcher;

            public DispatcherSynchronizationContext(MainThreadDispatcher dispatcher)
            {
                _dispatcher = dispatcher;
            }

            public override void Post(SendOrPostCallback callback, object? state)
            {
                _dispatcher.PostContinuation(() => callback(state));
            }

            public override void Send(SendOrPostCallback callback, object? state)
            {
                if (!_dispatcher.IsMainThread)
                {
                    throw new InvalidOperationException("不能从后台线程同步派发到主线程；请使用异步派发。");
                }

                callback(state);
            }

            public override SynchronizationContext CreateCopy() => this;
        }

        private readonly object _gate = new object();
        private readonly Queue<WorkItem> _queue = new Queue<WorkItem>();
        private readonly HashSet<TaskCompletionSource<bool>> _inFlightCompletions = new HashSet<TaskCompletionSource<bool>>();
        private readonly int _mainThreadId;
        private readonly int _capacity;
        private readonly DispatcherSynchronizationContext _synchronizationContext;
        private bool _accepting = true;
        private bool _draining;
        private int _inFlightAsync;

        public MainThreadDispatcher(int capacity = 1024)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "派发队列容量必须大于零。");
            }

            _capacity = capacity;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _synchronizationContext = new DispatcherSynchronizationContext(this);
        }

        public event Action<Exception>? UnhandledPostException;

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public bool IsAccepting
        {
            get
            {
                lock (_gate)
                {
                    return _accepting;
                }
            }
        }

        public int PendingCount
        {
            get
            {
                lock (_gate)
                {
                    return _queue.Count;
                }
            }
        }

        public void Post(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Enqueue(new WorkItem(action, null, CancellationToken.None));
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (IsMainThread)
            {
                if (!IsAccepting)
                {
                    return Failed(MainThreadDispatchFailure.Closed, "主线程派发器已关闭。");
                }

                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            var completion = NewCompletion();
            try
            {
                Enqueue(new WorkItem(action, completion, cancellationToken));
                return completion.Task;
            }
            catch (MainThreadDispatchException ex)
            {
                return Task.FromException(ex);
            }
        }

        public Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (IsMainThread)
            {
                if (!IsAccepting)
                {
                    return Failed(MainThreadDispatchFailure.Closed, "主线程派发器已关闭。");
                }

                return StartAsyncAction(action, cancellationToken);
            }

            var completion = NewCompletion();
            try
            {
                Enqueue(new WorkItem(
                    () => CompleteAsyncAction(action, completion, cancellationToken, hasReservation: true),
                    completion,
                    cancellationToken,
                    completesAsynchronously: true));
                return completion.Task;
            }
            catch (MainThreadDispatchException ex)
            {
                return Task.FromException(ex);
            }
        }

        public int Pump(int maxItems = 256)
        {
            RequireMainThread();
            if (maxItems <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItems), "单次泵送上限必须大于零。");
            }

            int processed = 0;
            while (processed < maxItems)
            {
                WorkItem? item;
                lock (_gate)
                {
                    item = _queue.Count == 0 ? null : _queue.Dequeue();
                    if (item?.CompletesAsynchronously == true)
                    {
                        Interlocked.Increment(ref _inFlightAsync);
                    }
                }

                if (item is null)
                {
                    break;
                }

                processed++;
                Execute(item);
            }

            return processed;
        }

        /// <summary>同步限时关闭入口，要求由创建线程调用。</summary>
        public void Shutdown(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            RequireMainThread();
            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            lock (_gate)
            {
                _accepting = false;
                _draining = true;
            }

            DateTime deadline = DateTime.UtcNow + timeout;
            while ((PendingCount > 0 || Volatile.Read(ref _inFlightAsync) > 0) && DateTime.UtcNow < deadline)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    FailOutstanding(
                        MainThreadDispatchFailure.ShutdownCancelled,
                        "主线程派发器关闭被取消。");
                    return;
                }

                Pump(_capacity);
                if (PendingCount == 0 && Volatile.Read(ref _inFlightAsync) > 0)
                {
                    Thread.Sleep(1);
                }
            }

            if (PendingCount > 0 || Volatile.Read(ref _inFlightAsync) > 0)
            {
                FailOutstanding(MainThreadDispatchFailure.ShutdownTimeout, "主线程派发器关闭排空超时。");
                return;
            }

            lock (_gate)
            {
                _draining = false;
            }
        }

        public Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                Shutdown(timeout, cancellationToken);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task StartAsyncAction(Func<Task> action, CancellationToken cancellationToken)
        {
            var completion = NewCompletion();
            lock (_gate)
            {
                if (!_accepting)
                {
                    completion.TrySetException(new MainThreadDispatchException(MainThreadDispatchFailure.Closed, "主线程派发器已关闭。"));
                    return completion.Task;
                }

                if (_queue.Count + _inFlightAsync >= _capacity)
                {
                    completion.TrySetException(new MainThreadDispatchException(MainThreadDispatchFailure.QueueFull, "主线程派发队列与异步操作容量已满。"));
                    return completion.Task;
                }

                Interlocked.Increment(ref _inFlightAsync);
            }

            CompleteAsyncAction(action, completion, cancellationToken, hasReservation: true);
            return completion.Task;
        }

        private void CompleteAsyncAction(
            Func<Task> action,
            TaskCompletionSource<bool> completion,
            CancellationToken cancellationToken,
            bool hasReservation)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                ReleaseAsyncReservation();
                return;
            }

            SynchronizationContext? previous = SynchronizationContext.Current;
            Task operation;
            try
            {
                SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
                operation = action() ?? throw new InvalidOperationException("异步派发委托返回了 null。");
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
                ReleaseAsyncReservation();
                return;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            if (operation.IsCompleted)
            {
                CompleteFrom(operation, completion);
                ReleaseAsyncReservation();
            }
            else
            {
                lock (_gate)
                {
                    _inFlightCompletions.Add(completion);
                }

                _ = ObserveAsync(operation, completion);
            }
        }

        private async Task ObserveAsync(Task operation, TaskCompletionSource<bool> completion)
        {
            try
            {
                await operation.ConfigureAwait(false);
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException ex)
            {
                completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                lock (_gate)
                {
                    if (_inFlightCompletions.Remove(completion))
                    {
                        Interlocked.Decrement(ref _inFlightAsync);
                    }
                }
            }
        }

        private static void CompleteFrom(Task operation, TaskCompletionSource<bool> completion)
        {
            if (operation.IsCanceled)
            {
                completion.TrySetCanceled();
            }
            else if (operation.IsFaulted)
            {
                completion.TrySetException(operation.Exception!.InnerExceptions);
            }
            else
            {
                completion.TrySetResult(true);
            }
        }

        private void Enqueue(WorkItem item)
        {
            lock (_gate)
            {
                if (!_accepting)
                {
                    throw new MainThreadDispatchException(MainThreadDispatchFailure.Closed, "主线程派发器已关闭。");
                }

                if (_queue.Count + _inFlightAsync >= _capacity)
                {
                    throw new MainThreadDispatchException(MainThreadDispatchFailure.QueueFull, "主线程派发队列与异步操作容量已满。");
                }

                _queue.Enqueue(item);
            }
        }

        private void PostContinuation(Action action)
        {
            lock (_gate)
            {
                if (!_accepting && (!_draining || _inFlightAsync == 0))
                {
                    throw new MainThreadDispatchException(MainThreadDispatchFailure.Closed, "主线程派发器已关闭。");
                }

                if (_queue.Count >= _capacity)
                {
                    throw new MainThreadDispatchException(MainThreadDispatchFailure.QueueFull, "主线程派发队列已满。");
                }

                _queue.Enqueue(new WorkItem(action, null, CancellationToken.None));
            }
        }

        private void Execute(WorkItem item)
        {
            if (item.CancellationToken.IsCancellationRequested)
            {
                item.Completion?.TrySetCanceled(item.CancellationToken);
                if (item.CompletesAsynchronously)
                {
                    ReleaseAsyncReservation();
                }

                return;
            }

            try
            {
                SynchronizationContext? previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
                try
                {
                    item.Action();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }

                if (item.Completion != null && !item.CompletesAsynchronously)
                {
                    item.Completion.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                if (item.Completion != null)
                {
                    item.Completion.TrySetException(ex);
                }
                else
                {
                    try
                    {
                        UnhandledPostException?.Invoke(ex);
                    }
                    catch
                    {
                        // 观察者异常不得中断同批派发队列。
                    }
                }
            }
        }

        private void FailOutstanding(MainThreadDispatchFailure failure, string message)
        {
            List<WorkItem> rejected = new List<WorkItem>();
            List<TaskCompletionSource<bool>> inFlight;
            lock (_gate)
            {
                _draining = false;
                while (_queue.Count > 0)
                {
                    rejected.Add(_queue.Dequeue());
                }

                inFlight = new List<TaskCompletionSource<bool>>(_inFlightCompletions);
                _inFlightCompletions.Clear();
                Interlocked.Exchange(ref _inFlightAsync, 0);
            }

            var dispatchException = new MainThreadDispatchException(failure, message);
            foreach (WorkItem item in rejected)
            {
                if (item.Completion != null)
                {
                    item.Completion.TrySetException(dispatchException);
                }
                else
                {
                    try
                    {
                        UnhandledPostException?.Invoke(dispatchException);
                    }
                    catch
                    {
                        // 观察者异常不得中断关闭清理。
                    }
                }
            }

            foreach (TaskCompletionSource<bool> completion in inFlight)
            {
                completion.TrySetException(dispatchException);
            }
        }

        private void ReleaseAsyncReservation()
        {
            lock (_gate)
            {
                Interlocked.Decrement(ref _inFlightAsync);
            }
        }

        private void RequireMainThread()
        {
            if (!IsMainThread)
            {
                throw new InvalidOperationException("此操作必须由创建主线程调用。");
            }
        }

        private static TaskCompletionSource<bool> NewCompletion() =>
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static Task Failed(MainThreadDispatchFailure failure, string message) =>
            Task.FromException(new MainThreadDispatchException(failure, message));
    }
}
