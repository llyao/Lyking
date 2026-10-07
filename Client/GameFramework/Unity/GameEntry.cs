using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client;
using Lyking.GameFramework.Client.Core.Dispatching;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Assets;
using UnityEngine;

namespace Lyking.GameFramework.Client.Unity
{
    /// <summary>
    /// Unity 启动引导：挂到场景根物体上。子类覆写 <see cref="CreateModules"/> 提供业务模块，
    /// 覆写 <see cref="ConfigureServices"/> 注册平台服务（资源、存档等真实实现）。
    /// 生命周期：Awake 装配 → Start 异步启动 → Update/FixedUpdate/LateUpdate 驱动帧循环
    /// → OnDestroy 异步关闭。
    /// </summary>
    public abstract class GameEntry : MonoBehaviour
    {
        private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromSeconds(3);
        private CancellationTokenSource? _startupCancellation;
        private Task? _startupTask;
        private bool _destroyed;

        /// <summary>装配完成的客户端实例；Awake 前为 null。</summary>
        public GameClient? Client { get; private set; }

        protected virtual void Awake()
        {
            UnityLogSink sink = new UnityLogSink();
            Logger logger = new Logger("GameClient", sink);
            Client = new GameClient(logger, CreateModules());
            Client.Services.Register<ILogSink>(sink);
            ConfigureServices(Client.Services);
        }

        protected virtual void Start()
        {
            if (Client == null)
            {
                return;
            }

            _startupCancellation = new CancellationTokenSource();
            _startupTask = RunStartupAsync(Client, _startupCancellation.Token);
        }

        private async Task RunStartupAsync(GameClient client, CancellationToken cancellationToken)
        {
            try
            {
                await client.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (_destroyed)
            {
                // 销毁期间主动取消启动；回滚在客户端模块宿主中有独立时限。
            }
            catch (Exception ex)
            {
                // 启动失败已由 GameClient 记录并置 Failed；这里兜底避免静默中断。
                Debug.LogException(ex);
            }
        }

        protected virtual void Update()
        {
            Client?.Dispatcher.Pump();
            if (IsRunning)
            {
                Client!.Update(Time.deltaTime);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (IsRunning)
            {
                Client!.FixedUpdate(Time.fixedDeltaTime);
            }
        }

        protected virtual void LateUpdate()
        {
            if (IsRunning)
            {
                Client!.LateUpdate(Time.deltaTime);
            }
        }

        protected virtual void OnDestroy()
        {
            _destroyed = true;
            _startupCancellation?.Cancel();
            GameClient? client = Client;
            Client = null;
            if (client == null)
            {
                return;
            }

            RunShutdownAsync(client, _startupTask ?? Task.CompletedTask);
        }

        private async void RunShutdownAsync(GameClient client, Task startupTask)
        {
            try
            {
                if (!await WaitWithPumpingAsync(client.Dispatcher, startupTask, LifecycleTimeout))
                {
                    AbortAfterTimeout(client, "启动", startupTask);
                    return;
                }

                if (client.State == ClientState.Running || client.State == ClientState.Failed)
                {
                    Task shutdownTask = client.ShutdownAsync();
                    if (!await WaitWithPumpingAsync(client.Dispatcher, shutdownTask, LifecycleTimeout))
                    {
                        AbortAfterTimeout(client, "关闭", shutdownTask);
                    }
                }
                else if (client.State == ClientState.Created)
                {
                    await client.Dispatcher.ShutdownAsync(LifecycleTimeout);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _startupCancellation?.Dispose();
                _startupCancellation = null;
            }
        }

        private static async Task<bool> WaitWithPumpingAsync(IMainThreadDispatcher dispatcher, Task operation, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!operation.IsCompleted && DateTime.UtcNow < deadline)
            {
                dispatcher.Pump();
                await Task.Delay(1);
            }

            dispatcher.Pump();
            return operation.IsCompleted;
        }

        private static void AbortAfterTimeout(GameClient client, string phase, Task operation)
        {
            try
            {
                client.AbortPendingOperations();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            Debug.LogError($"客户端{phase}未在销毁时限内收敛；状态为 {client.State}，生命周期任务状态为 {operation.Status}。");
        }

        private bool IsRunning => Client != null && Client.State == ClientState.Running;

        /// <summary>提供业务模块清单（框架内置模块已由 GameClient 注册）。</summary>
        protected abstract IReadOnlyList<IClientModule> CreateModules();

        /// <summary>注册平台服务实现（如 ResourcesAssetProvider、本地化词条表来源）。</summary>
        protected virtual void ConfigureServices(IServiceRegistry services)
        {
            services.Register<IAssetProvider>(new ResourcesAssetProvider());
        }
    }
}
