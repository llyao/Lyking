using System;
using System.Collections.Generic;
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
    public void 默认标签哈希稳定且不抛异常()
    {
        GameplayTag empty = default;

        Assert.True(empty.IsEmpty);
        Assert.Equal(string.Empty, empty.Name);
        Assert.Equal(string.Empty, empty.ToString());
        Assert.Equal(empty.GetHashCode(), empty.GetHashCode());
    }

    [Fact]
    public void 空标签不参与层级匹配且不能加入容器()
    {
        var tags = new GameplayTagContainer();
        tags.Add("State.Stunned");
        GameplayTag empty = default;

        Assert.False(empty.IsParentOf(new GameplayTag("State.Stunned")));
        Assert.False(new GameplayTag("State").IsParentOf(empty));
        Assert.False(tags.HasTag(empty));
        Assert.Throws<ArgumentException>(() => tags.Add(empty));
    }

    [Theory]
    [InlineData(".State")]
    [InlineData("State.")]
    [InlineData("State..Stunned")]
    public void 标签层级不允许空段(string name)
    {
        Assert.Throws<ArgumentException>(() => new GameplayTag(name));
    }

    [Fact]
    public void 能力标签属性不会暴露可变内部状态()
    {
        var ability = new GameplayAbility(
            "Test",
            cost: 1,
            cooldownSeconds: 1,
            damage: 1,
            blockedTags: new GameplayTagContainer());

        ability.BlockedTags.Add("State.Stunned");

        Assert.False(ability.BlockedTags.HasTag("State.Stunned"));
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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void 手动时钟拒绝非有限的定时延迟(double delaySeconds)
    {
        var clock = new ManualGameplayClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Schedule(delaySeconds, () => { }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void 手动时钟拒绝非有限的推进时间(double seconds)
    {
        var clock = new ManualGameplayClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(seconds));
        Assert.Equal(0, clock.Now);
    }

    [Fact]
    public void 手动时钟推进结果溢出时拒绝并保持当前时间()
    {
        var clock = new ManualGameplayClock();
        clock.Advance(double.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.MaxValue));
        Assert.Equal(double.MaxValue, clock.Now);
    }

    [Fact]
    public void 手动时钟定时点溢出时拒绝并保持当前时间()
    {
        var clock = new ManualGameplayClock();
        clock.Advance(double.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Schedule(double.MaxValue, () => { }));
        Assert.Equal(double.MaxValue, clock.Now);
    }

    [Fact]
    public void 效果规格复制修改器并拒绝非有限值()
    {
        var source = new Dictionary<string, double> { ["Health"] = -25 };
        var spec = new GameplayEffectSpec(source);
        source["Health"] = -99;

        Assert.Equal(-25, spec.Modifiers["Health"]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GameplayEffectSpec(new Dictionary<string, double> { ["Health"] = double.NaN }));
    }
}
