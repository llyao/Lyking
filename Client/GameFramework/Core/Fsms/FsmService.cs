using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Core.Fsms
{
    /// <summary>
    /// 状态机服务：集中创建与驱动多个具名状态机（AI、加载管线、玩法子流程各持一个），
    /// 随帧循环统一推进。状态机的生命周期由本服务管理，关闭时逆序销毁。
    /// </summary>
    public sealed class FsmService : ClientModule
    {
        private readonly Dictionary<string, Fsm> _fsms = new Dictionary<string, Fsm>(StringComparer.Ordinal);
        private readonly List<Fsm> _order = new List<Fsm>();

        public FsmService() : base("Fsm", priority: -900)
        {
        }

        public int FsmCount => _order.Count;

        /// <summary>创建状态机；重名抛 ArgumentException。状态机的 OnInit 在创建时立即执行。</summary>
        public Fsm CreateFsm(string name, params FsmState[] states)
        {
            var fsm = new Fsm(name, states);
            if (!_fsms.TryAdd(name, fsm))
            {
                throw new ArgumentException($"状态机 {name} 已存在。", nameof(name));
            }

            _order.Add(fsm);
            return fsm;
        }

        public bool TryGetFsm(string name, out Fsm? fsm) =>
            _fsms.TryGetValue(name ?? throw new ArgumentNullException(nameof(name)), out fsm);

        /// <summary>启动指定状态机；不存在抛 InvalidOperationException。</summary>
        public void StartFsm<TState>(string name) where TState : FsmState => StartFsm(name, typeof(TState));

        public void StartFsm(string name, Type initialStateType)
        {
            if (!_fsms.TryGetValue(name ?? throw new ArgumentNullException(nameof(name)), out Fsm? fsm))
            {
                throw new InvalidOperationException($"状态机 {name} 不存在。");
            }

            fsm.Start(initialStateType);
        }

        /// <summary>销毁状态机：先 Shutdown 再移除；不存在返回 false。</summary>
        public bool DestroyFsm(string name)
        {
            if (!_fsms.TryGetValue(name ?? throw new ArgumentNullException(nameof(name)), out Fsm? fsm))
            {
                return false;
            }

            _fsms.Remove(name);
            _order.Remove(fsm);
            fsm.Shutdown();
            return true;
        }

        public override void Update(double deltaTime)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Update(deltaTime);
            }
        }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            foreach (Fsm fsm in _fsms.Values)
            {
                fsm.Shutdown();
                Log.Debug($"状态机 {fsm.Name} 已销毁。");
            }

            _fsms.Clear();
            _order.Clear();
            return base.ShutdownAsync(cancellationToken);
        }
    }
}
