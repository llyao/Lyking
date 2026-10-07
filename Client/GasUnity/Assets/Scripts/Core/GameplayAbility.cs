#nullable enable
using System;

namespace Lyking.GameplayAbilities.Core
{
    public enum AbilityFailureReason
    {
        None,
        UnknownAbility,
        MissingTarget,
        BlockedByTags,
        MissingRequiredTags,
        Cooldown,
        InsufficientResource,
        InvalidTarget,
        ActivationInProgress,
        InvalidEffect,
    }

    public readonly struct AbilityActivationResult
    {
        public AbilityActivationResult(bool succeeded, AbilityFailureReason reason)
        {
            Succeeded = succeeded;
            Reason = reason;
        }

        public bool Succeeded { get; }

        public AbilityFailureReason Reason { get; }
    }

    public sealed class GameplayAbility
    {
        public GameplayAbility(
            string id,
            double cost,
            double cooldownSeconds,
            double damage,
            string costAttribute = "Mana",
            string damageAttribute = "Health",
            GameplayTagContainer? requiredTags = null,
            GameplayTagContainer? blockedTags = null,
            GameplayEffectSpec? damageEffect = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("能力 ID 不能为空。", nameof(id));
            }

            if (!IsFinite(cost) || !IsFinite(cooldownSeconds) || !IsFinite(damage)
                || cost < 0 || cooldownSeconds < 0 || damage < 0)
            {
                throw new ArgumentOutOfRangeException("能力成本、冷却和伤害不能为负数。");
            }

            if (string.IsNullOrWhiteSpace(costAttribute))
            {
                throw new ArgumentException("成本属性名称不能为空。", nameof(costAttribute));
            }

            if (string.IsNullOrWhiteSpace(damageAttribute))
            {
                throw new ArgumentException("伤害属性名称不能为空。", nameof(damageAttribute));
            }

            Id = id;
            Cost = cost;
            CooldownSeconds = cooldownSeconds;
            Damage = damage;
            CostAttribute = costAttribute;
            DamageAttribute = damageAttribute;
            DamageEffect = damageEffect;
            _requiredTags = requiredTags?.Clone() ?? new GameplayTagContainer();
            _blockedTags = blockedTags?.Clone() ?? new GameplayTagContainer();
        }

        public string Id { get; }

        public double Cost { get; }

        public double CooldownSeconds { get; }

        public double Damage { get; }

        public string CostAttribute { get; }

        public string DamageAttribute { get; }

        /// <summary>
        /// 可选的伤害效果规格。规格只描述目标属性和默认增量，运行时可由攻击力覆盖增量。
        /// </summary>
        public GameplayEffectSpec? DamageEffect { get; }

        private readonly GameplayTagContainer _requiredTags;

        private readonly GameplayTagContainer _blockedTags;

        public GameplayTagContainer RequiredTags => _requiredTags.Clone();

        public GameplayTagContainer BlockedTags => _blockedTags.Clone();

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public static class FireballAbility
    {
        public static GameplayAbility Create(double cost, double cooldown, double damage)
        {
            var blockedTags = new GameplayTagContainer();
            blockedTags.Add("State.Stunned");
            return new GameplayAbility(
                "Fireball",
                cost,
                cooldown,
                damage,
                blockedTags: blockedTags);
        }
    }
}
