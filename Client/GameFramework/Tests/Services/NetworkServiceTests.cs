using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Network;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class NetworkServiceTests
{
    /// <summary>连接 N 次后开始失败的传输，用于重连放弃测试。</summary>
    private sealed class LimitedConnectTransport : ITransport
    {
        private readonly int _allowedConnects;

        public LimitedConnectTransport(int allowedConnects)
        {
            _allowedConnects = allowedConnects;
        }

        public int ConnectCount { get; private set; }

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action? Connected;
        public event Action<string>? Disconnected;

#pragma warning disable CS0067 // 测试桩不产生到达数据
        public event Action<ReadOnlyMemory<byte>>? MessageReceived;
#pragma warning restore CS0067

        public ValueTask ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCount++;
            if (ConnectCount > _allowedConnects)
            {
                throw new InvalidOperationException("连接被拒绝（测试桩）。");
            }

            State = ConnectionState.Connected;
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
            Disconnected?.Invoke(reason);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => default;

        public void Pump()
        {
        }
    }

    private static ModuleEnvironment CreateEnvironment() => new(
        new ServiceRegistry(),
        new Logger("test", new DelegateLogSink((_, _) => { })));

    [Fact]
    public async Task 环回传输收发与消息分发()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        var bodies = new List<string>();
        network.Router.Register(1, body => bodies.Add(System.Text.Encoding.UTF8.GetString(body.Span)));

        await network.ConnectAsync();
        await network.SendAsync(1, System.Text.Encoding.UTF8.GetBytes("hello"));
        transport.Deliver(new PrefixMessageCodec().Frame(1, System.Text.Encoding.UTF8.GetBytes("world")));
        network.Update(0.016);

        Assert.True(network.IsConnected);
        Assert.Equal(new[] { "hello" }, transport.Sent.Select(p => System.Text.Encoding.UTF8.GetString(p.Span)));
        Assert.Equal(new[] { "world" }, bodies);
    }

    [Fact]
    public async Task 未注册消息走OnUnhandled()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        var unhandled = new List<int>();
        network.Router.OnUnhandled += (id, _) => unhandled.Add(id);
        await network.ConnectAsync();

        transport.Deliver(new PrefixMessageCodec().Frame(99, new byte[] { 1 }));
        network.Update(0.016);

        Assert.Equal(new[] { 99 }, unhandled);
    }

    [Fact]
    public async Task 无法拆包的数据走UnhandledPayload()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        int badCount = 0;
        network.UnhandledPayload += _ => badCount++;
        await network.ConnectAsync();

        transport.Deliver(new byte[] { 1, 2 });
        network.Update(0.016);

        Assert.Equal(1, badCount);
    }

    [Fact]
    public async Task 未连接发送抛出()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => network.SendAsync(new byte[] { 1 }).AsTask());
    }

    [Fact]
    public async Task 空闲超时发送心跳()
    {
        var transport = new LoopbackTransport();
        var options = new NetworkOptions { HeartbeatInterval = TimeSpan.FromSeconds(1) };
        var network = new NetworkService(transport, options: options);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        await network.ConnectAsync();

        network.Update(0.5);
        Assert.Empty(transport.Sent);

        network.Update(0.5);
        Assert.Single(transport.Sent);
        Assert.Equal(new byte[] { 0x00 }, transport.Sent[0].ToArray());
    }

    [Fact]
    public async Task 发送业务消息会刷新心跳计时()
    {
        var transport = new LoopbackTransport();
        var options = new NetworkOptions { HeartbeatInterval = TimeSpan.FromSeconds(1) };
        var network = new NetworkService(transport, options: options);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        await network.ConnectAsync();

        network.Update(0.6);
        await network.SendAsync(new byte[] { 1 });
        network.Update(0.6);

        // 1.2s 内有活动：只有业务消息，没有心跳。
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task 异常断开后按退避自动重连成功()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        await network.ConnectAsync();

        transport.SimulateRemoteDisconnect("远端踢下线");
        Assert.False(network.IsConnected);

        network.Update(0.5);
        Assert.False(network.IsConnected);

        network.Update(1.0);
        Assert.True(network.IsConnected);
        Assert.Equal(0, network.ReconnectAttempts);
    }

    [Fact]
    public async Task 主动关闭不触发重连()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        await network.ConnectAsync();

        network.Close();
        network.Update(10.0);

        Assert.Equal(ConnectionState.Disconnected, network.State);
        Assert.Equal(0, network.ReconnectAttempts);
    }

    [Fact]
    public async Task 重连达到上限后放弃并只触发一次()
    {
        var transport = new LimitedConnectTransport(allowedConnects: 1);
        var network = new NetworkService(transport, options: new NetworkOptions
        {
            HeartbeatInterval = TimeSpan.Zero,
            ReconnectMaxAttempts = 2,
            ReconnectBaseDelay = TimeSpan.FromSeconds(1),
        });
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        await network.ConnectAsync();

        int gaveUp = 0;
        network.ReconnectGaveUp += _ => gaveUp++;
        transport.Close("远端断开");

        // 尝试 1：1s 后失败；尝试 2：2s 后失败；之后放弃。
        network.Update(1.0);
        network.Update(2.0);
        network.Update(4.0);
        network.Update(8.0);

        Assert.Equal(1, gaveUp);
        Assert.Equal(3, transport.ConnectCount);
        Assert.Equal(ConnectionState.Disconnected, network.State);
    }

    [Fact]
    public async Task 关闭时断开传输()
    {
        var transport = new LoopbackTransport();
        var network = new NetworkService(transport);
        await network.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        var reasons = new List<string>();
        transport.Disconnected += reasons.Add;
        await network.ConnectAsync();

        await network.ShutdownAsync(CancellationToken.None);

        Assert.Equal(ConnectionState.Disconnected, transport.State);
        Assert.Contains("客户端关闭", reasons);
    }
}
