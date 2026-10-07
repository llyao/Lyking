using Lyking.GameFramework.Client.Core.Events;
using Lyking.GameFramework.Client.Core.Logging;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class EventBusTests
{
    private readonly List<string> _logs = new();

    private ILogger Logger => new Logger("test", new DelegateLogSink((_, message) => _logs.Add(message)));

    private sealed record ChatMessage(string Text);

    [Fact]
    public void 订阅者按顺序收到事件()
    {
        var bus = new EventBus();
        var received = new List<string>();
        using var s1 = bus.Subscribe<ChatMessage>(m => received.Add("1:" + m.Text));
        using var s2 = bus.Subscribe<ChatMessage>(m => received.Add("2:" + m.Text));

        bus.Publish(new ChatMessage("你好"));

        Assert.Equal(new[] { "1:你好", "2:你好" }, received);
    }

    [Fact]
    public void 退订后不再收到事件且退订幂等()
    {
        var bus = new EventBus();
        int count = 0;
        var handle = bus.Subscribe<ChatMessage>(_ => count++);

        handle.Dispose();
        handle.Dispose();
        bus.Publish(new ChatMessage("a"));

        Assert.Equal(0, count);
    }

    [Fact]
    public void 不同事件类型互不串扰()
    {
        var bus = new EventBus();
        int chat = 0, join = 0;
        using var _ = bus.Subscribe<ChatMessage>(_ => chat++);
        using var __ = bus.Subscribe<int>(_ => join++);

        bus.Publish(new ChatMessage("a"));

        Assert.Equal(1, chat);
        Assert.Equal(0, join);
    }

    [Fact]
    public void 无订阅者发布不抛异常()
    {
        var bus = new EventBus();
        bus.Publish(new ChatMessage("无人听"));
        bus.Publish(42);
    }

    [Fact]
    public void 处理器异常默认聚合抛出且全部处理器执行完毕()
    {
        var bus = new EventBus();
        int secondRan = 0;
        using var _ = bus.Subscribe<ChatMessage>(_ => throw new InvalidOperationException("坏处理器"));
        using var __ = bus.Subscribe<ChatMessage>(_ => secondRan++);

        Assert.Throws<AggregateException>(() => bus.Publish(new ChatMessage("x")));
        Assert.Equal(1, secondRan);
    }

    [Fact]
    public void 订阅异常钩子后异常被隔离上报()
    {
        var bus = new EventBus();
        var faults = new List<Exception>();
        bus.HandlerFaulted += faults.Add;
        using var _ = bus.Subscribe<ChatMessage>(_ => throw new InvalidOperationException("坏处理器"));

        bus.Publish(new ChatMessage("x"));

        Assert.Single(faults);
        Assert.IsType<InvalidOperationException>(faults[0]);
    }

    [Fact]
    public void 构造注入日志时异常写日志不抛出()
    {
        var bus = new EventBus(Logger);
        using var _ = bus.Subscribe<ChatMessage>(_ => throw new InvalidOperationException("坏处理器"));

        bus.Publish(new ChatMessage("x"));

        Assert.Contains(_logs, m => m.Contains("ChatMessage"));
    }
}
