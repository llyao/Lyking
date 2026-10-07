using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Fsms;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Core.Procedures
{
    /// <summary>游戏流程状态基类：Splash → 检查版本 → 更新资源 → 进入主城 → 战斗 …… 的"状态即类"。</summary>
    public abstract class ProcedureState : FsmState
    {
    }

    /// <summary>
    /// 游戏流程模块：单状态机驱动整局游戏的主流程。
    /// 业务在启动前（通常在另一个模块的 InitializeAsync 里）注册流程并指定初始流程；
    /// 启动后由帧循环推进当前流程，流程内部通过 ChangeState 切换。
    /// </summary>
    public sealed class ProcedureModule : ClientModule
    {
        private readonly List<ProcedureState> _procedures = new List<ProcedureState>();
        private Type? _initialProcedureType;
        private Fsm? _fsm;

        public ProcedureModule() : base("Procedure", priority: -800)
        {
        }

        /// <summary>当前流程类型名；未运行为 null。</summary>
        public string? CurrentProcedureName => _fsm?.CurrentStateType?.Name;

        /// <summary>流程机是否已启动。</summary>
        public bool IsRunning => _fsm?.IsRunning ?? false;

        public event Action<string, string>? ProcedureChanged;

        /// <summary>注册流程；启动后调用抛 InvalidOperationException。</summary>
        public ProcedureModule AddProcedure(ProcedureState procedure)
        {
            if (procedure is null)
            {
                throw new ArgumentNullException(nameof(procedure));
            }

            if (_fsm != null)
            {
                throw new InvalidOperationException("流程机已启动，不能再注册流程。");
            }

            _procedures.Add(procedure);
            return this;
        }

        /// <summary>指定初始流程；必须在启动前调用。</summary>
        public ProcedureModule SetInitial<TProcedure>() where TProcedure : ProcedureState
        {
            _initialProcedureType = typeof(TProcedure);
            return this;
        }

        public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            if (_procedures.Count == 0)
            {
                Log.Warn("未注册任何流程，流程模块将空转。");
                return base.InitializeAsync(environment, cancellationToken);
            }

            _fsm = new Fsm("GameProcedure", _procedures.ToArray());
            _fsm.StateChanged += (from, to) => ProcedureChanged?.Invoke(from, to);
            return base.InitializeAsync(environment, cancellationToken);
        }

        public override void Start()
        {
            if (_fsm == null)
            {
                return;
            }

            if (_initialProcedureType == null)
            {
                Log.Warn("未指定初始流程，流程机未启动。调用 SetInitial<T> 后重启。");
                return;
            }

            _fsm.Start(_initialProcedureType);
            Log.Info($"流程机启动，初始流程 {_initialProcedureType.Name}。");
        }

        public override void Update(double deltaTime) => _fsm?.Update(deltaTime);

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            _fsm?.Shutdown();
            _fsm = null;
            return base.ShutdownAsync(cancellationToken);
        }
    }
}
