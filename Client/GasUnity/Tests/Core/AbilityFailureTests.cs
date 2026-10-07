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
    public void 成功施法为成本和目标属性各发布一次变更事件()
    {
        var clock = new ManualGameplayClock();
        var caster = new AbilitySystemComponent(clock);
        var target = new AbilitySystemComponent(clock);
        var events = new List<(string Source, GameplayEvent Event)>();
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));
        caster.EventPublished += gameplayEvent => events.Add(("caster", gameplayEvent));
        target.EventPublished += gameplayEvent => events.Add(("target", gameplayEvent));

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                ("caster", GameplayEventType.AbilityActivated, "Fireball", (string?)null),
                ("caster", GameplayEventType.CostCommitted, "Fireball", "Mana"),
                ("caster", GameplayEventType.AttributeChanged, "Fireball", "Mana"),
                ("caster", GameplayEventType.EffectApplied, "Fireball", "Health"),
                ("target", GameplayEventType.AttributeChanged, "Fireball", "Health"),
                ("caster", GameplayEventType.CooldownStarted, "Fireball", (string?)null),
            },
            events.Select(item => (item.Source, item.Event.Type, item.Event.AbilityId, item.Event.AttributeName)).ToArray());
    }

    [Fact]
    public void 直接属性写入事件没有能力来源()
    {
        var component = new AbilitySystemComponent(new ManualGameplayClock());
        var events = new List<GameplayEvent>();
        component.EventPublished += events.Add;

        component.Attributes.Set("Mana", 50);

        GameplayEvent gameplayEvent = Assert.Single(events);
        Assert.Equal(GameplayEventType.AttributeChanged, gameplayEvent.Type);
        Assert.Equal(string.Empty, gameplayEvent.AbilityId);
    }

    [Fact]
    public void 事件回调不能递归激活零冷却能力()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.GrantAbility(new GameplayAbility("Free", cost: 0, cooldownSeconds: 0, damage: 0));
        var events = new List<GameplayEvent>();
        bool attemptedReentry = false;
        int activationEventCount = 0;
        AbilityActivationResult nestedResult = default;
        caster.EventPublished += events.Add;
        caster.EventPublished += gameplayEvent =>
        {
            if (gameplayEvent.Type != GameplayEventType.AbilityActivated)
            {
                return;
            }

            activationEventCount++;
            if (!attemptedReentry)
            {
                attemptedReentry = true;
                nestedResult = caster.TryActivate("Free", target);
            }
        };

        AbilityActivationResult result = caster.TryActivate("Free", target);

        Assert.True(result.Succeeded);
        Assert.False(nestedResult.Succeeded);
        Assert.Equal(AbilityFailureReason.ActivationInProgress, nestedResult.Reason);
        Assert.Equal(1, activationEventCount);
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.AbilityRejected);
        Assert.Equal(1, events.Count(item => item.Type == GameplayEventType.AbilityActivated));
    }

    [Fact]
    public void 火球路径拒绝带持续时间的效果且不提交状态()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        var events = new List<GameplayEvent>();
        var effect = new GameplayEffectSpec(
            new Dictionary<string, double> { ["Health"] = -25 },
            durationSeconds: 5);
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(new GameplayAbility(
            "Fireball",
            cost: 20,
            cooldownSeconds: 2,
            damage: 25,
            damageEffect: effect));
        caster.EventPublished += events.Add;

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.InvalidEffect, result.Reason);
        Assert.Equal(100, caster.Attributes.Get("Mana"));
        Assert.Equal(100, target.Attributes.Get("Health"));
        Assert.Equal(0, caster.GetCooldownRemaining("Fireball"));
        Assert.Single(events);
        Assert.Equal(GameplayEventType.AbilityRejected, events[0].Type);
        Assert.Equal(AbilityFailureReason.InvalidEffect, events[0].Reason);
    }

    [Fact]
    public void 火球路径拒绝授予标签的效果且不提交状态()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        var events = new List<GameplayEvent>();
        var grantedTags = new GameplayTagContainer();
        grantedTags.Add("State.Burning");
        var effect = new GameplayEffectSpec(
            new Dictionary<string, double> { ["Health"] = -25 },
            grantedTags: grantedTags);
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(new GameplayAbility(
            "Fireball",
            cost: 20,
            cooldownSeconds: 2,
            damage: 25,
            damageEffect: effect));
        caster.EventPublished += events.Add;

        AbilityActivationResult result = caster.TryActivate("Fireball", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.InvalidEffect, result.Reason);
        Assert.Equal(100, caster.Attributes.Get("Mana"));
        Assert.Equal(100, target.Attributes.Get("Health"));
        Assert.False(target.OwnedTags.HasTag("State.Burning"));
        Assert.Equal(0, caster.GetCooldownRemaining("Fireball"));
        Assert.Single(events);
        Assert.Equal(GameplayEventType.AbilityRejected, events[0].Type);
        Assert.Equal(AbilityFailureReason.InvalidEffect, events[0].Reason);
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
