using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.Core.Modules
{
    /// <summary>模块排序失败：未知依赖或循环依赖。</summary>
    public sealed class ModuleOrderException : Exception
    {
        public ModuleOrderException(string message) : base(message)
        {
        }
    }

    /// <summary>模块启动失败：携带失败模块名；启动前的模块已被逆序回滚。</summary>
    public sealed class ModuleStartupException : Exception
    {
        public ModuleStartupException(string moduleName, Exception inner)
            : base($"模块 {moduleName} 启动失败，已初始化模块已逆序回滚。", inner)
        {
            ModuleName = moduleName;
        }

        public string ModuleName { get; }
    }

    /// <summary>模块关闭阶段失败聚合：每个失败模块一条记录；所有模块都已尝试关闭后才抛出。</summary>
    public sealed class ModuleShutdownException : Exception
    {
        public ModuleShutdownException(IReadOnlyList<(string ModuleName, Exception Exception)> failures)
            : base($"模块关闭阶段出现 {failures.Count} 个失败。", failures.Count == 1 ? failures[0].Exception : null)
        {
            Failures = failures;
        }

        public IReadOnlyList<(string ModuleName, Exception Exception)> Failures { get; }
    }

    /// <summary>模块运行阶段。</summary>
    public enum ModulePhase
    {
        Registered = 0,
        Initializing = 1,
        Running = 2,
        Shutdown = 3,
    }

    /// <summary>单模块帧耗时画像（ProfilingEnabled 开启后统计）。</summary>
    public sealed class ModuleProfile
    {
        /// <summary>最近一帧耗时（毫秒）。</summary>
        public double LastMs { get; internal set; }

        /// <summary>累计耗时（毫秒）。</summary>
        public double TotalMs { get; internal set; }

        /// <summary>单帧最大耗时（毫秒）。</summary>
        public double MaxMs { get; internal set; }

        /// <summary>累计统计帧数。</summary>
        public long FrameCount { get; internal set; }

        /// <summary>平均帧耗时（毫秒）。</summary>
        public double AverageMs => FrameCount == 0 ? 0 : TotalMs / FrameCount;
    }

    /// <summary>
    /// 模块宿主：负责注册、拓扑排序、启动序列、帧泵与关闭序列。
    /// 失败语义：
    /// - 初始化失败：逆序回滚已完成初始化的模块，抛 ModuleStartupException；
    /// - 帧回调失败：隔离到单个模块（记录日志并触发 FrameFaulted），不中断其他模块；
    /// - 关闭失败：先尝试关闭全部模块，再聚合抛 ModuleShutdownException。
    /// 开启 ProfilingEnabled 后按模块统计帧耗时（Last/Average/Max）。
    /// </summary>
    public sealed class ModuleHost
    {
        private readonly ILogger _logger;
        private readonly IMainThreadDispatcher? _dispatcher;
        private readonly TimeSpan _rollbackTimeout;
        private readonly List<IClientModule> _registered = new List<IClientModule>();
        private readonly Dictionary<string, IClientModule> _byName = new Dictionary<string, IClientModule>(StringComparer.Ordinal);
        private readonly Dictionary<IClientModule, ModulePhase> _phases = new Dictionary<IClientModule, ModulePhase>();
        private List<IClientModule>? _ordered;
        private readonly Dictionary<string, ModuleProfile> _profiles = new Dictionary<string, ModuleProfile>();

        public ModuleHost(ILogger logger, IMainThreadDispatcher? dispatcher = null, TimeSpan? rollbackTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dispatcher = dispatcher;
            _rollbackTimeout = rollbackTimeout ?? TimeSpan.FromSeconds(2);
            if (_rollbackTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(rollbackTimeout), "启动回滚时限必须大于零。");
            }
        }

        /// <summary>单模块帧回调异常：记录后上报，帧循环继续。</summary>
        public event Action<IClientModule, Exception>? FrameFaulted;

        /// <summary>是否开启模块帧耗时统计。</summary>
        public bool ProfilingEnabled { get; set; }

        /// <summary>模块帧耗时画像快照（ProfilingEnabled 开启后有效）。</summary>
        public IReadOnlyDictionary<string, ModuleProfile> Profiles => _profiles;

        /// <summary>清空耗时统计。</summary>
        public void ResetProfiles() => _profiles.Clear();

        /// <summary>拓扑排序后的模块序列；初始化完成前为 null。</summary>
        public IReadOnlyList<IClientModule> OrderedModules =>
            _ordered ?? throw new InvalidOperationException("模块尚未排序：请先调用 InitializeAllAsync。");

        public IReadOnlyList<IClientModule> RegisteredModules => _registered;

        internal bool HasOrderedModules => _ordered != null;

        /// <summary>启动前注册模块；重名抛 ArgumentException。</summary>
        public void Add(IClientModule module)
        {
            if (module is null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (_byName.ContainsKey(module.Name))
            {
                throw new ArgumentException($"模块名 {module.Name} 重复。", nameof(module));
            }

            _registered.Add(module);
            _byName[module.Name] = module;
            _phases[module] = ModulePhase.Registered;
        }

        /// <summary>按依赖拓扑序初始化全部模块；任一失败即回滚并抛出。</summary>
        public Task InitializeAllAsync(ModuleEnvironment environment, CancellationToken cancellationToken = default)
        {
            if (_dispatcher != null)
            {
                return _dispatcher.InvokeAsync(
                    () => InitializeAllCoreAsync(environment, cancellationToken),
                    cancellationToken);
            }

            return InitializeAllCoreAsync(environment, cancellationToken);
        }

        private async Task InitializeAllCoreAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            List<IClientModule> order = BuildOrder();
            _ordered = order;

            List<IClientModule> initialized = new List<IClientModule>();
            try
            {
                foreach (IClientModule module in order)
                {
                    _phases[module] = ModulePhase.Initializing;
                    cancellationToken.ThrowIfCancellationRequested();
                    await InvokeModuleAsync(async () =>
                    {
                        // 环境在用户回调之前注入，模块回调内可直接使用日志与服务。
                        IClientModule concrete = module is ModuleMetadataAdapter adapter ? adapter.Inner : module;
                        if (concrete is ClientModule clientModule)
                        {
                            clientModule.AttachEnvironment(environment);
                        }

                        await module.InitializeAsync(environment, cancellationToken);
                    }, cancellationToken);
                    initialized.Add(module);
                    _logger.Info($"模块 {module.Name} 初始化完成。");
                }
            }
            catch (OperationCanceledException)
            {
                await RollbackAsync(initialized);
                throw;
            }
            catch (Exception ex) when (ex is not ModuleStartupException)
            {
                IClientModule? failed = order.FirstOrDefault(m =>
                    _phases[m] == ModulePhase.Initializing && !initialized.Contains(m));
                await RollbackAsync(initialized);
                throw new ModuleStartupException(failed?.Name ?? "<unknown>", ex);
            }

            foreach (IClientModule module in order)
            {
                _phases[module] = ModulePhase.Running;
            }
        }

        /// <summary>按拓扑序启动全部模块；失败包装为 ModuleStartupException。</summary>
        public void StartAll()
        {
            foreach (IClientModule module in OrderedModules)
            {
                try
                {
                    if (_dispatcher == null)
                    {
                        module.Start();
                    }
                    else
                    {
                        _dispatcher.InvokeAsync(module.Start).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    throw new ModuleStartupException(module.Name, ex);
                }
            }
        }

        public void Update(double deltaTime) => Pump(static (m, d) => m.Update(d), deltaTime);

        public void FixedUpdate(double deltaTime) => Pump(static (m, d) => m.FixedUpdate(d), deltaTime);

        public void LateUpdate(double deltaTime) => Pump(static (m, d) => m.LateUpdate(d), deltaTime);

        /// <summary>逆序关闭全部模块；收集失败后聚合抛出，保证每个模块都被尝试关闭。</summary>
        public Task ShutdownAllAsync(CancellationToken cancellationToken = default)
        {
            if (_dispatcher != null)
            {
                return _dispatcher.InvokeAsync(
                    () => ShutdownAllCoreAsync(cancellationToken),
                    CancellationToken.None);
            }

            return ShutdownAllCoreAsync(cancellationToken);
        }

        private async Task ShutdownAllCoreAsync(CancellationToken cancellationToken)
        {
            List<(string, Exception)> failures = new List<(string, Exception)>();
            for (int i = OrderedModules.Count - 1; i >= 0; i--)
            {
                IClientModule module = OrderedModules[i];
                if (_phases[module] == ModulePhase.Shutdown)
                {
                    continue;
                }

                _phases[module] = ModulePhase.Shutdown;
                try
                {
                    await InvokeModuleAsync(async () => await module.ShutdownAsync(cancellationToken), CancellationToken.None);
                    _logger.Info($"模块 {module.Name} 已关闭。");
                }
                catch (Exception ex)
                {
                    failures.Add((module.Name, ex));
                    _logger.Error($"模块 {module.Name} 关闭失败。", ex);
                }
            }

            if (failures.Count > 0)
            {
                throw new ModuleShutdownException(failures);
            }
        }

        private void Pump(Action<IClientModule, double> callback, double deltaTime)
        {
            foreach (IClientModule module in OrderedModules)
            {
                long startTimestamp = ProfilingEnabled ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    callback(module, deltaTime);
                }
                catch (Exception ex)
                {
                    _logger.Error($"模块 {module.Name} 帧回调异常，已隔离。", ex);
                    FrameFaulted?.Invoke(module, ex);
                }
                finally
                {
                    if (ProfilingEnabled)
                    {
                        RecordProfile(module.Name, startTimestamp);
                    }
                }
            }
        }

        private void RecordProfile(string moduleName, long startTimestamp)
        {
            double ms = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (!_profiles.TryGetValue(moduleName, out ModuleProfile? profile))
            {
                profile = new ModuleProfile();
                _profiles[moduleName] = profile;
            }

            profile.LastMs = ms;
            profile.TotalMs += ms;
            if (ms > profile.MaxMs)
            {
                profile.MaxMs = ms;
            }

            profile.FrameCount++;
        }

        private async Task RollbackAsync(List<IClientModule> initialized)
        {
            DateTime deadline = DateTime.UtcNow + _rollbackTimeout;
            using var cleanup = new CancellationTokenSource(_rollbackTimeout);
            for (int i = initialized.Count - 1; i >= 0; i--)
            {
                IClientModule module = initialized[i];
                _phases[module] = ModulePhase.Shutdown;
                try
                {
                    Task shutdown = InvokeModuleAsync(async () => await module.ShutdownAsync(cleanup.Token), CancellationToken.None);
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        CancelCleanupToken(cleanup, module.Name);
                        ObserveDetachedFailure(shutdown);
                        _logger.Error($"模块 {module.Name} 回滚关闭超过总时限，已继续清理后续模块。");
                        continue;
                    }

                    Task completed = await Task.WhenAny(shutdown, Task.Delay(remaining));
                    if (completed != shutdown)
                    {
                        CancelCleanupToken(cleanup, module.Name);
                        ObserveDetachedFailure(shutdown);
                        _logger.Error($"模块 {module.Name} 回滚关闭超过总时限，已继续清理后续模块。");
                        continue;
                    }

                    await shutdown;
                }
                catch (Exception ex)
                {
                    _logger.Error($"模块 {module.Name} 回滚关闭失败。", ex);
                }
            }
        }

        private void CancelCleanupToken(CancellationTokenSource cleanup, string moduleName)
        {
            try
            {
                cleanup.Cancel();
            }
            catch (Exception ex)
            {
                _logger.Error($"模块 {moduleName} 回滚取消回调失败。", ex);
            }
        }

        private static void ObserveDetachedFailure(Task operation)
        {
            _ = operation.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private Task InvokeModuleAsync(Func<Task> action, CancellationToken cancellationToken)
        {
            if (_dispatcher != null)
            {
                return _dispatcher.InvokeAsync(action, cancellationToken);
            }

            return action();
        }

        /// <summary>
        /// Kahn 拓扑排序：依赖先于被依赖者；就绪集合按（Priority 升序，注册序）稳定选取。
        /// 未知依赖与循环依赖抛 ModuleOrderException。
        /// </summary>
        private List<IClientModule> BuildOrder()
        {
            foreach (IClientModule module in _registered)
            {
                foreach (string dependency in module.Dependencies)
                {
                    if (!_byName.ContainsKey(dependency))
                    {
                        throw new ModuleOrderException($"模块 {module.Name} 依赖的 {dependency} 未注册。");
                    }
                }
            }

            var indegree = new Dictionary<IClientModule, int>();
            var dependents = new Dictionary<IClientModule, List<IClientModule>>();
            foreach (IClientModule module in _registered)
            {
                indegree[module] = 0;
                dependents[module] = new List<IClientModule>();
            }

            foreach (IClientModule module in _registered)
            {
                foreach (string dependency in module.Dependencies)
                {
                    IClientModule upstream = _byName[dependency];
                    if (upstream.Equals(module))
                    {
                        throw new ModuleOrderException($"模块 {module.Name} 依赖自身。");
                    }

                    indegree[module]++;
                    dependents[upstream].Add(module);
                }
            }

            // 就绪集合按（Priority, 注册索引）排序，保证输出顺序确定。
            List<IClientModule> ready = _registered
                .Where(m => indegree[m] == 0)
                .OrderBy(m => m.Priority)
                .ThenBy(m => _registered.IndexOf(m))
                .ToList();

            List<IClientModule> order = new List<IClientModule>(_registered.Count);
            while (ready.Count > 0)
            {
                IClientModule current = ready[0];
                ready.RemoveAt(0);
                order.Add(current);

                foreach (IClientModule dependent in dependents[current])
                {
                    indegree[dependent]--;
                    if (indegree[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }

                ready.Sort((a, b) =>
                {
                    int byPriority = a.Priority.CompareTo(b.Priority);
                    return byPriority != 0 ? byPriority : _registered.IndexOf(a).CompareTo(_registered.IndexOf(b));
                });
            }

            if (order.Count != _registered.Count)
            {
                IEnumerable<string> stuck = _registered
                    .Where(m => !order.Contains(m))
                    .Select(m => m.Name);
                throw new ModuleOrderException(
                    $"存在循环依赖，无法排序的模块：{string.Join(" → ", stuck)}");
            }

            return order;
        }
    }
}
