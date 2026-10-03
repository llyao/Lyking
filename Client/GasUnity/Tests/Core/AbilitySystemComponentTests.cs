using Xunit;
using System.Collections.Generic;

namespace Lyking.GameplayAbilities.Core.Tests;

public sealed class AbilitySystemComponentTests
{
    [Fact]
    public void 火球成功后扣除法力造成伤害并在冷却中拒绝()
    {
        var clock = new ManualGameplayClock();
        var caster = new AbilitySystemComponent(clock);
        var target = new AbilitySystemComponent(clock);
        caster.Attributes.Set("Mana", 100);
        caster.Attributes.Set("AttackPower", 25);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));

        var first = caster.TryActivate("Fireball", target);
        var second = caster.TryActivate("Fireball", target);

        Assert.True(first.Succeeded);
        Assert.Equal(80, caster.Attributes.Get("Mana"));
        Assert.Equal(75, target.Attributes.Get("Health"));
        Assert.False(second.Succeeded);
        Assert.Equal(80, caster.Attributes.Get("Mana"));
    }

    [Fact]
    public void 伤害使用施法者攻击力而不是能力静态伤害()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        caster.Attributes.Set("AttackPower", 40);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));

        Assert.True(caster.TryActivate("Fireball", target).Succeeded);
        Assert.Equal(60, target.Attributes.Get("Health"));
    }

    [Fact]
    public void 能力效果规格决定默认伤害属性和增量()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Shield", 100);
        var effect = new GameplayEffectSpec(new Dictionary<string, double>
        {
            ["Shield"] = -15,
        });
        caster.GrantAbility(new GameplayAbility(
            "Dispel",
            cost: 10,
            cooldownSeconds: 1,
            damage: 999,
            damageAttribute: "Shield",
            damageEffect: effect));

        AbilityActivationResult result = caster.TryActivate("Dispel", target);

        Assert.True(result.Succeeded);
        Assert.Equal(90, caster.Attributes.Get("Mana"));
        Assert.Equal(85, target.Attributes.Get("Shield"));
    }

    [Fact]
    public void 能力效果规格存在时攻击力仍覆盖默认增量()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        caster.Attributes.Set("Mana", 100);
        caster.Attributes.Set("AttackPower", 40);
        target.Attributes.Set("Shield", 100);
        var effect = new GameplayEffectSpec(new Dictionary<string, double>
        {
            ["Shield"] = -15,
        });
        caster.GrantAbility(new GameplayAbility(
            "Dispel",
            cost: 10,
            cooldownSeconds: 1,
            damage: 999,
            damageAttribute: "Shield",
            damageEffect: effect));

        Assert.True(caster.TryActivate("Dispel", target).Succeeded);
        Assert.Equal(60, target.Attributes.Get("Shield"));
    }

    [Fact]
    public void 多个必需标签必须全部满足()
    {
        var caster = new AbilitySystemComponent(new ManualGameplayClock());
        var target = new AbilitySystemComponent(new ManualGameplayClock());
        var required = new GameplayTagContainer();
        required.Add("State.Ready");
        required.Add("Weapon.Sword");
        caster.OwnedTags.Add("State.Ready");
        caster.Attributes.Set("Mana", 100);
        caster.GrantAbility(new GameplayAbility("Attack", 10, 1, 10, requiredTags: required));

        AbilityActivationResult result = caster.TryActivate("Attack", target);

        Assert.False(result.Succeeded);
        Assert.Equal(AbilityFailureReason.MissingRequiredTags, result.Reason);
        Assert.Equal(100, caster.Attributes.Get("Mana"));
    }

    [Fact]
    public void 冷却查询返回剩余时间并在结束后归零()
    {
        var clock = new ManualGameplayClock();
        var caster = new AbilitySystemComponent(clock);
        var target = new AbilitySystemComponent(clock);
        caster.Attributes.Set("Mana", 100);
        target.Attributes.Set("Health", 100);
        caster.GrantAbility(FireballAbility.Create(cost: 20, cooldown: 2, damage: 25));

        Assert.True(caster.TryActivate("Fireball", target).Succeeded);
        Assert.Equal(2, caster.GetCooldownRemaining("Fireball"));

        caster.AdvanceTime(0.75);
        Assert.Equal(1.25, caster.GetCooldownRemaining("Fireball"), precision: 10);

        caster.AdvanceTime(1.25);
        Assert.Equal(0, caster.GetCooldownRemaining("Fireball"));
        Assert.Equal(0, caster.GetCooldownRemaining("Unknown"));
    }
}
