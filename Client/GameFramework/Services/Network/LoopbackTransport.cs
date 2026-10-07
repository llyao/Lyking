using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;

namespace Lyking.GameFramework.Client.Services.Network
{
    /// <summary>
    /// 环回传输：连接即成功，Send 进入发送日志，Deliver 模拟服务器下发，
    /// Pump 把排队数据投递到 MessageReceived。用于纯逻辑测试与无头演示；
    /// 真实传输由平台层（TCP/KCP/WebSocket）实现同一 ITransport 合同。
    /// </summary>
    public sealed class LoopbackTransport : ITransport
    {
        private readonly Queue<ReadOnlyMemory<byte>> _incoming = new Queue<ReadOnlyMemory<byte>>();
        private readonly List<ReadOnlyMemory<byte>> _sent = new List<ReadOnlyMemory<byte>>();

        public LoopbackTransport(ILogger? logger = null)
        {
            Logger = logger;
        }

        private ILogger? Logger { get; }

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>本端已发送的载荷副本（测试断言用）。</summary>
        public IReadOnlyList<ReadOnlyMemory<byte>> Sent => _sent;

        public event Action? Connected;

        public event Action<string>? Disconnected;

        public event Action<ReadOnlyMemory<byte>>? MessageReceived;

        public ValueTask ConnectAsync(CancellationToken cancellationToken)
        {
            if (State == ConnectionState.Connected)
            {
                return default;
            }

            cancellationToken.ThrowIfCancellationRequested();
            State = ConnectionState.Connected;
            Logger?.Info("环回传输已连接。");
            Connected?.Invoke();
            return default;
        }

        public void Close(string reason)
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            State = ConnectionState.Disconnected;
            _incoming.Clear();
            Logger?.Warn($"环回传输关闭：{reason}");
            Disconnected?.Invoke(reason);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            if (State != ConnectionState.Connected)
            {
                throw new InvalidOperationException("环回传输未连接，无法发送。");
            }

            _sent.Add(payload.ToArray());
            return default;
        }

        public void Pump()
        {
            while (_incoming.Count > 0)
            {
                ReadOnlyMemory<byte> payload = _incoming.Dequeue();
                MessageReceived?.Invoke(payload);
            }
        }

        /// <summary>模拟服务器下发数据；未连接时排队的数据会在 Pump 前被丢弃（Close 清空）。</summary>
        public void Deliver(ReadOnlyMemory<byte> payload)
        {
            if (State != ConnectionState.Connected)
            {
                throw new InvalidOperationException("环回传输未连接，无法下发数据。");
            }

            _incoming.Enqueue(payload);
        }

        /// <summary>模拟远端异常断开（区别于本端 Close）：不清空排队数据，供重连测试。</summary>
        public void SimulateRemoteDisconnect(string reason)
        {
            if (State != ConnectionState.Connected)
            {
                return;
            }

            State = ConnectionState.Disconnected;
            Disconnected?.Invoke(reason);
        }
    }
}
