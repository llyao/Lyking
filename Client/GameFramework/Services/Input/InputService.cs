using System;
using System.Collections.Generic;
using Lyking.GameFramework.Client.Core.Dispatching;

namespace Lyking.GameFramework.Client.Services.Input
{
    /// <summary>与设备无关的逻辑输入采样。</summary>
    public sealed class InputSample
    {
        public InputSample(string device, string logicalControl, float value)
        {
            Device = RequireText(device, nameof(device));
            LogicalControl = RequireText(logicalControl, nameof(logicalControl));
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = Math.Max(-1f, Math.Min(1f, value));
        }

        public string Device { get; }
        public string LogicalControl { get; }
        public float Value { get; }

        private static string RequireText(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", name) : value;
    }

    public sealed class InputActionContext
    {
        public InputActionContext(
            string action,
            string logicalControl,
            float value,
            string device,
            string contextName = "default",
            int contextPriority = int.MinValue)
        {
            Action = action;
            LogicalControl = logicalControl;
            Value = value;
            Device = device;
            ContextName = contextName;
            ContextPriority = contextPriority;
        }

        public string Action { get; }
        public string LogicalControl { get; }
        public float Value { get; }
        public string Device { get; }
        public string ContextName { get; }
        public int ContextPriority { get; }
    }

    public interface IInputConsumer
    {
        bool TryConsume(InputActionContext action);
    }

    public enum InputSubmitStatus
    {
        Dispatched,
        ConsumedByUi,
        Unmapped,
    }

    public sealed class InputSubmitResult
    {
        public InputSubmitResult(InputSubmitStatus status)
        {
            Status = status;
        }

        public InputSubmitStatus Status { get; }
    }

    public sealed class InputContextScope : IDisposable
    {
        private Action? _pop;

        internal InputContextScope(Action pop)
        {
            _pop = pop ?? throw new ArgumentNullException(nameof(pop));
        }

        public void Dispose()
        {
            System.Threading.Interlocked.Exchange(ref _pop, null)?.Invoke();
        }
    }

    /// <summary>把键鼠、手柄或触屏适配到逻辑控制；先让 UI 消费，再向游戏逻辑派发。</summary>
    public sealed class InputService
    {
        private sealed class ContextActivation
        {
            public ContextActivation(string name, int priority, int sequence)
            {
                Name = name;
                Priority = priority;
                Sequence = sequence;
            }

            public string Name { get; }
            public int Priority { get; }
            public int Sequence { get; }
        }

        private const string DefaultContext = "default";
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly Dictionary<string, Dictionary<string, string>> _actionsByContext =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private readonly List<ContextActivation> _activeContexts = new List<ContextActivation>();
        private int _nextContextSequence;

        public InputService(IMainThreadDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _actionsByContext.Add(DefaultContext, new Dictionary<string, string>(StringComparer.Ordinal));
            _activeContexts.Add(new ContextActivation(DefaultContext, int.MinValue, _nextContextSequence++));
        }

        public event Action<InputActionContext>? ActionTriggered;

        public string ActiveContextName
        {
            get
            {
                EnsureMainThread();
                return GetTopContext().Name;
            }
        }

        public void Map(string action, string logicalControl)
        {
            Map(DefaultContext, action, logicalControl);
        }

        public void Map(string contextName, string action, string logicalControl)
        {
            EnsureMainThread();
            RequireText(contextName, nameof(contextName));
            RequireText(action, nameof(action));
            RequireText(logicalControl, nameof(logicalControl));
            GetOrCreateContext(contextName)[logicalControl] = action;
        }

        public bool Unmap(string logicalControl)
        {
            return Unmap(DefaultContext, logicalControl);
        }

        public bool Unmap(string contextName, string logicalControl)
        {
            EnsureMainThread();
            RequireText(contextName, nameof(contextName));
            RequireText(logicalControl, nameof(logicalControl));
            return _actionsByContext.TryGetValue(contextName, out Dictionary<string, string>? actions)
                && actions.Remove(logicalControl);
        }

