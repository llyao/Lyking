using System;

namespace Lyking.GameFramework.Client.UI
{
    /// <summary>窗口打开描述：身份与层级行为，与具体视图实现无关。</summary>
    public sealed class UIWindowDescriptor
    {
        public UIWindowDescriptor(
            string name,
            UILayer layer = UILayer.Main,
            bool exclusive = false,
            bool modal = false,
            bool unique = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("窗口名不能为空。", nameof(name));
            }

            Name = name;
            Layer = layer;
            Exclusive = exclusive;
            Modal = modal;
            Unique = unique;
        }

        /// <summary>窗口名，打开期唯一（unique 窗口）或同类多开。</summary>
        public string Name { get; }

        public UILayer Layer { get; }

        /// <summary>独占：打开时关闭所有更低层的窗口。</summary>
        public bool Exclusive { get; }

        /// <summary>模态：打开期间暂停所有更低层的窗口（OnPause(true)）。</summary>
        public bool Modal { get; }

        /// <summary>唯一：已打开时再次打开只置顶，不新建。</summary>
        public bool Unique { get; }
    }
}
