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
            GameplayTagContainer? blockedTags = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("能力 ID 不能为空。", nameof(id));
            }

            if (cost < 0 || cooldownSeconds < 0 || damage < 0)
            {
                throw new ArgumentOutOfRangeException("能力成本、冷却和伤害不能为负数。");
            }

            Id = id;
            Cost = cost;
            CooldownSeconds = cooldownSeconds;
            Damage = damage;
            CostAttribute = costAttribute;
            DamageAttribute = damageAttribute;
            RequiredTags = requiredTags?.Clone() ?? new GameplayTagContainer();
            BlockedTags = blockedTags?.Clone() ?? new GameplayTagContainer();
        }

        public string Id { get; }

        public double Cost { get; }

        public double CooldownSeconds { get; }

        public double Damage { get; }

        public string CostAttribute { get; }

        public string DamageAttribute { get; }

        public GameplayTagContainer RequiredTags { get; }

        public GameplayTagContainer BlockedTags { get; }
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
