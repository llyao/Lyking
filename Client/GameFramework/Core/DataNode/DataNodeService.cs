using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Core.DataNode
{
    /// <summary>
    /// 数据结点：树状运行时数据容器（参考 GameFramework 的 DataNode），
    /// 适合存"当前对局 id、主城坐标"这类跨模块共享的运行时状态，
    /// 以 "A.B.C" 路径寻址，避免为每个零散状态建全局变量。
    /// </summary>
    public sealed class DataNode
    {
        private readonly List<DataNode> _children = new List<DataNode>();

        internal DataNode(string name, DataNode? parent)
        {
            Name = name;
            Parent = parent;
        }

        public string Name { get; }

        public DataNode? Parent { get; internal set; }

        /// <summary>全路径（不含根），如 "Battle.CurrentWave"。</summary>
        public string FullName
        {
            get
            {
                if (Parent == null || string.IsNullOrEmpty(Parent.Name))
                {
                    return Name;
                }

                return Parent.FullName + "." + Name;
            }
        }

        public int Depth
        {
            get
            {
                int depth = 0;
                DataNode? node = this;
                while (node != null && !string.IsNullOrEmpty(node.Name))
                {
                    depth++;
                    node = node.Parent;
                }

                return depth;
            }
        }

        public int ChildCount => _children.Count;

        public bool HasData { get; private set; }

        public IReadOnlyList<DataNode> Children => _children;

        /// <summary>写入数据；写 null 等价于 Clear。</summary>
        public void SetData(object? value)
        {
            Data = value;
            HasData = value != null;
        }

        public object? Data { get; private set; }

        /// <summary>读取数据；类型不符抛 InvalidOperationException，无数据抛 InvalidOperationException。</summary>
        public T GetData<T>()
        {
            if (!HasData)
            {
                throw new InvalidOperationException($"结点 {FullName} 没有数据。");
            }

            if (Data is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException($"结点 {FullName} 的数据类型 {Data!.GetType().Name} 与请求类型 {typeof(T).Name} 不匹配。");
        }

        public bool TryGetData<T>(out T value)
        {
            if (HasData && Data is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public void Clear()
        {
            Data = null;
            HasData = false;
        }

        /// <summary>取子结点；不存在且 create 为 false 返回 null。</summary>
        public DataNode? GetChild(string name, bool create = false)
        {
            ValidateName(name);
            foreach (DataNode child in _children)
            {
                if (string.Equals(child.Name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            if (!create)
            {
                return null;
            }

            var node = new DataNode(name, this);
            _children.Add(node);
            return node;
        }

        /// <summary>移除子结点（含其整棵子树），返回是否存在。</summary>
        public bool RemoveChild(string name)
        {
            ValidateName(name);
            for (int i = 0; i < _children.Count; i++)
            {
                if (string.Equals(_children[i].Name, name, StringComparison.Ordinal))
                {
                    _children[i].Parent = null;
                    _children.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        internal static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("结点名不能为空。", nameof(name));
            }
        }
    }

    /// <summary>数据结点服务：以模块身份提供全局数据树，路径以 "." 分层寻址。</summary>
    public sealed class DataNodeService : ClientModule
    {
        private DataNode _root;

        public DataNodeService() : base("DataNode", priority: -700)
        {
            _root = new DataNode(string.Empty, null);
        }

        /// <summary>按路径取结点；missing 时 create=true 自动建链，否则返回 null。</summary>
        public DataNode? GetNode(string path, bool create = false)
        {
            ValidatePath(path);
            DataNode? current = _root;
            foreach (string segment in path.Split('.'))
            {
                current = current!.GetChild(segment, create);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        public void Set<T>(string path, T value) => GetNode(path, create: true)!.SetData(value);

        /// <summary>按路径读取数据；路径缺失或类型不符抛异常。</summary>
        public T Get<T>(string path)
        {
            DataNode? node = GetNode(path, create: false);
            if (node == null)
            {
                throw new InvalidOperationException($"数据结点 {path} 不存在。");
            }

            return node.GetData<T>();
        }

        public bool TryGet<T>(string path, out T value)
        {
            DataNode? node = GetNode(path, create: false);
            if (node != null && node.TryGetData(out value))
            {
                return true;
            }

            value = default!;
            return false;
        }

        public bool Remove(string path)
        {
            int lastDot = path.LastIndexOf('.');
            string parentPath = lastDot < 0 ? string.Empty : path[..lastDot];
            string leaf = lastDot < 0 ? path : path[(lastDot + 1)..];
            DataNode? parent = parentPath.Length == 0 ? _root : GetNode(parentPath, create: false);
            return parent != null && parent.RemoveChild(leaf);
        }

        /// <summary>清空整棵数据树。</summary>
        public void ClearAll()
        {
            _root = new DataNode(string.Empty, null);
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("数据路径不能为空。", nameof(path));
            }
        }
    }
}