        public InputContextScope PushContext(string contextName, int priority = 0)
        {
            EnsureMainThread();
            RequireText(contextName, nameof(contextName));
            if (string.Equals(contextName, DefaultContext, StringComparison.Ordinal))
            {
                throw new ArgumentException("默认输入上下文不能重复压入。", nameof(contextName));
            }

            GetOrCreateContext(contextName);
            var activation = new ContextActivation(contextName, priority, _nextContextSequence++);
            _activeContexts.Add(activation);
            return new InputContextScope(() => PopActivation(activation));
        }

        public bool PopContext(string contextName)
        {
            EnsureMainThread();
            RequireText(contextName, nameof(contextName));
            ContextActivation? activation = null;
            foreach (ContextActivation active in _activeContexts)
            {
                if (active.Name == contextName && active.Name != DefaultContext
                    && (activation == null || active.Sequence > activation.Sequence))
                {
                    activation = active;
                }
            }

            if (activation == null)
            {
                return false;
            }

            _activeContexts.Remove(activation);
            return true;
        }

        public string? PopContext()
        {
            EnsureMainThread();
            ContextActivation? activation = null;
            foreach (ContextActivation active in _activeContexts)
            {
                if (active.Name != DefaultContext && (activation == null || active.Sequence > activation.Sequence))
                {
                    activation = active;
                }
            }

            if (activation == null)
            {
                return null;
            }

            _activeContexts.Remove(activation);
            return activation.Name;
        }

        public InputSubmitResult Submit(InputSample sample, IInputConsumer? uiConsumer = null)
        {
            EnsureMainThread();
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            ContextActivation? selectedContext = null;
            string? action = null;
            foreach (ContextActivation active in _activeContexts)
            {
                if (!_actionsByContext[active.Name].TryGetValue(sample.LogicalControl, out string? candidate))
                {
                    continue;
                }

                if (selectedContext == null
                    || active.Priority > selectedContext.Priority
                    || (active.Priority == selectedContext.Priority && active.Sequence > selectedContext.Sequence))
                {
                    selectedContext = active;
                    action = candidate;
                }
            }

            if (selectedContext == null || action == null)
            {
                return new InputSubmitResult(InputSubmitStatus.Unmapped);
            }

            var actionContext = new InputActionContext(
                action,
                sample.LogicalControl,
                sample.Value,
                sample.Device,
                selectedContext.Name,
                selectedContext.Priority);
            if (uiConsumer != null && uiConsumer.TryConsume(actionContext))
            {
                return new InputSubmitResult(InputSubmitStatus.ConsumedByUi);
            }

            ActionTriggered?.Invoke(actionContext);
            return new InputSubmitResult(InputSubmitStatus.Dispatched);
        }

        private ContextActivation GetTopContext()
        {
            ContextActivation top = _activeContexts[0];
            foreach (ContextActivation active in _activeContexts)
            {
                if (active.Priority > top.Priority
                    || (active.Priority == top.Priority && active.Sequence > top.Sequence))
                {
                    top = active;
                }
            }

            return top;
        }

        private Dictionary<string, string> GetOrCreateContext(string contextName)
        {
            if (!_actionsByContext.TryGetValue(contextName, out Dictionary<string, string>? actions))
            {
                actions = new Dictionary<string, string>(StringComparer.Ordinal);
                _actionsByContext.Add(contextName, actions);
            }

            return actions;
        }

        private void PopActivation(ContextActivation activation)
        {
            EnsureMainThread();
            _activeContexts.Remove(activation);
        }

        private void EnsureMainThread()
        {
            if (!_dispatcher.IsMainThread)
            {
                throw new InvalidOperationException("输入映射和动作派发必须在框架主线程执行。");
            }
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("值不能为空。", name);
            }
        }
    }
}
