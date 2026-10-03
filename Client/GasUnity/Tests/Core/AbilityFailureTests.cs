using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Lyking.GameplayAbilities.Core.Tests;

public sealed class AbilityFailureTests
{
    [Fact]
    public void 空能力ID返回未知能力且不抛异常()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());

        AbilityActivationResult result = caster.TryActivate(null!, target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.UnknownAbility, result.Reason);
    }

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
        var events = new List<GameplayEvent>();
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));
        caster.EventPublished += events.Add;

        AbilityActivationResult result = caster.TryActivate("Fireball", target);
        var types = events.Select(item => item.Type).ToArray();

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                GameplayEventType.AbilityActivated,
                GameplayEventType.CostCommitted,
                GameplayEventType.EffectApplied,
                GameplayEventType.AttributeChanged,
                GameplayEventType.CooldownStarted,
            },
            types);
    }

    [Fact]
    public void 目标属性数值溢出时拒绝且不提交任何状态()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        var events = new List<GameplayEvent>();
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", double.MinValue);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: double.MaxValue));
        caster.EventPublished += events.Add;

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.InvalidTarget, result.Reason);
        Assert.Equal(100, caster.Attributes.Get("Mana"));
        Assert.Equal(double.MinValue, target.Attributes.Get("Health"));
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.CostCommitted);
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.EffectApplied);
        AbilityActivationResult second = caster.TryActivate("Fireball", target);
        Assert.False(second.Succeeded);
        Assert.Equal(AbilityFailureReason.InvalidTarget, second.Reason);
    }

    [Fact]
    public void 属性和事件观察者抛异常时仍完成原子提交()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        target.Attributes.AttributeChanged += (_, _, _) => throw new InvalidOperationException("属性观察者失败");
        caster.EventPublished += _ => throw new InvalidOperationException("表现观察者失败");
        target.EventPublished += _ => throw new InvalidOperationException("目标表现观察者失败");
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.True(result.Succeeded);
        Assert.Equal(80, caster.Attributes.Get("Mana"));
        Assert.Equal(75, target.Attributes.Get("Health"));
        AbilityActivationResult second = caster.TryActivate("Fireball", target);
        Assert.False(second.Succeeded);
        Assert.Equal(AbilityFailureReason.Cooldown, second.Reason);
    }
}
