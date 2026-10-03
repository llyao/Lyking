using Xunit;

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
}
