#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class AbilitySystemComponent
    {
        private readonly IGameplayClock _clock;
        private readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _cooldownEnds = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<ActiveGameplayEffectHandle, ActiveGameplayEffectState> _activeEffects =
            new Dictionary<ActiveGameplayEffectHandle, ActiveGameplayEffectState>();
        private readonly Dictionary<GameplayTag, HashSet<ActiveGameplayEffectHandle>> _activeTagSources =
            new Dictionary<GameplayTag, HashSet<ActiveGameplayEffectHandle>>();
        private readonly Guid _effectOwnerToken = Guid.NewGuid();
        private int _nextEffectHandleId;
        private bool _activationInProgress;
        private bool _effectApplicationInProgress;
        private bool _effectLifecycleEventInProgress;

        private sealed class ActiveGameplayEffectState
        {
            public ActiveGameplayEffectState(ActiveGameplayEffect snapshot)
            {
                Snapshot = snapshot;
            }

            public ActiveGameplayEffect Snapshot { get; }

            public int TimerId { get; set; }

            public bool HasTimer { get; set; }
        }

        public AbilitySystemComponent(IGameplayClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Attributes = new AttributeSet();
            OwnedTags = new GameplayTagContainer();
            Attributes.GameplayAttributeChanged += OnAttributeChanged;
        }

        public AttributeSet Attributes { get; }

        public GameplayTagContainer OwnedTags { get; }

        public event Action<GameplayEvent>? EventPublished;

        public GameplayEffectApplicationResult TryApplyEffect(
            GameplayEffectSpec? spec,
            string? sourceAbilityId)
        {
            if (_effectApplicationInProgress || _effectLifecycleEventInProgress)
            {
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.ApplicationInProgress);
            }

            _effectApplicationInProgress = true;
            try
            {
                return TryApplyEffectCore(spec, sourceAbilityId);
            }
            finally
            {
                _effectApplicationInProgress = false;
            }
        }

        public bool TryRemoveEffect(ActiveGameplayEffectHandle handle)
        {
            if (_effectLifecycleEventInProgress || !handle.BelongsTo(_effectOwnerToken))
            {
                return false;
            }

            return RemoveEffectInternal(handle, expired: false);
        }

        public IReadOnlyList<ActiveGameplayEffect> GetActiveEffects()
        {
            return _activeEffects.Values
                .Select(state => state.Snapshot)
                .OrderBy(effect => effect.Handle.Id)
                .ToArray();
        }

        public bool HasTag(GameplayTag tag)
        {
            if (tag.IsEmpty)
            {
                return false;
            }

            if (OwnedTags.HasTag(tag))
            {
                return true;
            }

            return _activeTagSources.Keys.Any(owned => owned.Equals(tag) || tag.IsParentOf(owned));
        }

        public bool HasTag(string tag) => HasTag(new GameplayTag(tag));

        public bool HasAny(GameplayTagContainer tags)
        {
            if (tags is null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            return tags.Enumerate().Any(HasTag);
        }

        public bool HasAll(GameplayTagContainer tags)
        {
            if (tags is null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            return tags.Enumerate().All(HasTag);
        }

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
            if (_activationInProgress)
            {
                // 同步事件回调中的递归请求不再二次发布事件。
                return new AbilityActivationResult(false, AbilityFailureReason.ActivationInProgress);
            }

            _activationInProgress = true;
            try
            {
                return TryActivateCore(abilityId, target);
            }
            finally
            {
                _activationInProgress = false;
            }
        }

        private AbilityActivationResult TryActivateCore(string? abilityId, AbilitySystemComponent? target)
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

            if (HasAny(ability.BlockedTags))
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

            double configuredDamage = ability.Damage;
            if (ability.DamageEffect is not null)
            {
                if (ability.DamageEffect.IsInfinite
                    || ability.DamageEffect.DurationSeconds != 0
                    || ability.DamageEffect.GrantedTags.Count != 0)
                {
                    return Reject(abilityId, AbilityFailureReason.InvalidEffect);
                }

                if (ability.DamageEffect.Modifiers.Count != 1
                    || !ability.DamageEffect.Modifiers.TryGetValue(ability.DamageAttribute, out double effectDelta)
                    || effectDelta >= 0)
                {
                    return Reject(abilityId, AbilityFailureReason.InvalidEffect);
                }

                configuredDamage = -effectDelta;
            }

            double damage = Attributes.Contains("AttackPower")
                ? Attributes.Get("AttackPower")
                : configuredDamage;
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
            if (!sharesAttribute && costBefore != costAfter)
            {
                Attributes.NotifyChanged(costAttribute, costBefore, costAfter, ability.Id);
            }

            Publish(new GameplayEvent(GameplayEventType.EffectApplied, ability.Id, attributeName: damageAttribute));
            if (targetBefore != targetAfter)
            {
                if (sharesAttribute)
                {
                    Attributes.NotifyChanged(damageAttribute, targetBefore, targetAfter, ability.Id);
                }
                else
                {
                    target.Attributes.NotifyChanged(damageAttribute, targetBefore, targetAfter, ability.Id);
                }
            }

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
            return requiredTags.Count == 0 || HasAll(requiredTags);
        }

        private AbilityActivationResult Reject(string? abilityId, AbilityFailureReason reason)
        {
            Publish(new GameplayEvent(GameplayEventType.AbilityRejected, abilityId ?? string.Empty, reason));
            return new AbilityActivationResult(false, reason);
        }

        private void OnAttributeChanged(
            string attributeName,
            double _,
            double __,
            string? sourceAbilityId,
            int? effectHandleId,
            double? timestamp)
        {
            Publish(new GameplayEvent(
                GameplayEventType.AttributeChanged,
                sourceAbilityId ?? string.Empty,
                attributeName: attributeName,
                effectHandleId: effectHandleId,
                timestamp: timestamp));
        }

        private GameplayEffectApplicationResult TryApplyEffectCore(
            GameplayEffectSpec? spec,
            string? sourceAbilityId)
        {
            if (spec is null
                || (spec.Modifiers.Count == 0 && spec.GrantedTags.Count == 0)
                || (!spec.IsInfinite && spec.DurationSeconds <= 0))
            {
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidSpec);
            }

            double startTime = _clock.Now;
            if (!IsFinite(startTime))
            {
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidValue);
            }

            double? endTime = null;
            if (!spec.IsInfinite)
            {
                double candidateEnd = startTime + spec.DurationSeconds;
                if (!IsFinite(candidateEnd))
                {
                    return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidValue);
                }

                endTime = candidateEnd;
            }

            if (_nextEffectHandleId == int.MaxValue)
            {
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidValue);
            }

            ActiveGameplayEffectHandle handle = new ActiveGameplayEffectHandle(++_nextEffectHandleId, _effectOwnerToken);
            bool contributionAdded = false;
            bool stateAdded = false;
            var addedTags = new List<GameplayTag>();
            var changedTags = new List<GameplayTag>();
            ActiveGameplayEffectState? state = null;
            try
            {
                if (!Attributes.TryAddContribution(handle, spec.Modifiers, out IReadOnlyList<AttributeSet.AttributeChange> changes))
                {
                    return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidValue);
                }

                contributionAdded = true;
                GameplayTagContainer grantedTags = spec.GrantedTags;
                foreach (GameplayTag tag in grantedTags.Enumerate().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    bool wasActive = HasTag(tag);
                    AddActiveTag(handle, tag);
                    addedTags.Add(tag);
                    if (!wasActive && HasTag(tag))
                    {
                        changedTags.Add(tag);
                    }
                }

                var snapshot = new ActiveGameplayEffect(handle, sourceAbilityId, spec, startTime, endTime);
                state = new ActiveGameplayEffectState(snapshot);
                _activeEffects.Add(handle, state);
                stateAdded = true;
                if (!spec.IsInfinite)
                {
                    try
                    {
                        state.TimerId = _clock.Schedule(spec.DurationSeconds, () => RemoveEffectInternal(handle, expired: true));
                    }
                    catch
                    {
                        RollbackEffect(handle, state, stateAdded, contributionAdded, addedTags);
                        return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.SchedulingFailed);
                    }

                    state.HasTimer = true;
                }

                PublishEffectLifecycle(() =>
                {
                    Publish(new GameplayEvent(
                        GameplayEventType.ActiveEffectApplied,
                        sourceAbilityId ?? string.Empty,
                        effectHandleId: handle.Id,
                        timestamp: startTime));
                    foreach (AttributeSet.AttributeChange change in changes)
                    {
                        Attributes.NotifyChanged(
                            change.Name,
                            change.Before,
                            change.After,
                            sourceAbilityId,
                            handle.Id,
                            startTime);
                    }

                    foreach (GameplayTag tag in changedTags)
                    {
                        Publish(new GameplayEvent(
                            GameplayEventType.TagChanged,
                            sourceAbilityId ?? string.Empty,
                            effectHandleId: handle.Id,
                            timestamp: startTime,
                            tagName: tag.Name));
                    }
                });

                return GameplayEffectApplicationResult.SucceededWith(handle);
            }
            catch (ArgumentOutOfRangeException)
            {
                RollbackEffect(handle, state, stateAdded, contributionAdded, addedTags);
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidValue);
            }
            catch (ArgumentException)
            {
                RollbackEffect(handle, state, stateAdded, contributionAdded, addedTags);
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.InvalidAttribute);
            }
            catch
            {
                RollbackEffect(handle, state, stateAdded, contributionAdded, addedTags);
                return GameplayEffectApplicationResult.Failed(GameplayEffectFailureReason.SchedulingFailed);
            }
        }

        private bool RemoveEffectInternal(ActiveGameplayEffectHandle handle, bool expired)
        {
            if (!handle.BelongsTo(_effectOwnerToken)
                || !_activeEffects.TryGetValue(handle, out ActiveGameplayEffectState? state))
            {
                return false;
            }

            if (!Attributes.TryRemoveContribution(handle, out IReadOnlyList<AttributeSet.AttributeChange> changes))
            {
                return false;
            }

            if (state.HasTimer && !expired)
            {
                _clock.Cancel(state.TimerId);
            }

            _activeEffects.Remove(handle);
            var changedTags = new List<GameplayTag>();
            foreach (GameplayTag tag in state.Snapshot.Spec.GrantedTags.Enumerate().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                bool wasActive = HasTag(tag);
                RemoveActiveTag(handle, tag);
                if (wasActive && !HasTag(tag))
                {
                    changedTags.Add(tag);
                }
            }

            double timestamp = _clock.Now;
            PublishEffectLifecycle(() =>
            {
                Publish(new GameplayEvent(
                    expired ? GameplayEventType.ActiveEffectExpired : GameplayEventType.ActiveEffectRemoved,
                    state.Snapshot.SourceAbilityId ?? string.Empty,
                    effectHandleId: handle.Id,
                    timestamp: timestamp));
                foreach (AttributeSet.AttributeChange change in changes)
                {
                    Attributes.NotifyChanged(
                        change.Name,
                        change.Before,
                        change.After,
                        state.Snapshot.SourceAbilityId,
                        handle.Id,
                        timestamp);
                }

                foreach (GameplayTag tag in changedTags)
                {
                    Publish(new GameplayEvent(
                        GameplayEventType.TagChanged,
                        state.Snapshot.SourceAbilityId ?? string.Empty,
                        effectHandleId: handle.Id,
                        timestamp: timestamp,
                        tagName: tag.Name));
                }
            });

            return true;
        }

        private void RollbackEffect(
            ActiveGameplayEffectHandle handle,
            ActiveGameplayEffectState? state,
            bool stateAdded,
            bool contributionAdded,
            IEnumerable<GameplayTag> addedTags)
        {
            if (stateAdded && state is not null)
            {
                if (state.HasTimer)
                {
                    _clock.Cancel(state.TimerId);
                }

                _activeEffects.Remove(handle);
            }

            if (contributionAdded)
            {
                Attributes.TryRemoveContribution(handle, out _);
            }

            foreach (GameplayTag tag in addedTags)
            {
                RemoveActiveTag(handle, tag);
            }
        }

        private void AddActiveTag(ActiveGameplayEffectHandle handle, GameplayTag tag)
        {
            if (!_activeTagSources.TryGetValue(tag, out HashSet<ActiveGameplayEffectHandle>? sources))
            {
                sources = new HashSet<ActiveGameplayEffectHandle>();
                _activeTagSources.Add(tag, sources);
            }

            sources.Add(handle);
        }

        private void RemoveActiveTag(ActiveGameplayEffectHandle handle, GameplayTag tag)
        {
            if (!_activeTagSources.TryGetValue(tag, out HashSet<ActiveGameplayEffectHandle>? sources))
            {
                return;
            }

            sources.Remove(handle);
            if (sources.Count == 0)
            {
                _activeTagSources.Remove(tag);
            }
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

        private void PublishEffectLifecycle(Action publish)
        {
            bool previous = _effectLifecycleEventInProgress;
            _effectLifecycleEventInProgress = true;
            try
            {
                publish();
            }
            finally
            {
                _effectLifecycleEventInProgress = previous;
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
