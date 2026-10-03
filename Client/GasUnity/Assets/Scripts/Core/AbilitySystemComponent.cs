using System;
using System.Collections.Generic;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class AbilitySystemComponent
    {
        private readonly IGameplayClock _clock;
        private readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _cooldownEnds = new Dictionary<string, double>(StringComparer.Ordinal);

        public AbilitySystemComponent(IGameplayClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Attributes = new AttributeSet();
            OwnedTags = new GameplayTagContainer();
            Attributes.AttributeChanged += OnAttributeChanged;
        }

        public AttributeSet Attributes { get; }

        public GameplayTagContainer OwnedTags { get; }

        public event Action<GameplayEvent>? EventPublished;

        public void GrantAbility(GameplayAbility ability)
        {
            if (ability is null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            _abilities[ability.Id] = ability;
        }

        public AbilityActivationResult TryActivate(string? abilityId, AbilitySystemComponent? target)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return Reject(abilityId, AbilityFailureReason.UnknownAbility);
            }

            if (!_abilities.TryGetValue(abilityId, out GameplayAbility? ability))
            {
                return Reject(abilityId, AbilityFailureReason.UnknownAbility);
            }

            if (target is null)
            {
                return Reject(abilityId, AbilityFailureReason.MissingTarget);
            }

            if (OwnedTags.HasAny(ability.BlockedTags))
            {
                return Reject(abilityId, AbilityFailureReason.BlockedByTags);
            }

            if (!HasRequiredTags(ability.RequiredTags))
            {
                return Reject(abilityId, AbilityFailureReason.MissingRequiredTags);
            }

            if (_cooldownEnds.TryGetValue(ability.Id, out double activeCooldownEnd) && _clock.Now < activeCooldownEnd)
            {
                return Reject(abilityId, AbilityFailureReason.Cooldown);
            }

            if (Attributes.Get(ability.CostAttribute) < ability.Cost)
            {
                return Reject(abilityId, AbilityFailureReason.InsufficientResource);
            }

            double damage = Attributes.Contains("AttackPower")
                ? Attributes.Get("AttackPower")
                : ability.Damage;
            if (!IsFinite(damage) || damage < 0)
            {
                return Reject(abilityId, AbilityFailureReason.InvalidTarget);
            }

            string costAttribute = ability.CostAttribute;
            string damageAttribute = ability.DamageAttribute;
            double costBefore = Attributes.Get(costAttribute);
            double targetBefore = target.Attributes.Get(damageAttribute);
            bool sharesAttribute = ReferenceEquals(Attributes, target.Attributes)
                && string.Equals(costAttribute, damageAttribute, StringComparison.Ordinal);
            double costAfter;
            double targetAfter;

            if (sharesAttribute)
            {
                if (!TryApplyDelta(costBefore, -ability.Cost - damage, out costAfter))
                {
                    return Reject(abilityId, AbilityFailureReason.InvalidTarget);
                }

                targetAfter = costAfter;
            }
            else
            {
                if (!TryApplyDelta(costBefore, -ability.Cost, out costAfter)
                    || !TryApplyDelta(targetBefore, -damage, out targetAfter))
                {
                    return Reject(abilityId, AbilityFailureReason.InvalidTarget);
                }
            }

            double cooldownEnd = _clock.Now + ability.CooldownSeconds;
            if (!IsFinite(cooldownEnd))
            {
                return Reject(abilityId, AbilityFailureReason.InvalidTarget);
            }

            bool hadCooldown = _cooldownEnds.TryGetValue(ability.Id, out double previousCooldownEnd);
            bool sourceCommitted = false;
            bool targetCommitted = false;
            try
            {
                // 先锁定冷却，再提交属性，防止观察者重入造成重复施法。
                _cooldownEnds[ability.Id] = cooldownEnd;
                if (sharesAttribute)
                {
                    Attributes.SetSilently(costAttribute, costAfter);
                    sourceCommitted = true;
                    targetCommitted = true;
                }
                else
                {
                    Attributes.SetSilently(costAttribute, costAfter);
                    sourceCommitted = true;
                    target.Attributes.SetSilently(damageAttribute, targetAfter);
                    targetCommitted = true;
                }
            }
            catch
            {
                if (targetCommitted)
                {
                    target.Attributes.SetSilently(damageAttribute, targetBefore);
                }

                if (sourceCommitted)
                {
                    Attributes.SetSilently(costAttribute, costBefore);
                }

                if (hadCooldown)
                {
                    _cooldownEnds[ability.Id] = previousCooldownEnd;
                }
                else
                {
                    _cooldownEnds.Remove(ability.Id);
                }

                return Reject(abilityId, AbilityFailureReason.InvalidTarget);
            }

            Publish(new GameplayEvent(GameplayEventType.AbilityActivated, ability.Id));
            Publish(new GameplayEvent(GameplayEventType.CostCommitted, ability.Id, attributeName: costAttribute));
            Publish(new GameplayEvent(GameplayEventType.EffectApplied, ability.Id, attributeName: damageAttribute));
            if (targetBefore != targetAfter)
            {
                target.Attributes.NotifyChanged(damageAttribute, targetBefore, targetAfter);
            }

            Publish(new GameplayEvent(GameplayEventType.AttributeChanged, ability.Id, attributeName: damageAttribute));
            Publish(new GameplayEvent(GameplayEventType.CooldownStarted, ability.Id));
            return new AbilityActivationResult(true, AbilityFailureReason.None);
        }

        public double GetCooldownRemaining(string? abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId)
                || !_cooldownEnds.TryGetValue(abilityId, out double cooldownEnd))
            {
                return 0;
            }

            double remaining = cooldownEnd - _clock.Now;
            return IsFinite(remaining) && remaining > 0 ? remaining : 0;
        }

        public void AdvanceTime(double seconds)
        {
            if (_clock is ManualGameplayClock manualClock)
            {
                manualClock.Advance(seconds);
                return;
            }

            throw new InvalidOperationException("当前时钟不支持手动推进。");
        }

        private bool HasRequiredTags(GameplayTagContainer requiredTags)
        {
            return requiredTags.Count == 0 || OwnedTags.HasAll(requiredTags);
        }

        private AbilityActivationResult Reject(string? abilityId, AbilityFailureReason reason)
        {
            Publish(new GameplayEvent(GameplayEventType.AbilityRejected, abilityId ?? string.Empty, reason));
            return new AbilityActivationResult(false, reason);
        }

        private void OnAttributeChanged(string attributeName, double _, double __)
        {
            Publish(new GameplayEvent(GameplayEventType.AttributeChanged, string.Empty, attributeName: attributeName));
        }

        private void Publish(GameplayEvent gameplayEvent)
        {
            Delegate[] handlers = EventPublished?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Delegate handler in handlers)
            {
                try
                {
                    ((Action<GameplayEvent>)handler)(gameplayEvent);
                }
                catch
                {
                    // 表现层观察者失败不能回滚已经提交的玩法状态。
                }
            }
        }

        private static bool TryApplyDelta(double current, double delta, out double result)
        {
            result = current + delta;
            return IsFinite(current) && IsFinite(delta) && IsFinite(result);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
