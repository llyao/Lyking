using Lyking.GameFramework.Client.Services.Input;
using Lyking.GameFramework.Client.Core.Dispatching;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class InputServiceTests
{
    [Fact]
    public void 设备无关映射由UI优先消费()
    {
        var dispatcher = new MainThreadDispatcher();
        var service = new InputService(dispatcher);
        service.Map("ui.confirm", "Confirm");
        service.Map("player.move", "Move");
        var consumer = new RecordingInputConsumer("ui.confirm");
        var dispatched = new List<InputActionContext>();
        service.ActionTriggered += dispatched.Add;

        InputSubmitResult consumed = service.Submit(new InputSample("keyboard", "Confirm", 1f), consumer);
        InputSubmitResult passed = service.Submit(new InputSample("gamepad", "Move", 0.75f), consumer);

        Assert.Equal(InputSubmitStatus.ConsumedByUi, consumed.Status);
        Assert.Equal(new[] { "ui.confirm", "player.move" }, consumer.Observed.Select(item => item.Action));
        Assert.Equal(InputSubmitStatus.Dispatched, passed.Status);
        Assert.Equal("player.move", Assert.Single(dispatched).Action);
        Assert.Equal(0.75f, dispatched[0].Value);
    }

    [Fact]
    public void 未绑定控制与非法输入被拒绝()
    {
        var service = new InputService(new MainThreadDispatcher());

        Assert.Equal(InputSubmitStatus.Unmapped, service.Submit(new InputSample("keyboard", "Unknown", 1f)).Status);
        Assert.Throws<ArgumentException>(() => service.Map(" ", "Confirm"));
    }

    [Fact]
    public async Task 后台线程不能直接提交输入()
    {
        var service = new InputService(new MainThreadDispatcher());
        service.Map("player.jump", "Jump");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(
            () => service.Submit(new InputSample("keyboard", "Jump", 1f))));
    }

    [Fact]
    public void 输入上下文按优先级切换并在弹出后恢复()
    {
        var service = new InputService(new MainThreadDispatcher());
        service.Map("gameplay", "game.confirm", "Confirm");
        service.Map("menu", "menu.confirm", "Confirm");
        var actions = new List<InputActionContext>();
        service.ActionTriggered += actions.Add;
        using IDisposable gameplay = service.PushContext("gameplay", priority: 10);
        service.Submit(new InputSample("keyboard", "Confirm", 1f));

        using (service.PushContext("menu", priority: 100))
        {
            service.Submit(new InputSample("keyboard", "Confirm", 1f));
        }

        service.Submit(new InputSample("keyboard", "Confirm", 1f));

        Assert.Equal(new[] { "game.confirm", "menu.confirm", "game.confirm" }, actions.Select(item => item.Action));
        Assert.Equal(new[] { "gameplay", "menu", "gameplay" }, actions.Select(item => item.ContextName));
    }

    [Fact]
    public void 可按名称弹出输入上下文并保护默认上下文()
    {
        var service = new InputService(new MainThreadDispatcher());
        IDisposable overlay = service.PushContext("overlay", priority: 10);

        Assert.Equal("overlay", service.PopContext());
        Assert.Equal("default", service.ActiveContextName);
        Assert.Null(service.PopContext());
        overlay.Dispose();
    }

    private sealed class RecordingInputConsumer : IInputConsumer
    {
        private readonly string _actionToConsume;

        public RecordingInputConsumer(string actionToConsume)
        {
            _actionToConsume = actionToConsume;
        }

        public List<InputActionContext> Observed { get; } = new List<InputActionContext>();

        public bool TryConsume(InputActionContext action)
        {
            Observed.Add(action);
            return action.Action == _actionToConsume;
        }
    }
}
