using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.UI
{
    /// <summary>
    /// 窗口服务：按层级维护窗口栈，管理打开/关闭/置顶与全局唯一焦点、模态暂停。
    /// 规则：
    /// - 层级数值大者在上；同层内后打开者在栈顶；
    /// - unique 窗口重复打开只置顶；非 unique 窗口可同名人多开；
    /// - exclusive 打开时自底向上关闭所有更低层的窗口；
    /// - 全局焦点唯一：最高层栈顶窗口获得焦点，其余窗口失焦；焦点变化才回调 OnFocus；
    /// - 模态窗口打开期间，所有更低层窗口处于暂停态（OnPause(true)），变化时才回调。
    /// 视图实例由调用方创建传入（Unity 侧负责实例化面板 GameObject）；
    /// 视图继承 UIFormLogic 时自动接上窗口记录与 userData。
    /// </summary>
    public sealed class UIWindowService
    {
        private readonly Dictionary<UILayer, List<UIWindow>> _layers = new Dictionary<UILayer, List<UIWindow>>();
        private readonly Dictionary<string, List<UIWindow>> _byName = new Dictionary<string, List<UIWindow>>(StringComparer.Ordinal);
        private readonly ILogger? _logger;
        private int _nextSequence;

        public UIWindowService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>窗口打开事件。</summary>
        public event Action<UIWindow>? Opened;

        public event Action<UIWindow>? Closed;

        /// <summary>焦点变化事件：(窗口, 是否获得焦点)。</summary>
        public event Action<UIWindow, bool>? FocusChanged;

        /// <summary>模态暂停变化事件：(窗口, 是否被暂停)。</summary>
        public event Action<UIWindow, bool>? PauseChanged;

        /// <summary>当前打开的窗口总数。</summary>
        public int OpenCount => _byName.Values.Sum(static list => list.Count);

        /// <summary>打开窗口；userData 透传给 UIFormLogic.OnOpen 与窗口记录。</summary>
        public UIWindow Open(UIWindowDescriptor descriptor, IWindowView view, object? userData = null)
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (descriptor.Unique && TryFind(descriptor.Name, out UIWindow? existing))
            {
                BringToTop(existing);
                _logger?.Debug($"窗口 {descriptor.Name} 已打开，置顶处理。");
                return existing;
            }

            if (descriptor.Exclusive)
            {
                CloseLowerLayers(descriptor.Layer);
            }

            var window = new UIWindow(descriptor, view, _nextSequence++, userData);
            if (view is UIFormLogic form)
            {
                form.Attach(window);
            }

            GetLayerStack(descriptor.Layer).Add(window);
            AddToIndex(window);
            view.OnOpened();
            ApplyFocus();
            _logger?.Debug($"窗口 {descriptor.Name} 已打开（层 {descriptor.Layer}）。");
            Opened?.Invoke(window);
            return window;
        }

        /// <summary>按名关闭一个窗口（多开时关最顶的一个）；不存在返回 false。</summary>
        public bool Close(string name)
        {
            if (!TryFind(name ?? throw new ArgumentNullException(nameof(name)), out UIWindow? window))
            {
                return false;
            }

            RemoveWindow(window);
            window.View.OnClosed();
            ApplyFocus();
            _logger?.Debug($"窗口 {name} 已关闭。");
            Closed?.Invoke(window);
            return true;
        }

        /// <summary>关闭全部窗口（自高向低、自顶向下），返回关闭数量。</summary>
        public int CloseAll()
        {
            int closed = 0;
            foreach (UILayer layer in SortedLayersDescending())
            {
                List<UIWindow> stack = _layers[layer];
                while (stack.Count > 0)
                {
                    UIWindow window = stack[stack.Count - 1];
                    RemoveWindow(window);
                    window.View.OnClosed();
                    Closed?.Invoke(window);
                    closed++;
                }
            }

            ApplyFocus();
            return closed;
        }

        public bool IsOpen(string name) => TryFind(name, out _);

        /// <summary>按名查找窗口（多开时返回最顶的一个）。</summary>
        public UIWindow? Find(string name) => TryFind(name, out UIWindow? window) ? window : null;

        /// <summary>全局最顶窗口；无打开窗口为 null。</summary>
        public UIWindow? TopWindow
        {
            get
            {
                foreach (UILayer layer in SortedLayersDescending())
                {
                    List<UIWindow> stack = _layers[layer];
                    if (stack.Count > 0)
                    {
                        return stack[stack.Count - 1];
                    }
                }

                return null;
            }
        }

        /// <summary>全部打开窗口：自高向低、自顶向下。</summary>
        public IReadOnlyList<UIWindow> OpenWindows
        {
            get
            {
                var result = new List<UIWindow>();
                foreach (UILayer layer in SortedLayersDescending())
                {
                    List<UIWindow> stack = _layers[layer];
                    for (int i = stack.Count - 1; i >= 0; i--)
                    {
                        result.Add(stack[i]);
                    }
                }

                return result;
            }
        }

        private bool TryFind(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UIWindow? window)
        {
            if (_byName.TryGetValue(name, out List<UIWindow>? list) && list.Count > 0)
            {
                window = list[list.Count - 1];
                return true;
            }

            window = null;
            return false;
        }

        private void BringToTop(UIWindow window)
        {
            List<UIWindow> stack = GetLayerStack(window.Descriptor.Layer);
            stack.Remove(window);
            stack.Add(window);
            ApplyFocus();
        }

        private void CloseLowerLayers(UILayer layer)
        {
            foreach (UILayer target in SortedLayersDescending())
            {
                if (target >= layer)
                {
                    continue;
                }

                List<UIWindow> stack = _layers[target];
                while (stack.Count > 0)
                {
                    UIWindow window = stack[stack.Count - 1];
                    RemoveWindow(window);
                    window.View.OnClosed();
                    Closed?.Invoke(window);
                }
            }
        }

        private void RemoveWindow(UIWindow window)
        {
            GetLayerStack(window.Descriptor.Layer).Remove(window);
            if (_byName.TryGetValue(window.Descriptor.Name, out List<UIWindow>? list))
            {
                list.Remove(window);
                if (list.Count == 0)
                {
                    _byName.Remove(window.Descriptor.Name);
                }
            }
        }

        private void ApplyFocus()
        {
            UIWindow? top = TopWindow;
            foreach (UIWindow window in OpenWindows)
            {
                bool focused = ReferenceEquals(window, top);
                if (window.Focused != focused)
                {
                    window.Focused = focused;
                    window.View.OnFocus(focused);
                    FocusChanged?.Invoke(window, focused);
                }

                bool paused = ShouldPause(window);
                if (window.Paused != paused)
                {
                    window.Paused = paused;
                    window.View.OnPause(paused);
                    PauseChanged?.Invoke(window, paused);
                }
            }
        }

        /// <summary>存在更高层的已打开模态窗口时，本窗口被暂停。</summary>
        private bool ShouldPause(UIWindow window)
        {
            foreach (UIWindow other in OpenWindows)
            {
                if (other.Descriptor.Modal && other.Descriptor.Layer > window.Descriptor.Layer)
                {
                    return true;
                }
            }

            return false;
        }

        private List<UIWindow> GetLayerStack(UILayer layer)
        {
            if (!_layers.TryGetValue(layer, out List<UIWindow>? stack))
            {
                stack = new List<UIWindow>();
                _layers[layer] = stack;
            }

            return stack;
        }

        private void AddToIndex(UIWindow window)
        {
            if (!_byName.TryGetValue(window.Descriptor.Name, out List<UIWindow>? list))
            {
                list = new List<UIWindow>();
                _byName[window.Descriptor.Name] = list;
            }

            list.Add(window);
        }

        private IEnumerable<UILayer> SortedLayersDescending() =>
            _layers.Keys.OrderByDescending(static layer => layer);
    }
}
