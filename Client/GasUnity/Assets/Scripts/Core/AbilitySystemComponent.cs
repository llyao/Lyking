using System;
using System.Collections.Generic;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class AbilitySystemComponent
    {
        private readonly IGameplayClock _clock;
        private readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _cooldownEnds = new Dictionary<string, double>(StringComparer.Ordinal);
        private bool _suppressAttributeEvents;

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

        public double GetCooldownRemaining(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new ArgumentException("能力 ID 不能为空。", nameof(abilityId));
            }

            return _cooldownEnds.TryGetValue(abilityId, out double cooldownEnd)
                ? Math.Max(0, cooldownEnd - _clock.Now)
                : 0;
        }

        public void GrantAbility(GameplayAbility ability)
        {
            if (ability is null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            _abilities[ability.Id] = ability;
        }

        public AbilityActivationResult TryActivate(string abilityId, AbilitySystemComponent? target)
        {
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

            if (_cooldownEnds.TryGetValue(ability.Id, out double cooldownEnd) && _clock.Now < cooldownEnd)
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
            if (damage < 0 || double.IsNaN(damage) || double.IsInfinity(damage))
            {
                return Reject(abilityId, AbilityFailureReason.InvalidTarget);
            }

            Publish(new GameplayEvent(GameplayEventType.AbilityActivated, ability.Id));
            _suppressAttributeEvents = true;
            try
            {
                Attributes.ApplyDelta(ability.CostAttribute, -ability.Cost);
            }
            finally
            {
                _suppressAttributeEvents = false;
            }

            Publish(new GameplayEvent(GameplayEventType.CostCommitted, ability.Id, attributeName: ability.CostAttribute));

            var effect = new GameplayEffectSpec(
                new Dictionary<string, double> { [ability.DamageAttribute] = -damage });
            foreach (KeyValuePair<string, double> modifier in effect.Modifiers)
            {
                target.ApplyExternalDelta(modifier.Key, modifier.Value);
            }

            Publish(new GameplayEvent(GameplayEventType.EffectApplied, ability.Id, attributeName: ability.DamageAttribute));
            target.Publish(new GameplayEvent(GameplayEventType.AttributeChanged, ability.Id, attributeName: ability.DamageAttribute));
            _cooldownEnds[ability.Id] = _clock.Now + ability.CooldownSeconds;
            Publish(new GameplayEvent(GameplayEventType.CooldownStarted, ability.Id));
            return new AbilityActivationResult(true, AbilityFailureReason.None);
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

        private AbilityActivationResult Reject(string abilityId, AbilityFailureReason reason)
        {
            Publish(new GameplayEvent(GameplayEventType.AbilityRejected, abilityId, reason));
            return new AbilityActivationResult(false, reason);
        }

        private void ApplyExternalDelta(string attributeName, double delta)
        {
            _suppressAttributeEvents = true;
            try
            {
                Attributes.ApplyDelta(attributeName, delta);
            }
            finally
            {
                _suppressAttributeEvents = false;
            }
        }

        private void Publish(GameplayEvent gameplayEvent)
        {
            Delegate[] handlers = EventPublished?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Action<GameplayEvent> handler in handlers)
            {
                try
                {
                    handler(gameplayEvent);
                }
                catch
                {
                    // 事件观察者不能改变能力提交结果。
                }
            }
        }

        private void OnAttributeChanged(string attributeName, double _, double __)
        {
            if (!_suppressAttributeEvents)
            {
                Publish(new GameplayEvent(GameplayEventType.AttributeChanged, string.Empty, attributeName: attributeName));
            }
        }
    }
}
