using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.Client.Services.Network
{
    /// <summary>
    /// 消息路由：按消息 id 注册处理器并分发。
    /// 未注册的消息 id 走 OnUnhandled；处理器异常由调用方（NetworkService 帧隔离）兜底。
    /// </summary>
    public sealed class MessageRouter
    {
        private readonly Dictionary<int, Action<ReadOnlyMemory<byte>>> _handlers =
            new Dictionary<int, Action<ReadOnlyMemory<byte>>>();

        /// <summary>收到未注册消息 id 时触发；未订阅则未处理消息被静默丢弃。</summary>
        public event Action<int, ReadOnlyMemory<byte>>? OnUnhandled;

        public void Register(int messageId, Action<ReadOnlyMemory<byte>> handler)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_handlers.TryAdd(messageId, handler))
            {
                throw new InvalidOperationException($"消息 id {messageId} 的处理器已注册。");
            }
        }

        public bool Unregister(int messageId) => _handlers.Remove(messageId);

        /// <summary>分发一条已解码消息。</summary>
        public void Dispatch(int messageId, ReadOnlyMemory<byte> body)
        {
            if (_handlers.TryGetValue(messageId, out Action<ReadOnlyMemory<byte>>? handler))
            {
                handler(body);
                return;
            }

            OnUnhandled?.Invoke(messageId, body);
        }

        /// <summary>已注册消息数量，诊断用。</summary>
        public int RegisteredCount => _handlers.Count;
    }
}
