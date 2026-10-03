using System.Collections.Generic;
using Lyking.GameplayAbilities.Core;
using UnityEngine;

namespace Lyking.GameplayAbilities.Definitions
{
    [CreateAssetMenu(menuName = "Lyking/GAS/Gameplay Effect Definition")]
    public sealed class GameplayEffectDefinition : ScriptableObject
    {
        [SerializeField] private string attributeName = "Health";
        [SerializeField] private float delta = -25;

        public GameplayEffectSpec ToRuntimeSpec()
        {
            return new GameplayEffectSpec(new Dictionary<string, double>
            {
                [attributeName] = delta,
            });
        }
    }
}
