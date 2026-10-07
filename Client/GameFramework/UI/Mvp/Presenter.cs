using System;

namespace Lyking.GameFramework.Client.UI.Mvp
{
    /// <summary>
    /// Presenter 基类：持有视图并在 Attach/Detach 周期回调子类。
    /// 视图类型不限于是窗口视图——弹窗、面板、HUD 均可用同一模式。
    /// </summary>
    public abstract class Presenter<TView> where TView : class
    {
        private TView? _view;

        /// <summary>当前视图；未绑定为 null。</summary>
        public TView? View => _view;

        public bool IsAttached => _view != null;

        public void Attach(TView view)
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (_view != null)
            {
                throw new InvalidOperationException("Presenter 已绑定视图，请先 Detach。");
            }

            _view = view;
            OnViewAttached(view);
        }

        public void Detach()
        {
            if (_view == null)
            {
                return;
            }

            TView view = _view;
            _view = null;
            OnViewDetached(view);
        }

        /// <summary>视图绑定后回调（订阅视图事件、初始化展示的推荐位置）。</summary>
        protected virtual void OnViewAttached(TView view)
        {
        }

        /// <summary>视图解绑前回调（退订事件，防止悬空引用）。</summary>
        protected virtual void OnViewDetached(TView view)
        {
        }
    }
}
