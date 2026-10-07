using System;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

namespace Lyking.GameFramework.Client.Services.Network
{
    /// <summary>网络服务选项。</summary>
    public sealed class NetworkOptions
    {
        /// <summary>心跳间隔；TimeSpan.Zero 表示禁用心跳。</summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>心跳载荷；默认单字节 0x00，正式协议接入后替换。</summary>
        public ReadOnlyMemory<byte> HeartbeatPayload { get; set; } = new byte[] { 0x00 };

        /// <summary>异常断开后的自动重连尝试上限；0 表示禁用自动重连。</summary>
        public int ReconnectMaxAttempts { get; set; } = 3;

        /// <summary>重连基础延迟，按 2 的幂退避。</summary>
        public TimeSpan ReconnectBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>重连延迟上限。</summary>
        public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 网络服务：组装传输、编解码与消息路由，附加心跳保活与异常断开自动重连（指数退避）。
    /// 心跳与重连时刻都在自身 Update 内按帧时间推进。主动 Close 不触发重连；
    /// 重连达到上限后触发 ReconnectGaveUp，需要人工调用 ConnectAsync 恢复。
    /// </summary>
    public sealed class NetworkService : ClientModule
    {
        private readonly ITransport _transport;
        private readonly IMessageCodec _codec;
        private readonly NetworkOptions _options;
        private double _uptime;
        private double _lastActivity;
        private double _reconnectDueAt = double.MinValue;
        private int _reconnectAttempts;
        private bool _userClosed;
        private bool _reconnectGaveUp;

        public NetworkService(
            ITransport transport,
            IMessageCodec? codec = null,
            NetworkOptions? options = null)
            : base("Network", priority: 100)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _codec = codec ?? new PrefixMessageCodec();
            _options = options ?? new NetworkOptions();
            Router = new MessageRouter();
        }

        public MessageRouter Router { get; }

        public ITransport Transport => _transport;

        public ConnectionState State => _transport.State;

        public bool IsConnected => State == ConnectionState.Connected;

        /// <summary>已执行的自动重连尝试次数（每次连接成功后清零）。</summary>
        public int ReconnectAttempts => _reconnectAttempts;

        public event Action? Connected;

        public event Action<string>? Disconnected;

        /// <summary>收到无法拆包的数据时触发（未注册 id 走 Router.OnUnhandled）。</summary>
        public event Action<ReadOnlyMemory<byte>>? UnhandledPayload;

        /// <summary>自动重连达到上限时触发。</summary>
        public event Action<int>? ReconnectGaveUp;

        public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
        {
            _transport.Connected += OnTransportConnected;
            _transport.Disconnected += OnTransportDisconnected;
            _transport.MessageReceived += OnMessageReceived;
            return base.InitializeAsync(environment, cancellationToken);
        }

        public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
        {
            _transport.Connected -= OnTransportConnected;
            _transport.Disconnected -= OnTransportDisconnected;
            _transport.MessageReceived -= OnMessageReceived;
            if (_transport.State != ConnectionState.Disconnected)
            {
                _transport.Close("客户端关闭");
            }

            return base.ShutdownAsync(cancellationToken);
        }

        /// <summary>建立连接；重置重连计数与主动关闭标记。</summary>
        public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            _userClosed = false;
            _reconnectGaveUp = false;
            _reconnectAttempts = 0;
            _reconnectDueAt = double.MinValue;
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _lastActivity = _uptime;
        }

        /// <summary>主动关闭：不触发自动重连。</summary>
        public void Close()
        {
            _userClosed = true;
            _reconnectDueAt = double.MinValue;
            _transport.Close("主动关闭");
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            _lastActivity = _uptime;
            return _transport.SendAsync(payload, cancellationToken);
        }

        /// <summary>按（消息 id + 消息体）发送，经编解码组帧。</summary>
        public ValueTask SendAsync(int messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
            => SendAsync(_codec.Frame(messageId, body), cancellationToken);

        public override void Update(double deltaTime)
        {
            _uptime += deltaTime;
            _transport.Pump();

            if (IsConnected)
            {
                SendHeartbeatIfDue();
                return;
            }

            TryReconnectIfDue();
        }

        private void SendHeartbeatIfDue()
        {
            if (_options.HeartbeatInterval <= TimeSpan.Zero)
            {
                return;
            }

            double intervalSeconds = _options.HeartbeatInterval.TotalSeconds;
            if (_uptime - _lastActivity < intervalSeconds)
            {
                return;
            }

            Log.Debug("连接空闲超时，发送心跳。");
            _ = SendAsync(_options.HeartbeatPayload);
        }

        private void TryReconnectIfDue()
        {
            if (_userClosed || _reconnectGaveUp || _options.ReconnectMaxAttempts <= 0 || _uptime < _reconnectDueAt)
            {
                return;
            }

            if (_reconnectAttempts >= _options.ReconnectMaxAttempts)
            {
                _reconnectGaveUp = true;
                _reconnectDueAt = double.MinValue;
                ReconnectGaveUp?.Invoke(_reconnectAttempts);
                Log.Error($"自动重连已达上限 {_options.ReconnectMaxAttempts} 次，停止重连。");
                return;
            }

            double delay = ComputeBackoffDelay(_reconnectAttempts);
            // TryReconnectIfDue 每帧都会在到期后进入：先推进下次尝试时刻，再发起连接。
            _reconnectDueAt = _uptime + delay;
            _reconnectAttempts++;
            Log.Warn($"第 {_reconnectAttempts}/{_options.ReconnectMaxAttempts} 次自动重连。");
            _ = AttemptReconnectAsync();
        }

        private double ComputeBackoffDelay(int attempts)
        {
            double seconds = _options.ReconnectBaseDelay.TotalSeconds * Math.Pow(2, attempts);
            return Math.Min(seconds, _options.ReconnectMaxDelay.TotalSeconds);
        }

        private async Task AttemptReconnectAsync()
        {
            try
            {
                await _transport.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error("自动重连失败。", ex);
            }
        }

        private void OnTransportConnected()
        {
            _reconnectAttempts = 0;
            _reconnectDueAt = double.MinValue;
            _reconnectGaveUp = false;
            _lastActivity = _uptime;
            Log.Info("网络连接已建立。");
            Connected?.Invoke();
        }

        private void OnTransportDisconnected(string reason)
        {
            Log.Warn($"网络连接断开：{reason}");
            Disconnected?.Invoke(reason);
            if (_userClosed || _options.ReconnectMaxAttempts <= 0)
            {
                return;
            }

            _reconnectDueAt = _uptime + ComputeBackoffDelay(_reconnectAttempts);
        }

        private void OnMessageReceived(ReadOnlyMemory<byte> payload)
        {
            _lastActivity = _uptime;
            if (_codec.TryUnframe(payload, out int messageId, out ReadOnlyMemory<byte> body))
            {
                Router.Dispatch(messageId, body);
                return;
            }

            Log.Warn($"收到无法拆包的数据（{payload.Length} 字节）。");
            UnhandledPayload?.Invoke(payload);
        }
    }
}
