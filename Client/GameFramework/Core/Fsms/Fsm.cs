using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Core.Fsms
{
    /// <summary>
    /// 类状态基类：与 <see cref="StateMachines.StateMachine{TState}"/> 的枚举状态机互补。
    /// 枚举状态机适合子系统内部小状态；本类型面向流程、AI、加载管线等
    /// "状态即类"的场景（参考 GameFramework 的 Fsm 设计）。
    /// 状态内部通过 <see cref="ChangeState{TState}"/> 主动迁移，不设集中迁移表。
    /// </summary>
    public abstract class FsmState
    {
        /// <summary>所属状态机；未加入状态机前访问抛 InvalidOperationException。</summary>
        protected Fsm Owner { get; private set; } = null!;

        /// <summary>当前状态已停留时长（秒）。</summary>
        protected double TimeInState => Owner.TimeInState;

        /// <summary>迁移到指定状态；退出当前状态后进入新状态。</summary>
        protected void ChangeState<TState>() where TState : FsmState
            => Owner.ChangeState(typeof(TState));

        /// <summary>迁移到指定状态（运行期动态类型版本）。</summary>
        protected void ChangeState(Type stateType)
            => Owner.ChangeState(stateType);

        internal void Bind(Fsm owner) => Owner = owner;

        // 状态机宿主通过内部分发器调用受保护钩子（C# 不允许跨类型直呼 protected 成员）。

        internal void InvokeInit(Fsm fsm) => OnInit(fsm);

        internal void InvokeEnter(Fsm fsm) => OnEnter(fsm);

        internal void InvokeUpdate(Fsm fsm, double deltaTime) => OnUpdate(fsm, deltaTime);

        internal void InvokeExit(Fsm fsm) => OnExit(fsm);

        internal void InvokeDestroy(Fsm fsm) => OnDestroy(fsm);

        /// <summary>状态机创建时回调一次（缓存协作者等准备工作）。</summary>
        protected virtual void OnInit(Fsm fsm)
        {
        }

        protected virtual void OnEnter(Fsm fsm)
        {
        }

        protected virtual void OnUpdate(Fsm fsm, double deltaTime)
        {
        }

        protected virtual void OnExit(Fsm fsm)
        {
        }

        /// <summary>状态机关闭时对所有状态回调一次。</summary>
        protected virtual void OnDestroy(Fsm fsm)
        {
        }
    }

    /// <summary>
    /// 类状态机：状态以类型为键，单当前态，迁移顺序固定为"退出旧 → 进入新"。
    /// 同状态重入会触发完整的退出与进入回调（与 GameFramework 语义一致）。
    /// 主线程使用，不保证线程安全。
    /// </summary>
    public sealed class Fsm
    {
        private readonly Dictionary<Type, FsmState> _states;
        private bool _running;
        private bool _destroyed;

        public Fsm(string name, params FsmState[] states)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("状态机名不能为空。", nameof(name));
            }

            if (states is null || states.Length == 0)
            {
                throw new ArgumentException("状态机至少需要一个状态。", nameof(states));
            }

            Name = name;
            _states = new Dictionary<Type, FsmState>(states.Length);
            foreach (FsmState state in states)
            {
                if (state is null)
                {
                    throw new ArgumentException("状态不能为 null。", nameof(states));
                }

                if (!_states.TryAdd(state.GetType(), state))
                {
                    throw new ArgumentException($"状态类型 {state.GetType().FullName} 重复。", nameof(states));
                }

                state.Bind(this);
                state.InvokeInit(this);
            }
        }

        public string Name { get; }

        public bool IsRunning => _running;

        public FsmState? CurrentState { get; private set; }

        public Type? CurrentStateType => CurrentState?.GetType();

        /// <summary>当前状态已停留时长（秒）。</summary>
        public double TimeInState { get; private set; }

        public int StateCount => _states.Count;

        /// <summary>状态迁移事件：(旧状态类型名, 新状态类型名)。</summary>
        public event Action<string, string>? StateChanged;

        /// <summary>以指定状态启动状态机；重复启动抛 InvalidOperationException。</summary>
        public void Start<TState>() where TState : FsmState => Start(typeof(TState));

        public void Start(Type initialStateType)
        {
            if (_running)
            {
                throw new InvalidOperationException($"状态机 {Name} 已在运行。");
            }

            FsmState state = ResolveState(initialStateType);
            _running = true;
            TimeInState = 0;
            CurrentState = state;
            state.InvokeEnter(this);
        }

        /// <summary>迁移到目标状态；必须处于运行中，目标状态必须已注册。</summary>
        public void ChangeState<TState>() where TState : FsmState => ChangeState(typeof(TState));

        public void ChangeState(Type stateType)
        {
            if (!_running)
            {
                throw new InvalidOperationException($"状态机 {Name} 未运行，无法迁移。");
            }

            FsmState next = ResolveState(stateType);
            FsmState previous = CurrentState!;
            previous.InvokeExit(this);
            CurrentState = next;
            TimeInState = 0;
            next.InvokeEnter(this);
            StateChanged?.Invoke(previous.GetType().Name, next.GetType().Name);
        }

        /// <summary>推进当前状态；未运行时为空操作。</summary>
        public void Update(double deltaTime)
        {
            if (!_running)
            {
                return;
            }

            TimeInState += deltaTime;
            CurrentState!.InvokeUpdate(this, deltaTime);
        }

        /// <summary>退出当前状态并对全部状态执行 OnDestroy；幂等。</summary>
        public void Shutdown()
        {
            if (_destroyed)
            {
                return;
            }

            if (_running)
            {
                _running = false;
                CurrentState!.InvokeExit(this);
                CurrentState = null;
            }

            _destroyed = true;
            foreach (FsmState state in _states.Values)
            {
                state.InvokeDestroy(this);
            }
        }

        public TState GetState<TState>() where TState : FsmState
            => (TState)ResolveState(typeof(TState));

        public bool IsInState<TState>() where TState : FsmState
            => CurrentStateType == typeof(TState);

        private FsmState ResolveState(Type stateType)
        {
            if (_states.TryGetValue(stateType, out FsmState? state))
            {
                return state;
            }

            throw new InvalidOperationException($"状态机 {Name} 未注册状态 {stateType.FullName}。");
        }
    }
}
