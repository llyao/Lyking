using Xunit;

namespace Lyking.GameplayAbilities.Core.Tests;

public sealed class GameplayValueTests
{
    [Fact]
    public void 标签支持父标签查询和移除()
    {
        var tags = new GameplayTagContainer();
        tags.Add("State.Stunned");

        Assert.True(tags.HasTag("State"));
        Assert.True(tags.HasTag("State.Stunned"));
        Assert.False(tags.HasTag("State.Root"));

        tags.Remove("State.Stunned");
        Assert.False(tags.HasTag("State"));
    }

    [Fact]
    public void 属性变更发布前后值并支持增量()
    {
        var attributes = new AttributeSet();
        (string Name, double Before, double After)? changed = null;
        attributes.AttributeChanged += (name, before, after) => changed = (name, before, after);

        attributes.Set("Mana", 100);
        attributes.ApplyDelta("Mana", -20);

        Assert.Equal(80, attributes.Get("Mana"));
        Assert.Equal(("Mana", 100, 80), changed);
    }

    [Fact]
    public void 手动时钟推进到期回调且不会重复调用()
    {
        var clock = new ManualGameplayClock();
        var callbackCount = 0;
        clock.Schedule(2, () => callbackCount++);

        clock.Advance(1.9);
        Assert.Equal(0, callbackCount);
        clock.Advance(0.1);
        Assert.Equal(1, callbackCount);
        clock.Advance(5);
        Assert.Equal(1, callbackCount);
    }
}
