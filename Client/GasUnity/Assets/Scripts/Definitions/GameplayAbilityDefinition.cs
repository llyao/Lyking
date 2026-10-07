#nullable enable
using System;
using Lyking.GameplayAbilities.Core;
using UnityEngine;

namespace Lyking.GameplayAbilities.Definitions
{
    [CreateAssetMenu(menuName = "Lyking/GAS/Gameplay Ability Definition")]
    public sealed class GameplayAbilityDefinition : ScriptableObject
    {
        [SerializeField] private string abilityId = "Fireball";
        [SerializeField] private float manaCost = 20;
        [SerializeField] private float cooldownSeconds = 2;
        [SerializeField] private float damage = 25;
        [SerializeField] private string blockedTag = "State.Stunned";
        [SerializeField] private GameplayEffectDefinition? damageEffect;

        public GameplayAbility ToRuntimeAbility()
        {
            var blockedTags = new GameplayTagContainer();
            if (!string.IsNullOrWhiteSpace(blockedTag))
            {
                blockedTags.Add(blockedTag);
            }

            GameplayEffectSpec? runtimeEffect = damageEffect is null
                ? null
                : damageEffect.ToRuntimeSpec();
            string runtimeDamageAttribute = "Health";
            double runtimeDamage = damage;
            if (runtimeEffect is not null)
            {
                if (runtimeEffect.Modifiers.Count != 1)
                {
                    throw new InvalidOperationException("能力伤害效果必须且只能包含一个属性修饰器。");
                }

                foreach (var modifier in runtimeEffect.Modifiers)
                {
                    if (modifier.Value >= 0)
                    {
                        throw new InvalidOperationException("能力伤害效果的属性增量必须为负数。");
                    }

                    runtimeDamageAttribute = modifier.Key;
                    runtimeDamage = -modifier.Value;
                }
            }

            return new GameplayAbility(
                abilityId,
                manaCost,
                cooldownSeconds,
                runtimeDamage,
                damageAttribute: runtimeDamageAttribute,
                damageEffect: runtimeEffect,
                blockedTags: blockedTags);
        }
    }
}
