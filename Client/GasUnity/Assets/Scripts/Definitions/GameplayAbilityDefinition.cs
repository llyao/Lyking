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

        public GameplayAbility ToRuntimeAbility()
        {
            var blockedTags = new GameplayTagContainer();
            if (!string.IsNullOrWhiteSpace(blockedTag))
            {
                blockedTags.Add(blockedTag);
            }

            return new GameplayAbility(
                abilityId,
                manaCost,
                cooldownSeconds,
                damage,
                blockedTags: blockedTags);
        }
    }
}
