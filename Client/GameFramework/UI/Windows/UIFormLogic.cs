using System;

namespace Lyking.GameFramework.Client.UI
{
    /// <summary>
    /// 窗口业务逻辑基类：业务界面继承本类而不是直接实现 IWindowView，
    /// 服务在打开时自动把窗口记录（含 userData）绑进来。
    /// 生命周期：OnInit → OnOpen(userData) → OnFocus/OnPause（随状态）→ OnClose。
    /// Unity 侧通常再包一层：本类持数据与逻辑，MonoBehaviour 视图转发输入与显示。
    /// </summary>
    public abstract class UIFormLogic : IWindowView
    {
        private UIWindow? _window;

        /// <summary>所属窗口记录；仅在本视图被窗口服务打开期间有效。</summary>
        protected UIWindow Window => _window ?? throw new InvalidOperationException("窗口尚未被打开或已关闭。");

        /// <summary>打开时携带的业务数据。</summary>
        protected object? UserData => _window?.UserData;

        internal void Attach(UIWindow window) => _window = window;

        void IWindowView.OnOpened()
        {
            OnInit(Window);
            OnOpen(Window.UserData);
        }

        void IWindowView.OnFocus(bool focused) => OnFocus(focused);

        void IWindowView.OnPause(bool paused) => OnPause(paused);

        void IWindowView.OnClosed()
        {
            try
            {
                OnClose();
            }
            finally
            {
                _window = null;
            }
        }

        /// <summary>首次打开回调（窗口级一次性初始化）。</summary>
        protected virtual void OnInit(UIWindow window)
        {
        }

        /// <summary>每次打开回调，userData 携带打开上下文。</summary>
        protected virtual void OnOpen(object? userData)
        {
        }

        protected virtual void OnFocus(bool focused)
        {
        }

        protected virtual void OnPause(bool paused)
        {
        }

        protected virtual void OnClose()
        {
        }
    }
}
