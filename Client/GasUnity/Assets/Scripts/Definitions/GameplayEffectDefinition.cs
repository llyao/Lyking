using System;
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
        [SerializeField] private float durationSeconds;
        [SerializeField] private bool isInfinite;
        [SerializeField] private string[] grantedTags = Array.Empty<string>();

        public GameplayEffectSpec ToRuntimeSpec()
        {
            var tags = new GameplayTagContainer();
            foreach (string tag in grantedTags ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
            }

            var modifiers = new Dictionary<string, double>
            {
                [attributeName] = delta,
            };
            return isInfinite
                ? GameplayEffectSpec.CreateInfinite(modifiers, tags)
                : new GameplayEffectSpec(modifiers, tags, durationSeconds);
        }
    }
}
