using System.Collections.Generic;
using System;
using System.Linq;
using Xunit;

namespace Lyking.GameplayAbilities.Core.Tests;

public sealed class ActiveGameplayEffectTests
{
    [Fact]
    public void 句柄只接受正数并支持相等比较()
    {
        var valid = new ActiveGameplayEffectHandle(1);

        Assert.True(valid.IsValid);
        Assert.Equal(valid, new ActiveGameplayEffectHandle(1));
        Assert.NotEqual(valid, new ActiveGameplayEffectHandle(2));
        Assert.False(default(ActiveGameplayEffectHandle).IsValid);
    }

    [Fact]
    public void 无限效果使用显式工厂且不写入非有限持续时间()
    {
        var spec = GameplayEffectSpec.CreateInfinite(
            new Dictionary<string, double> { ["Health"] = -5 });

        Assert.True(spec.IsInfinite);
        Assert.Equal(0, spec.DurationSeconds);
    }

    [Fact]
    public void 应用结果保留失败原因和无效句柄()
    {
        var result = GameplayEffectApplicationResult.Failed(
            GameplayEffectFailureReason.ApplicationInProgress);

        Assert.False(result.Succeeded);
        Assert.Equal(GameplayEffectFailureReason.ApplicationInProgress, result.Reason);
        Assert.False(result.Handle.IsValid);
    }

