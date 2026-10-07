using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lyking.GameFramework.Client.Services.Network
{
    /// <summary>连接状态。</summary>
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
    }

    /// <summary>
    /// 传输抽象：面向连接的字节流收发。数据到达通过 Pump 在主线程投递到 MessageReceived，
    /// 由帧循环驱动；真实实现（TCP/KCP/WebSocket）由平台层提供。
    /// </summary>
    public interface ITransport
    {
        ConnectionState State { get; }

        event Action? Connected;

        /// <summary>连接断开事件，携带原因；主动 Close 与远端断开都会触发。</summary>
        event Action<string>? Disconnected;

        /// <summary>数据到达事件，Pump 时在主线程触发。</summary>
        event Action<ReadOnlyMemory<byte>>? MessageReceived;

        ValueTask ConnectAsync(CancellationToken cancellationToken);

        /// <summary>主动关闭；不会触发自动重连策略（由上层区分主动与异常断开）。</summary>
        void Close(string reason);

        ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

        /// <summary>泵：把传输内部排队的到达数据投递到 MessageReceived。</summary>
        void Pump();
    }
}
