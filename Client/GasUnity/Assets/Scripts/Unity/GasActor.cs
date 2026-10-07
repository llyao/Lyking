#nullable enable
using Lyking.GameplayAbilities.Core;
using UnityEngine;

namespace Lyking.GameplayAbilities.Unity
{
    public sealed class GasActor : MonoBehaviour
    {
        [SerializeField] private float initialHealth = 100;
        [SerializeField] private float initialMana = 100;
        [SerializeField] private float attackPower = 25;

        private ManualGameplayClock? _clock;

        public AbilitySystemComponent AbilitySystem { get; private set; } = null!;

        public float Health => (float)AbilitySystem.Attributes.Get("Health");

        public float Mana => (float)AbilitySystem.Attributes.Get("Mana");

        public float GetCooldownRemaining(string abilityId) => (float)AbilitySystem.GetCooldownRemaining(abilityId);

        private void Awake()
        {
            _clock = new ManualGameplayClock();
            AbilitySystem = new AbilitySystemComponent(_clock);
            AbilitySystem.Attributes.Set("Health", initialHealth);
            AbilitySystem.Attributes.Set("Mana", initialMana);
            AbilitySystem.Attributes.Set("AttackPower", attackPower);
        }

        public void Tick(float deltaTime)
        {
            _clock?.Advance(Mathf.Max(0, deltaTime));
        }
    }
}
