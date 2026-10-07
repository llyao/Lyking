using System;

namespace Lyking.GameFramework.Client.Core.Events
{
    /// <summary>
    /// 类型化发布订阅总线。用于跨模块通信，替代模块之间的直接引用。
    /// 主线程使用为主；订阅与发布不保证线程安全。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>订阅事件，返回退订句柄；Dispose 幂等。</summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// 发布事件：按订阅顺序通知全部处理器。单个处理器异常被隔离，
        /// 收集后交由 <see cref="HandlerFaulted"/> 上报；若无人订阅该事件，
        /// 则在全部处理器执行完毕后抛出 <see cref="AggregateException"/>（快速失败）。
        /// </summary>
        void Publish<TEvent>(TEvent evt);

        /// <summary>处理器异常上报钩子。订阅后异常只上报不再抛出。</summary>
        event Action<Exception>? HandlerFaulted;
    }
}
