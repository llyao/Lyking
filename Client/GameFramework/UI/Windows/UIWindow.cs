namespace Lyking.GameFramework.Client.UI
{
    /// <summary>
    /// 窗口视图合同：由具体界面实现（Unity 中通常包一个 GameObject/UGUI 面板）。
    /// 生命周期回调顺序：OnOpened → OnFocus/OnPause（随焦点与模态变化）→ OnClosed。
    /// OnPause 为默认接口方法（不暂停语义），已有实现无需改动即可接入模态暂停。
    /// </summary>
    public interface IWindowView
    {
        /// <summary>窗口已进入打开状态。</summary>
        void OnOpened();

        /// <summary>焦点变化：focused = 是否为全局唯一焦点窗口。</summary>
        void OnFocus(bool focused);

        /// <summary>模态暂停变化：paused = 是否被更高层的模态窗口压制。</summary>
        void OnPause(bool paused)
        {
        }

        /// <summary>窗口已关闭，视图随后不再被服务引用。</summary>
        void OnClosed();
    }

    /// <summary>服务内在的窗口记录：描述、视图与打开序号。</summary>
    public sealed class UIWindow
    {
        internal UIWindow(UIWindowDescriptor descriptor, IWindowView view, int sequence, object? userData)
        {
            Descriptor = descriptor;
            View = view;
            Sequence = sequence;
            UserData = userData;
        }

        public UIWindowDescriptor Descriptor { get; }

        public IWindowView View { get; }

        /// <summary>单调递增的打开序号，诊断与排序用。</summary>
        public int Sequence { get; }

        /// <summary>打开时携带的业务数据（如"从哪个入口打开"）。</summary>
        public object? UserData { get; }

        /// <summary>是否为当前焦点窗口（由 UIWindowService 维护）。</summary>
        public bool Focused { get; internal set; }

        /// <summary>是否被更高层模态窗口暂停（由 UIWindowService 维护）。</summary>
        public bool Paused { get; internal set; }
    }
}