    [Fact]
    public void 基础值和有效值分离且移除贡献不覆盖基础值变化()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", 100);

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -20 },
                durationSeconds: 5),
            "Burn");

        Assert.True(result.Succeeded);
        Assert.Equal(80, asc.Attributes.Get("Health"));
        Assert.Equal(100, asc.Attributes.GetBase("Health"));

        asc.Attributes.Set("Health", 90);
        Assert.Equal(70, asc.Attributes.Get("Health"));
        clock.Advance(5);
        Assert.Equal(90, asc.Attributes.Get("Health"));
    }

    [Fact]
    public void 任一活动贡献溢出时不写入任何属性()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", double.MaxValue);

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(new Dictionary<string, double>
            {
                ["Health"] = double.MaxValue,
                ["Mana"] = double.MaxValue,
            }, durationSeconds: 5),
            "Overflow");

        Assert.False(result.Succeeded);
        Assert.Equal(GameplayEffectFailureReason.InvalidValue, result.Reason);
        Assert.Equal(double.MaxValue, asc.Attributes.GetBase("Health"));
        Assert.Equal(double.MaxValue, asc.Attributes.Get("Health"));
        Assert.Equal(0, asc.Attributes.Get("Mana"));
        Assert.Empty(asc.GetActiveEffects());
    }

    [Fact]
    public void 极值贡献移除失败时保持有效值和贡献账本原子性()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", double.MaxValue);

        var negative = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -double.MaxValue },
                durationSeconds: 5),
            "Negative");
        var positive = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = double.MaxValue },
                durationSeconds: 5),
            "Positive");

        Assert.True(negative.Succeeded);
        Assert.True(positive.Succeeded);
        Assert.Equal(double.MaxValue, asc.Attributes.Get("Health"));

        var events = new List<GameplayEvent>();
        asc.EventPublished += events.Add;

        Assert.False(asc.TryRemoveEffect(negative.Handle));

        Assert.Equal(double.MaxValue, asc.Attributes.Get("Health"));
        var activeHandles = asc.GetActiveEffects().Select(effect => effect.Handle).ToArray();
        Assert.Equal(2, activeHandles.Length);
        Assert.Contains(negative.Handle, activeHandles);
        Assert.Contains(positive.Handle, activeHandles);
        Assert.DoesNotContain(events, item =>
            item.Type == GameplayEventType.ActiveEffectRemoved
            || item.Type == GameplayEventType.AttributeChanged
            || item.Type == GameplayEventType.TagChanged);
    }

    [Fact]
    public void 活动标签按句柄计数且不会误删静态标签()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.OwnedTags.Add("State.Ready");

        var first = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -1 },
                grantedTags: Tags("State.Burning"),
                durationSeconds: 5),
            "A");
        var second = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -1 },
                grantedTags: Tags("State.Burning"),
                durationSeconds: 5),
            "B");

        Assert.True(first.Succeeded && second.Succeeded);
        Assert.True(asc.HasTag("State.Burning"));
        Assert.True(asc.TryRemoveEffect(first.Handle));
        Assert.True(asc.HasTag("State.Burning"));
        Assert.True(asc.TryRemoveEffect(second.Handle));
        Assert.False(asc.HasTag("State.Burning"));
        Assert.True(asc.HasTag("State.Ready"));
    }

    [Fact]
    public void 持续效果到期只移除自身贡献()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        var events = new List<GameplayEvent>();
        asc.Attributes.Set("Health", 100);
        asc.EventPublished += events.Add;

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -20 },
                grantedTags: Tags("State.Burning"),
                durationSeconds: 5),
            "Burn");

        Assert.True(result.Succeeded);
        Assert.Equal(80, asc.Attributes.Get("Health"));
        Assert.Equal(
            new[]
            {
                GameplayEventType.ActiveEffectApplied,
                GameplayEventType.AttributeChanged,
                GameplayEventType.TagChanged,
            },
            events.Select(item => item.Type).ToArray());
        Assert.Equal(result.Handle.Id, events[0].EffectHandleId);
        Assert.Equal(result.Handle.Id, events[1].EffectHandleId);
        Assert.Equal("State.Burning", events[2].TagName);

        events.Clear();
        asc.Attributes.Set("Health", 90);
        Assert.Equal(70, asc.Attributes.Get("Health"));
        events.Clear();

        clock.Advance(5);

        Assert.Equal(90, asc.Attributes.Get("Health"));
        Assert.False(asc.HasTag("State.Burning"));
        Assert.Empty(asc.GetActiveEffects());
        Assert.Equal(
            new[]
            {
                GameplayEventType.ActiveEffectExpired,
                GameplayEventType.AttributeChanged,
                GameplayEventType.TagChanged,
            },
            events.Select(item => item.Type).ToArray());
    }

    [Fact]
    public void 零增量活动效果不发布属性变化事件()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", 100);
        var events = new List<GameplayEvent>();
        asc.EventPublished += events.Add;

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = 0 },
                durationSeconds: 5),
            "NoOp");

        Assert.True(result.Succeeded);
        Assert.Equal(100, asc.Attributes.Get("Health"));
        Assert.Equal(new[] { GameplayEventType.ActiveEffectApplied }, events.Select(item => item.Type).ToArray());
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.AttributeChanged);

        events.Clear();
        clock.Advance(5);

        Assert.Equal(100, asc.Attributes.Get("Health"));
        Assert.Equal(new[] { GameplayEventType.ActiveEffectExpired }, events.Select(item => item.Type).ToArray());
        Assert.DoesNotContain(events, item => item.Type == GameplayEventType.AttributeChanged);
    }

    [Fact]
    public void 无限效果只能显式移除且重复移除幂等()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", 100);

        var result = asc.TryApplyEffect(
            GameplayEffectSpec.CreateInfinite(
                new Dictionary<string, double> { ["Health"] = -10 }),
            "Aura");

        Assert.True(result.Succeeded);
        Assert.Single(asc.GetActiveEffects());
        clock.Advance(100);
        Assert.Equal(90, asc.Attributes.Get("Health"));
        Assert.True(asc.TryRemoveEffect(result.Handle));
        Assert.False(asc.TryRemoveEffect(result.Handle));
        Assert.Equal(100, asc.Attributes.Get("Health"));
    }

    [Fact]
    public void 调度失败时不留下活动效果或贡献()
    {
        var asc = new AbilitySystemComponent(new ThrowingScheduleClock());
        asc.Attributes.Set("Health", 100);

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(new Dictionary<string, double> { ["Health"] = -10 }, durationSeconds: 5),
            "Broken");

        Assert.False(result.Succeeded);
        Assert.Equal(GameplayEffectFailureReason.SchedulingFailed, result.Reason);
        Assert.Equal(100, asc.Attributes.Get("Health"));
        Assert.Empty(asc.GetActiveEffects());
    }

    [Fact]
    public void 活动效果事件回调中的递归应用被拒绝()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        var nested = default(GameplayEffectApplicationResult);
        bool attempted = false;
        int appliedCount = 0;
        asc.EventPublished += gameplayEvent =>
        {
            if (gameplayEvent.Type != GameplayEventType.ActiveEffectApplied)
            {
                return;
            }

            appliedCount++;
            if (!attempted)
            {
                attempted = true;
                nested = asc.TryApplyEffect(
                    new GameplayEffectSpec(new Dictionary<string, double> { ["Health"] = -1 }, durationSeconds: 1),
                    "Nested");
            }
        };

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(new Dictionary<string, double> { ["Health"] = -1 }, durationSeconds: 1),
            "Root");

        Assert.True(result.Succeeded);
        Assert.False(nested.Succeeded);
        Assert.Equal(GameplayEffectFailureReason.ApplicationInProgress, nested.Reason);
        Assert.Equal(1, appliedCount);
        Assert.Single(asc.GetActiveEffects());
    }

    [Fact]
    public void 活动效果应用事件回调中的移除被拒绝且生命周期保持一致()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        asc.Attributes.Set("Health", 100);
        var events = new List<GameplayEvent>();
        bool attempted = false;
        bool removalResult = true;
        ActiveGameplayEffectHandle attemptedHandle = default;
        asc.EventPublished += gameplayEvent =>
        {
            events.Add(gameplayEvent);
            if (gameplayEvent.Type != GameplayEventType.ActiveEffectApplied)
            {
                return;
            }

            attempted = true;
            Assert.True(gameplayEvent.EffectHandleId.HasValue);
            attemptedHandle = asc.GetActiveEffects().Single().Handle;
            Assert.Equal(gameplayEvent.EffectHandleId.Value, attemptedHandle.Id);
            removalResult = asc.TryRemoveEffect(attemptedHandle);
        };

        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -10 },
                grantedTags: Tags("State.Burning"),
                durationSeconds: 5),
            "Burn");

        Assert.True(result.Succeeded);
        Assert.True(attempted);
        Assert.False(removalResult);
        Assert.Equal(result.Handle, attemptedHandle);
        Assert.Equal(
            new[]
            {
                GameplayEventType.ActiveEffectApplied,
                GameplayEventType.AttributeChanged,
                GameplayEventType.TagChanged,
            },
            events.Select(item => item.Type).ToArray());
        Assert.Equal(90, asc.Attributes.Get("Health"));
        Assert.True(asc.HasTag("State.Burning"));
        Assert.Single(asc.GetActiveEffects());
    }

    [Fact]
    public void 句柄不能跨组件移除同编号效果()
    {
        var clock = new ManualGameplayClock();
        var first = new AbilitySystemComponent(clock);
        var second = new AbilitySystemComponent(clock);
        var firstResult = first.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -1 },
                durationSeconds: 5),
            "First");
        var secondResult = second.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double> { ["Health"] = -2 },
                durationSeconds: 5),
            "Second");

        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);
        Assert.False(second.TryRemoveEffect(firstResult.Handle));
        Assert.Single(second.GetActiveEffects());
        Assert.Equal(secondResult.Handle, second.GetActiveEffects().Single().Handle);
    }

    [Fact]
    public void 纯标签效果可以独立经历生命周期()
    {
        var clock = new ManualGameplayClock();
        var asc = new AbilitySystemComponent(clock);
        var result = asc.TryApplyEffect(
            new GameplayEffectSpec(
                new Dictionary<string, double>(),
                grantedTags: Tags("State.Stunned"),
                durationSeconds: 2),
            "Stun");

        Assert.True(result.Succeeded);
        Assert.True(asc.HasTag("State.Stunned"));
        Assert.Single(asc.GetActiveEffects());

        clock.Advance(2);

        Assert.False(asc.HasTag("State.Stunned"));
        Assert.Empty(asc.GetActiveEffects());
    }

    private sealed class ThrowingScheduleClock : IGameplayClock
    {
        public double Now => 0;

        public int Schedule(double delaySeconds, Action callback)
        {
            throw new ArgumentOutOfRangeException(nameof(delaySeconds), "模拟调度参数失败");
        }

        public void Cancel(int timerId)
        {
        }
    }

    private static GameplayTagContainer Tags(string tag)
    {
        var tags = new GameplayTagContainer();
        tags.Add(tag);
        return tags;
    }
}
