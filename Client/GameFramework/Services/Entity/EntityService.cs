using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Services.Entity
{
    /// <summary>
    /// 实体逻辑合同：一个业务实体（角色、NPC、特效、子弹）的行为面。
    /// 生命周期：首次创建 OnInit → 每次展示 OnShow → 每帧 OnUpdate → 隐藏 OnHide → 归池前 OnReset。
    /// 实例由 <see cref="IEntityInstanceProvider"/> 创建（Unity 侧实例化 prefab 并挂逻辑组件），
    /// 隐藏不销毁，回池复用。
    /// </summary>
    public interface IEntityLogic
    {
        /// <summary>实体 id，全服务唯一，由 EntityService 分配。</summary>
        int EntityId { get; set; }

        /// <summary>实体资源 key（Unity 侧即 prefab 名），由服务注入。</summary>
        string Key { get; set; }

        /// <summary>首次创建回调（只执行一次）。</summary>
        void OnInit();

        void OnShow(object? userData);

        void OnUpdate(double deltaTime);

        void OnHide(object? userData);

        /// <summary>归还对象池前的状态重置。</summary>
        void OnReset();
    }

    /// <summary>实体逻辑基类：默认空实现，业务按需覆写。</summary>
    public abstract class EntityLogic : IEntityLogic
    {
        public int EntityId { get; set; }

        public string Key { get; set; } = string.Empty;

        protected bool IsShown { get; private set; }

        public virtual void OnInit()
        {
        }

        public virtual void OnShow(object? userData)
        {
            IsShown = true;
        }

        public virtual void OnUpdate(double deltaTime)
        {
        }

        public virtual void OnHide(object? userData)
        {
            IsShown = false;
        }

        public virtual void OnReset()
        {
        }
    }

    /// <summary>实体实例提供器：实例来源抽象。模拟实现直接 new；Unity 侧实例化 prefab 取逻辑组件。</summary>
    public interface IEntityInstanceProvider
    {
        IEntityLogic Create(string key);
    }

    /// <summary>实体组：同组实体共享容量上限，超限时自动回收最早展示且未固定的实体。</summary>
    public sealed class EntityGroup
    {
        internal EntityGroup(string name, int capacity)
        {
            Name = name;
            Capacity = capacity;
        }

        public string Name { get; }

        /// <summary>同组可见实体上限；0 表示不限制。</summary>
        public int Capacity { get; }

        public int VisibleCount { get; internal set; }
    }

    /// <summary>
    /// 实体服务：按组管理实体展示/隐藏/回收，内置按 key 的实例池。
    /// 容量语义：组满时回收最早展示且未固定（pinned）的可见实体腾位；
    /// 全部固定仍要展示则抛 InvalidOperationException。
    /// 每实体回调异常被隔离上报（EntityFaulted），不影响其他实体。
    /// </summary>
    public sealed class EntityService : ClientModule
    {
        private readonly IEntityInstanceProvider _instanceProvider;
        private readonly Dictionary<string, EntityGroup> _groups = new Dictionary<string, EntityGroup>(StringComparer.Ordinal);
        private readonly Dictionary<int, EntityRecord> _visible = new Dictionary<int, EntityRecord>();
        private readonly Dictionary<string, Stack<IEntityLogic>> _pool = new Dictionary<string, Stack<IEntityLogic>>(StringComparer.Ordinal);
        private int _nextEntityId;

        public EntityService(IEntityInstanceProvider instanceProvider) : base("Entity", priority: 200)
        {
            _instanceProvider = instanceProvider ?? throw new ArgumentNullException(nameof(instanceProvider));
        }

        /// <summary>单实体回调异常：记录后上报，其他实体继续更新。</summary>
        public event Action<IEntityLogic, Exception>? EntityFaulted;

        public event Action<IEntityLogic>? EntityShown;

        public event Action<IEntityLogic>? EntityHidden;

        /// <summary>组容量；未注册组返回 0。</summary>
        public int GetGroupCapacity(string groupName) =>
            _groups.TryGetValue(groupName, out EntityGroup? group) ? group.Capacity : 0;

        public bool HasGroup(string groupName) => _groups.ContainsKey(groupName);

        public int VisibleEntityCount => _visible.Count;

        public int VisibleCount(string groupName) =>
            _groups.TryGetValue(groupName, out EntityGroup? group) ? group.VisibleCount : 0;

        /// <summary>key 的池内空闲实例数，诊断用。</summary>
        public int PooledCount(string key) =>
            _pool.TryGetValue(key, out Stack<IEntityLogic>? stack) ? stack.Count : 0;

        public void AddGroup(string groupName, int capacity = 0)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("组名不能为空。", nameof(groupName));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "容量不能为负。");
            }

            if (!_groups.TryAdd(groupName, new EntityGroup(groupName, capacity)))
            {
                throw new InvalidOperationException($"实体组 {groupName} 已存在。");
            }
        }

        /// <summary>展示实体，返回实体 id。</summary>
        public int Show(string key, string groupName, object? userData = null, bool pinned = false)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("实体 key 不能为空。", nameof(key));
            }

            if (!_groups.TryGetValue(groupName ?? throw new ArgumentNullException(nameof(groupName)), out EntityGroup? group))
            {
                throw new InvalidOperationException($"实体组 {groupName} 未注册。");
            }

            if (group.Capacity > 0 && group.VisibleCount >= group.Capacity && !RecycleOldestUnpinned(group))
            {
                throw new InvalidOperationException($"实体组 {groupName} 容量已满（{group.Capacity}）且全部固定，无法展示 {key}。");
            }

            IEntityLogic logic = RentLogic(key);
            logic.Key = key;
            logic.EntityId = ++_nextEntityId;
            var record = new EntityRecord(logic, pinned, _nextEntityId, groupName);
            _visible[logic.EntityId] = record;
            group.VisibleCount++;
            logic.OnShow(userData);
            EntityShown?.Invoke(logic);
            return logic.EntityId;
        }

        /// <summary>隐藏实体并回池；id 不存在返回 false。</summary>
        public bool Hide(int entityId, object? userData = null)
        {
            if (!_visible.Remove(entityId, out EntityRecord? record))
            {
                return false;
            }

            if (_groups.TryGetValue(record.GroupName, out EntityGroup? group))
            {
                group.VisibleCount--;
            }

            record.Logic.OnHide(userData);
            ReturnLogic(record.Logic);
            EntityHidden?.Invoke(record.Logic);
            return true;
        }

        public bool IsVisible(int entityId) => _visible.ContainsKey(entityId);

        public bool IsPinned(int entityId) => _visible.TryGetValue(entityId, out EntityRecord? record) && record.Pinned;

        /// <summary>隐藏指定组的全部实体。</summary>
        public int HideGroup(string groupName)
        {
            int hidden = 0;
            foreach (KeyValuePair<int, EntityRecord> pair in _visible.ToArray())
            {
                if (string.Equals(pair.Value.GroupName, groupName, StringComparison.Ordinal) && Hide(pair.Key))
                {
                    hidden++;
                }
            }

            return hidden;
        }

        public override void Update(double deltaTime)
        {
            foreach (KeyValuePair<int, EntityRecord> pair in _visible)
            {
                try
                {
                    pair.Value.Logic.OnUpdate(deltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error($"实体 {pair.Value.Logic.Key}({pair.Key}) 更新异常，已隔离。", ex);
                    EntityFaulted?.Invoke(pair.Value.Logic, ex);
                }
            }
        }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<int, EntityRecord> pair in _visible.ToArray())
            {
                Hide(pair.Key);
            }

            _pool.Clear();
            _groups.Clear();
            return base.ShutdownAsync(cancellationToken);
        }

        private bool RecycleOldestUnpinned(EntityGroup group)
        {
            EntityRecord? oldest = null;
            foreach (EntityRecord record in _visible.Values)
            {
                if (!string.Equals(record.GroupName, group.Name, StringComparison.Ordinal) || record.Pinned)
                {
                    continue;
                }

                if (oldest == null || record.Sequence < oldest.Sequence)
                {
                    oldest = record;
                }
            }

            return oldest != null && Hide(oldest.Logic.EntityId);
        }

        private IEntityLogic RentLogic(string key)
        {
            if (_pool.TryGetValue(key, out Stack<IEntityLogic>? stack) && stack.Count > 0)
            {
                return stack.Pop();
            }

            IEntityLogic created = _instanceProvider.Create(key)
                ?? throw new InvalidOperationException($"实例提供器为 key={key} 返回 null。");
            created.OnInit();
            return created;
        }

        private void ReturnLogic(IEntityLogic logic)
        {
            logic.OnReset();
            if (!_pool.TryGetValue(logic.Key, out Stack<IEntityLogic>? stack))
            {
                stack = new Stack<IEntityLogic>();
                _pool[logic.Key] = stack;
            }

            stack.Push(logic);
        }

        private sealed class EntityRecord
        {
            public EntityRecord(IEntityLogic logic, bool pinned, int sequence, string groupName)
            {
                Logic = logic;
                Pinned = pinned;
                Sequence = sequence;
                GroupName = groupName;
            }

            public IEntityLogic Logic { get; }

            public bool Pinned { get; }

            public int Sequence { get; }

            public string GroupName { get; }
        }
    }
}
