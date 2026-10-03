using System;
using System.Collections.Generic;
using Xunit;

namespace Lyking.GameplayAbilities.Core.Tests;

public sealed class AbilityFailureTests
{
    [Fact]
    public void 法力不足时拒绝且不发布成本或效果事件()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        var events = new List<GameplayEvent>();
        caster.Attributes.Set("Mana", 10);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));
        caster.EventPublished += events.Add;

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.InsufficientResource, result.Reason);
        Assert.Equal(10, caster.Attributes.Get("Mana"));
        Assert.Equal(100, target.Attributes.Get("Health"));
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.CostCommitted);
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.EffectApplied);
    }

    [Fact]
    public void 阻断标签拒绝能力()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        caster.OwnedTags.Add("State.Stunned");
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.BlockedByTags, result.Reason);
    }

    [Fact]
    public void 成功事件按激活成本效果属性冷却顺序发布()
    {
        var clock = new ManualGameplayClock();
        var caster = new AbilitySystemComponent(clock);
        var target = new AbilitySystemComponent(clock);
        var events = new List<string>();
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));
        caster.EventPublished += item => events.Add($"caster:{item.Type}");
        target.EventPublished += item => events.Add($"target:{item.Type}");

        AbilityActivationResult result = caster.TryActivate("Fireball", target);
        var types = events.ToArray();

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                "caster:AbilityActivated",
                "caster:CostCommitted",
                "caster:EffectApplied",
                "target:AttributeChanged",
                "caster:CooldownStarted",
            },
            types);
    }

    [Fact]
    public void 无效属性名在构造阶段拒绝()
    {
        Assert.Throws<ArgumentException>(() => new GameplayAbility("Bad", 20, 2, 10, damageAttribute: ""));
    }

    [Fact]
    public void 事件订阅者抛异常不会中断能力提交()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));
        caster.EventPublished += _ => throw new InvalidOperationException("观察者故障");

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.True(result.Succeeded);
        Assert.Equal(80, caster.Attributes.Get("Mana"));
        Assert.Equal(75, target.Attributes.Get("Health"));
        Assert.Equal(2, caster.GetCooldownRemaining("Fireball"));
    }
}
