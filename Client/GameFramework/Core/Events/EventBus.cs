using System;
using System.Collections.Generic;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.Core.Events
{
    /// <summary>IEventBus 默认实现，见接口注释的异常隔离语义。</summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>可选：上报处理器异常到日志。与 HandlerFaulted 互补，两者都未使用时异常聚合抛出。</summary>
        public EventBus(ILogger? logger = null)
        {
            Logger = logger;
        }

        private ILogger? Logger { get; }

        public event Action<Exception>? HandlerFaulted;

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
            {
                list = new List<Delegate>();
                _handlers[typeof(TEvent)] = list;
            }

            list.Add(handler);
            return new Subscription(typeof(TEvent), handler, this);
        }

        public void Publish<TEvent>(TEvent evt)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list) || list.Count == 0)
            {
                return;
            }

            // 快照后遍历：处理器内的新订阅本次不触发，退订不影响本轮已快照的成员。
            Delegate[] snapshot = list.ToArray();
            List<Exception>? faults = null;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<TEvent>)snapshot[i])(evt);
                }
                catch (Exception ex)
                {
                    faults ??= new List<Exception>();
                    faults.Add(ex);
                }
            }

            if (faults == null)
            {
                return;
            }

            if (HandlerFaulted != null)
            {
                foreach (Exception fault in faults)
                {
                    HandlerFaulted.Invoke(fault);
                }

                return;
            }

            if (Logger != null)
            {
                foreach (Exception fault in faults)
                {
                    Logger.Error($"事件 {typeof(TEvent).Name} 的处理器抛出异常。", fault);
                }

                return;
            }

            throw new AggregateException($"事件 {typeof(TEvent).Name} 的处理器抛出异常。", faults);
        }

        private sealed class Subscription : IDisposable
        {
            private EventBus? _owner;
            private readonly Type _eventType;
            private readonly Delegate _handler;

            public Subscription(Type eventType, Delegate handler, EventBus owner)
            {
                _eventType = eventType;
                _handler = handler;
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                if (_owner._handlers.TryGetValue(_eventType, out List<Delegate>? list))
                {
                    list.Remove(_handler);
                }

                _owner = null;
            }
        }
    }
}
