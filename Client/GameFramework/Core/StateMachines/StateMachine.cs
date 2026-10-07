using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Core.StateMachines
{
    /// <summary>
    /// 轻量状态机：显式迁移表 + 进入/退出回调。
    /// 迁移顺序：先退出旧状态，再进入新状态；非法迁移抛 InvalidOperationException。
    /// 主线程使用，不保证线程安全。
    /// </summary>
    public sealed class StateMachine<TState> where TState : notnull
    {
        private readonly HashSet<(TState From, TState To)> _permitted = new HashSet<(TState, TState)>();
        private readonly Action<TState>? _onEnter;
        private readonly Action<TState>? _onExit;

        public StateMachine(TState initial, Action<TState>? onEnter = null, Action<TState>? onExit = null)
        {
            Current = initial;
            _onEnter = onEnter;
            _onExit = onExit;
        }

        public TState Current { get; private set; }

        /// <summary>累计迁移次数，诊断用。</summary>
        public int TransitionCount { get; private set; }

        /// <summary>迁移完成事件：(旧状态, 新状态)。</summary>
        public event Action<TState, TState>? Transitioned;

        /// <summary>允许 from → to 的迁移；重复注册幂等。</summary>
        public void Permit(TState from, TState to) => _permitted.Add((from, to));

        public bool CanTransition(TState to) => _permitted.Contains((Current, to));

        /// <summary>迁移到目标状态；与当前状态相同则忽略；未允许则抛异常。</summary>
        public void TransitionTo(TState next)
        {
            if (EqualityComparer<TState>.Default.Equals(Current, next))
            {
                return;
            }

            if (!CanTransition(next))
            {
                throw new InvalidOperationException($"状态机不允许从 {Current} 迁移到 {next}。");
            }

            TState previous = Current;
            _onExit?.Invoke(previous);
            Current = next;
            TransitionCount++;
            _onEnter?.Invoke(next);
            Transitioned?.Invoke(previous, next);
        }
    }
}
