using System;

namespace Lyking.GameFramework.Client.Services.Network
{
    /// <summary>
    /// 消息编解码：把（消息 id，消息体）组织为线上字节，与反向拆包。
    /// 客户端第一版不定义正式协议格式（协议契约属 Share 领域）；
    /// 默认实现仅是"4 字节小端 id + 消息体"的内部演示约定，正式联调前替换实现即可。
    /// </summary>
    public interface IMessageCodec
    {
        byte[] Frame(int messageId, ReadOnlyMemory<byte> body);

        /// <summary>拆包；payload 不完整或格式不符时返回 false。</summary>
        bool TryUnframe(ReadOnlyMemory<byte> payload, out int messageId, out ReadOnlyMemory<byte> body);
    }

    /// <summary>默认演示编解码：[4 字节小端 id][消息体]。</summary>
    public sealed class PrefixMessageCodec : IMessageCodec
    {
        public byte[] Frame(int messageId, ReadOnlyMemory<byte> body)
        {
            byte[] payload = new byte[4 + body.Length];
            payload[0] = (byte)messageId;
            payload[1] = (byte)(messageId >> 8);
            payload[2] = (byte)(messageId >> 16);
            payload[3] = (byte)(messageId >> 24);
            body.Span.CopyTo(payload.AsSpan(4));
            return payload;
        }

        public bool TryUnframe(ReadOnlyMemory<byte> payload, out int messageId, out ReadOnlyMemory<byte> body)
        {
            if (payload.Length < 4)
            {
                messageId = 0;
                body = default;
                return false;
            }

            messageId = payload.Span[0]
                | (payload.Span[1] << 8)
                | (payload.Span[2] << 16)
                | (payload.Span[3] << 24);
            body = payload.Slice(4);
            return true;
        }
    }
}
