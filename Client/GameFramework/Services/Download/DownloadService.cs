using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Services.Download
{
    /// <summary>下载任务状态。</summary>
    public enum DownloadState
    {
        Pending = 0,
        Downloading = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
    }

    /// <summary>下载任务。</summary>
    public sealed class DownloadTask
    {
        private int _state;
        private long _progressBits;
        private long _totalLength;
        private long _downloadedLength;

        internal DownloadTask(int id, string url, string savePath, int priority, object? userData)
        {
            Id = id;
            Url = url;
            SavePath = savePath;
            Priority = priority;
            UserData = userData;
        }

        public int Id { get; }

        public string Url { get; }

        public string SavePath { get; }

        /// <summary>调度优先级，数值大者先出队。</summary>
        public int Priority { get; }

        public object? UserData { get; }

        public DownloadState State
        {
            get => (DownloadState)Volatile.Read(ref _state);
            internal set => Volatile.Write(ref _state, (int)value);
        }

        /// <summary>已尝试的重试次数。</summary>
        public int RetryCount { get; internal set; }

        /// <summary>进度 0..1（总长未知时为 0）。</summary>
        public double Progress
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _progressBits));
            internal set => Interlocked.Exchange(ref _progressBits, BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>总字节数；未知为 0。</summary>
        public long TotalLength
        {
            get => Interlocked.Read(ref _totalLength);
            internal set => Interlocked.Exchange(ref _totalLength, value);
        }

        public long DownloadedLength
        {
            get => Interlocked.Read(ref _downloadedLength);
            internal set => Interlocked.Exchange(ref _downloadedLength, value);
        }

        public string? ErrorMessage { get; internal set; }
    }

    /// <summary>
    /// 下载代理：单个下载的执行者。真实实现（UnityWebRequest / HttpClient / 断点续传）
    /// 由平台层提供；通过 onProgress 上报 0..1 进度，失败抛异常，取消响应 token。
    /// </summary>
    public interface IDownloadAgent
    {
        ValueTask DownloadAsync(DownloadTask task, Action<double> onProgress, CancellationToken cancellationToken);
    }

    /// <summary>下载选项。</summary>
    public sealed class DownloadOptions
    {
        /// <summary>并发上限。</summary>
        public int MaxConcurrent { get; set; } = 3;

        /// <summary>失败自动重试次数（不含首次）。</summary>
        public int MaxRetry { get; set; } = 2;

        /// <summary>关闭时等待在途下载响应取消的最长时间。</summary>
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// 下载服务：任务队列 + 并发控制 + 失败重试 + 进度事件，随帧循环调度。
    /// 用于资源热更分包、版本清单下载等管线；真实 IO 在 IDownloadAgent 平台实现内。
    /// 语义：Remove 仅对排队任务生效；Cancel 对在途任务生效并标记 Cancelled（不重试）；
    /// 失败按 RetryCount 重新排队，超过 MaxRetry 触发 DownloadFailed。
    /// </summary>
    public sealed class DownloadService : ClientModule
    {
        private sealed class DispatchFailure
        {
            public DispatchFailure(DownloadTask task, CancellationTokenSource cancellation, Exception exception)
            {
                Task = task;
                Cancellation = cancellation;
                Exception = exception;
            }

            public DownloadTask Task { get; }

            public CancellationTokenSource Cancellation { get; }

            public Exception Exception { get; }
        }

        private readonly IDownloadAgent _agent;
        private readonly DownloadOptions _options;
        private readonly List<DownloadTask> _pending = new List<DownloadTask>();
        private readonly Dictionary<int, DownloadTask> _active = new Dictionary<int, DownloadTask>();
        private readonly Dictionary<int, DownloadTask> _tasks = new Dictionary<int, DownloadTask>();
        private readonly Dictionary<int, CancellationTokenSource> _cancellations = new Dictionary<int, CancellationTokenSource>();
        private readonly ConcurrentQueue<DispatchFailure> _dispatchFailures = new ConcurrentQueue<DispatchFailure>();
        private IMainThreadDispatcher? _dispatcher;
        private bool _shuttingDown;
        private int _nextTaskId;

        public DownloadService(IDownloadAgent agent, DownloadOptions? options = null, IMainThreadDispatcher? dispatcher = null)
            : base("Download", priority: 300)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _options = options ?? new DownloadOptions();
            if (_options.ShutdownTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "关闭等待时限不能为负数。");
            }

            _dispatcher = dispatcher;
        }

        public event Action<DownloadTask>? DownloadStarted;

        public event Action<DownloadTask>? DownloadProgress;

        public event Action<DownloadTask>? DownloadSucceeded;

        /// <summary>最终失败（重试耗尽）事件。</summary>
        public event Action<DownloadTask>? DownloadFailed;

        public event Action<DownloadTask>? DownloadCancelled;

        public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            base.InitializeAsync(environment, cancellationToken);
            if (_dispatcher == null && environment.Services.TryResolve(out IMainThreadDispatcher? dispatcher))
            {
                _dispatcher = dispatcher;
            }

            return default;
        }

        public int PendingCount => _pending.Count;

        public int ActiveCount => _active.Count;

        /// <summary>添加下载任务，返回任务 id；优先级数值大者先调度。</summary>
        public int AddDownload(string url, string savePath, int priority = 0, object? userData = null)
        {
            RequireMainThread();
            ThrowIfShuttingDown();
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("下载地址不能为空。", nameof(url));
            }

            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("保存路径不能为空。", nameof(savePath));
            }

            var task = new DownloadTask(++_nextTaskId, url, savePath, priority, userData);
            _pending.Add(task);
            _tasks[task.Id] = task;
            return task.Id;
        }

        /// <summary>移除排队任务（在途任务用 Cancel）；返回是否存在。</summary>
        public bool Remove(int taskId)
        {
            RequireMainThread();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Id == taskId)
                {
                    DownloadTask task = _pending[i];
                    _pending.RemoveAt(i);
                    task.State = DownloadState.Cancelled;
                    DownloadCancelled?.Invoke(task);
                    return true;
                }
            }

            return false;
        }

        /// <summary>取消在途任务；排队任务请用 Remove。返回任务是否处于在途。</summary>
        public bool Cancel(int taskId)
        {
            RequireMainThread();
            if (_active.ContainsKey(taskId) && _cancellations.TryGetValue(taskId, out CancellationTokenSource? cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>按 id 查任务（任何状态：排队/在途/已完成/失败/取消）。</summary>
        public DownloadTask? Find(int taskId)
        {
            RequireMainThread();
            return _tasks.TryGetValue(taskId, out DownloadTask? task) ? task : null;
        }

        public override void Update(double deltaTime)
        {
            RequireMainThread();
            ProcessDispatchFailures();
            if (_shuttingDown)
            {
                return;
            }

            // 调度：优先级大者先，同优先级按加入顺序；并发受限。
            while (_active.Count < Math.Max(1, _options.MaxConcurrent) && _pending.Count > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < _pending.Count; i++)
                {
                    if (_pending[i].Priority > _pending[bestIndex].Priority)
                    {
                        bestIndex = i;
                    }
                }

                DownloadTask task = _pending[bestIndex];
                _pending.RemoveAt(bestIndex);
                StartTask(task);
            }
        }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            RequireMainThread();
            _shuttingDown = true;
            if (_dispatcher != null)
            {
                return new ValueTask(_dispatcher.InvokeAsync(() => ShutdownWithDispatcherAsync()));
            }

            return new ValueTask(ShutdownWithoutDispatcherAsync());
        }

        private async Task ShutdownWithDispatcherAsync()
        {
            CancelPending();
            foreach (KeyValuePair<int, CancellationTokenSource> pair in _cancellations.ToArray())
            {
                pair.Value.Cancel();
            }

            DateTime deadline = DateTime.UtcNow + _options.ShutdownTimeout;
            while (_active.Count > 0 && DateTime.UtcNow < deadline)
            {
                ProcessDispatchFailures();
                await Task.Delay(1);
            }

            if (_active.Count > 0)
            {
                foreach (DownloadTask task in _active.Values.ToArray())
                {
                    task.State = DownloadState.Cancelled;
                    task.ErrorMessage = "关闭等待超时，已停止跟踪下载任务。";
                    DownloadCancelled?.Invoke(task);
                    _active.Remove(task.Id);
                    _cancellations.Remove(task.Id);
                }

                throw new TimeoutException("下载服务关闭等待在途任务取消超时。");
            }
        }

        private async Task ShutdownWithoutDispatcherAsync()
        {
            CancelPending();
            foreach (CancellationTokenSource cancellation in _cancellations.Values.ToArray())
            {
                cancellation.Cancel();
            }

            DateTime deadline = DateTime.UtcNow + _options.ShutdownTimeout;
            while (_active.Count > 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1).ConfigureAwait(false);
            }

            if (_active.Count > 0)
            {
                foreach (DownloadTask task in _active.Values.ToArray())
                {
                    task.State = DownloadState.Cancelled;
                    task.ErrorMessage = "关闭等待超时，已停止跟踪下载任务。";
                    _active.Remove(task.Id);
                    _cancellations.Remove(task.Id);
                }

                throw new TimeoutException("下载服务关闭等待在途任务取消超时。");
            }
        }

        private void CancelPending()
        {
            foreach (DownloadTask task in _pending)
            {
                task.State = DownloadState.Cancelled;
                task.ErrorMessage = "服务关闭，任务未启动。";
                DownloadCancelled?.Invoke(task);
            }

            _pending.Clear();
        }

        private void StartTask(DownloadTask task)
        {
            if (_shuttingDown)
            {
                return;
            }

            task.State = DownloadState.Downloading;
            _active[task.Id] = task;
            var cts = new CancellationTokenSource();
            _cancellations[task.Id] = cts;
            Log.Info($"开始下载 {task.Url} → {task.SavePath}。");
            DownloadStarted?.Invoke(task);
            _ = RunTaskAsync(task, cts);
        }

        private async Task RunTaskAsync(DownloadTask task, CancellationTokenSource cts)
        {
            Exception? failure = null;
            bool cancelled = false;
            bool succeeded = false;
            try
            {
                await _agent.DownloadAsync(
                    task,
                    progress =>
                    {
                        Action update = () =>
                        {
                            task.Progress = Math.Clamp(progress, 0d, 1d);
                            task.DownloadedLength = (long)(task.TotalLength * task.Progress);
                            DownloadProgress?.Invoke(task);
                        };

                        if (_dispatcher == null)
                        {
                            update();
                        }
                        else
                        {
                            _dispatcher.Post(update);
                        }
                    },
                    cts.Token);
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            Action complete = () => CompleteTask(task, cts, succeeded, cancelled, failure);
            if (_dispatcher == null)
            {
                complete();
                return;
            }

            try
            {
                await _dispatcher.InvokeAsync(complete);
            }
            catch (Exception ex)
            {
                _dispatchFailures.Enqueue(new DispatchFailure(task, cts, ex));
            }
        }

        private void CompleteTask(DownloadTask task, CancellationTokenSource cts, bool succeeded, bool cancelled, Exception? failure)
        {
            if (_active.ContainsKey(task.Id) && _shuttingDown)
            {
                task.State = DownloadState.Cancelled;
                task.ErrorMessage = "服务关闭，下载已取消。";
                DownloadCancelled?.Invoke(task);
            }
            else if (_active.ContainsKey(task.Id))
            {
                if (cancelled)
                {
                    task.State = DownloadState.Cancelled;
                    task.ErrorMessage = "已取消。";
                    Log.Warn($"下载取消 {task.Url}。");
                    DownloadCancelled?.Invoke(task);
                }
                else if (failure == null && succeeded)
                {
                    task.Progress = 1d;
                    task.State = DownloadState.Succeeded;
                    Log.Info($"下载完成 {task.Url}。");
                    DownloadSucceeded?.Invoke(task);
                }
                else
                {
                    task.RetryCount++;
                    if (task.RetryCount <= _options.MaxRetry)
                    {
                        task.State = DownloadState.Pending;
                        task.Progress = 0;
                        task.DownloadedLength = 0;
                        Log.Warn($"下载失败（第 {task.RetryCount} 次重试）{task.Url}：{failure!.Message}");
                        _pending.Add(task);
                    }
                    else
                    {
                        task.State = DownloadState.Failed;
                        task.ErrorMessage = failure?.Message ?? "下载未完成。";
                        Log.Error($"下载最终失败 {task.Url}。", failure);
                        DownloadFailed?.Invoke(task);
                    }
                }
            }

            Cleanup(task.Id, cts);
        }

        private void ProcessDispatchFailures()
        {
            while (_dispatchFailures.TryDequeue(out DispatchFailure? dispatchFailure))
            {
                DownloadTask task = dispatchFailure.Task;
                if (_active.ContainsKey(task.Id))
                {
                    if (_shuttingDown)
                    {
                        task.State = DownloadState.Cancelled;
                        task.ErrorMessage = "服务关闭，后台完成无法派发。";
                        DownloadCancelled?.Invoke(task);
                    }
                    else
                    {
                        task.State = DownloadState.Failed;
                        task.ErrorMessage = $"主线程派发失败：{dispatchFailure.Exception.Message}";
                        Log.Error(task.ErrorMessage, dispatchFailure.Exception);
                        DownloadFailed?.Invoke(task);
                    }

                    Cleanup(task.Id, dispatchFailure.Cancellation);
                }
                else
                {
                    dispatchFailure.Cancellation.Dispose();
                }
            }
        }

        private void Cleanup(int taskId, CancellationTokenSource cts)
        {
            _active.Remove(taskId);
            _cancellations.Remove(taskId);
            cts.Dispose();
        }

        private void RequireMainThread()
        {
            if (_dispatcher != null && !_dispatcher.IsMainThread)
            {
                throw new InvalidOperationException("下载服务状态只能由主线程访问。");
            }
        }

        private void ThrowIfShuttingDown()
        {
            if (_shuttingDown)
            {
                throw new InvalidOperationException("下载服务正在关闭，不能接收新任务。");
            }
        }
    }
}
