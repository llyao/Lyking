using Lyking.GameFramework.Client.Core.Pooling;
using Lyking.GameFramework.Client.Core.StateMachines;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class ObjectPoolTests
{
    [Fact]
    public void 空池创建新对象()
    {
        var pool = new ObjectPool<object>(() => new object());

        var item = pool.Rent();

        Assert.NotNull(item);
        Assert.Equal(1, pool.CreatedCount);
        Assert.Equal(0, pool.PooledCount);
    }

    [Fact]
    public void 归还后复用同一实例()
    {
        var pool = new ObjectPool<object>(() => new object());
        var item = pool.Rent();
        pool.Return(item);

        var again = pool.Rent();

        Assert.Same(item, again);
        Assert.Equal(1, pool.CreatedCount);
    }

    [Fact]
    public void 超出上限的归还直接丢弃()
    {
        var pool = new ObjectPool<object>(() => new object(), maxPooled: 1);
        var a = pool.Rent();
        var b = pool.Rent();
        pool.Return(a);
        pool.Return(b);

        Assert.Equal(1, pool.PooledCount);
    }

    [Fact]
    public void 钩子按借出与归还顺序触发()
    {
        var events = new List<string>();
        var pool = new ObjectPool<object>(
            () => new object(),
            onRent: _ => events.Add("rent"),
            onReturn: _ => events.Add("return"));

        var item = pool.Rent();
        pool.Return(item);

        Assert.Equal(new[] { "rent", "return" }, events);
    }

    [Fact]
    public void 归还null抛出()
    {
        var pool = new ObjectPool<object>(() => new object());
        Assert.Throws<ArgumentNullException>(() => pool.Return(null!));
    }
}

public sealed class StateMachineTests
{
    private enum Phase
    {
        Idle,
        Loading,
        Playing,
    }

    [Fact]
    public void 初始状态与合法迁移()
    {
        var machine = new StateMachine<Phase>(Phase.Idle);
        machine.Permit(Phase.Idle, Phase.Loading);
        machine.Permit(Phase.Loading, Phase.Playing);

        machine.TransitionTo(Phase.Loading);
        machine.TransitionTo(Phase.Playing);

        Assert.Equal(Phase.Playing, machine.Current);
        Assert.Equal(2, machine.TransitionCount);
    }

    [Fact]
    public void 非法迁移抛出()
    {
        var machine = new StateMachine<Phase>(Phase.Idle);
        machine.Permit(Phase.Idle, Phase.Loading);

        Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(Phase.Playing));
    }

    [Fact]
    public void 相同状态迁移被忽略()
    {
        var machine = new StateMachine<Phase>(Phase.Idle);
        machine.Permit(Phase.Idle, Phase.Idle);

        machine.TransitionTo(Phase.Idle);

        Assert.Equal(0, machine.TransitionCount);
    }

    [Fact]
    public void 先退出旧状态再进入新状态()
    {
        var calls = new List<string>();
        var machine = new StateMachine<Phase>(
            Phase.Idle,
            onEnter: s => calls.Add("enter:" + s),
            onExit: s => calls.Add("exit:" + s));
        machine.Permit(Phase.Idle, Phase.Loading);

        machine.TransitionTo(Phase.Loading);

        Assert.Equal(new[] { "exit:Idle", "enter:Loading" }, calls);
    }

    [Fact]
    public void 迁移事件携带新旧状态()
    {
        var transitions = new List<(Phase From, Phase To)>();
        var machine = new StateMachine<Phase>(Phase.Idle);
        machine.Permit(Phase.Idle, Phase.Loading);
        machine.Transitioned += (from, to) => transitions.Add((from, to));

        machine.TransitionTo(Phase.Loading);

        Assert.Equal(new[] { (Phase.Idle, Phase.Loading) }, transitions);
    }

    [Fact]
    public void CanTransition按当前状态判断()
    {
        var machine = new StateMachine<Phase>(Phase.Idle);
        machine.Permit(Phase.Idle, Phase.Loading);

        Assert.True(machine.CanTransition(Phase.Loading));
        Assert.False(machine.CanTransition(Phase.Playing));
    }
}
