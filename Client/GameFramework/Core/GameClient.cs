using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Events;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.Scheduling;
using Lyking.GameFramework.Client.Core.ServiceLocation;

namespace Lyking.GameFramework.Client
{
    /// <summary>客户端整体运行状态。</summary>
    public enum ClientState
    {
        Created = 0,
        Starting = 1,
        Running = 2,
        ShuttingDown = 3,
        Shutdown = 4,
        Failed = 5,
    }

    /// <summary>
    /// 客户端装配门面：持有事件总线、服务注册表、计时服务与模块宿主，
    /// 提供启动 → 帧循环 → 关闭的完整生命周期。Unity 入口（GameEntry）或测试环境都从这里进入。
    /// 启动失败（任一模块初始化/启动异常）时状态置为 Failed 并上抛；关闭仍按逆序完成。
    /// </summary>
    public sealed class GameClient
    {
        private readonly List<IClientModule> _externalModules = new List<IClientModule>();

        public GameClient(
            ILogger? logger = null,
            IEnumerable<IClientModule>? modules = null,
            IMainThreadDispatcher? dispatcher = null)
        {
            Logger = logger ?? new Logger(
                "GameClient",
                new DelegateLogSink((level, message) => Console.WriteLine(message)),
                LogLevel.Info);
            Events = new EventBus(Logger);
            Services = new ServiceRegistry();
            Timers = new TimerService();
            Dispatcher = dispatcher ?? new MainThreadDispatcher();
            Services.Register(Dispatcher);
            Modules = new ModuleHost(Logger, Dispatcher);

            if (modules != null)
            {
                foreach (IClientModule module in modules)
                {
                    _externalModules.Add(module);
                }
            }
        }

        public ClientState State { get; private set; } = ClientState.Created;

        public ILogger Logger { get; }

        public IEventBus Events { get; }

        public ServiceRegistry Services { get; }

        public TimerService Timers { get; }

        /// <summary>客户端主线程派发器；无 Unity 同步上下文的宿主必须显式调用 Pump。</summary>
        public IMainThreadDispatcher Dispatcher { get; }

        public ModuleHost Modules { get; }

        /// <summary>启动前追加模块；启动后调用抛 InvalidOperationException。</summary>
        public void AddModule(IClientModule module)
        {
            if (State != ClientState.Created)
            {
                throw new InvalidOperationException("只能在启动前注册模块。");
            }

            if (module is null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            _externalModules.Add(module);
        }

        /// <summary>注册内置服务与模块，按拓扑序初始化并启动全部模块。</summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            RequireMainThread();
            if (State != ClientState.Created)
            {
                throw new InvalidOperationException($"客户端已启动过，当前状态 {State}。");
            }

            State = ClientState.Starting;
            Logger.Info("客户端启动开始。");

            try
            {
                // Builder 显式登记了同名计时器模块时由外部实例接管，内置实例不再加入。
                IClientModule? externalTimer = null;
                foreach (IClientModule external in _externalModules)
                {
                    if (external.Name == Timers.Name)
                    {
                        externalTimer = external;
                        break;
                    }
                }

                TimerService activeTimer = externalTimer as TimerService ?? Timers;
                Services.Register<IEventBus>(Events);
                Services.Register<IServiceRegistry>(Services);
                Services.Register(activeTimer);

                if (externalTimer == null)
                {
                    Modules.Add(Timers);
                }

                foreach (IClientModule module in _externalModules)
                {
                    Modules.Add(module);
                }

                await Modules.InitializeAllAsync(new ModuleEnvironment(Services, Logger), cancellationToken);
                Modules.StartAll();
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    State = ClientState.Failed;
                    Logger.Error("客户端启动失败。", ex);
                });
                throw;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                State = ClientState.Running;
                Logger.Info($"客户端启动完成，共 {Modules.OrderedModules.Count} 个模块。");
            });
        }

        /// <summary>帧驱动入口，由平台每帧调用；未运行时抛异常。</summary>
        public void Update(double deltaTime)
        {
            RequireMainThread();
            Dispatcher.Pump();
            RequireRunning();
            Modules.Update(deltaTime);
        }

        public void FixedUpdate(double fixedDeltaTime)
        {
            RequireMainThread();
            Dispatcher.Pump();
            RequireRunning();
            Modules.FixedUpdate(fixedDeltaTime);
        }

        public void LateUpdate(double deltaTime)
        {
            RequireMainThread();
            Dispatcher.Pump();
            RequireRunning();
            Modules.LateUpdate(deltaTime);
        }

        /// <summary>逆序关闭全部模块并清理状态；从 Running 或 Failed 进入。</summary>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            RequireMainThread();
            if (State != ClientState.Running && State != ClientState.Failed)
            {
                throw new InvalidOperationException($"当前状态 {State} 不允许关闭。");
            }

            State = ClientState.ShuttingDown;
            Logger.Info("客户端开始关闭。");
            try
            {
                if (Modules.HasOrderedModules)
                {
                    await Modules.ShutdownAllAsync(cancellationToken);
                }
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    State = ClientState.Shutdown;
                    Logger.Info("客户端已关闭。");
                    Dispatcher.Shutdown(TimeSpan.FromSeconds(2), cancellationToken);
                });
            }
        }

        /// <summary>宿主生命周期等待超时时关闭派发器，并显式失败全部未完成的派发等待者。</summary>
        public void AbortPendingOperations()
        {
            RequireMainThread();
            Dispatcher.Shutdown(TimeSpan.Zero);
        }

        private void RequireMainThread()
        {
            if (!Dispatcher.IsMainThread)
            {
                throw new InvalidOperationException("客户端生命周期与帧入口必须由创建主线程调用。");
            }
        }

        private void RequireRunning()
        {
            if (State != ClientState.Running)
            {
                throw new InvalidOperationException($"客户端未在运行，当前状态 {State}。");
            }
        }
    }
}
