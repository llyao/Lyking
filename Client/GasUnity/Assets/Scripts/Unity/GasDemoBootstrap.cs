#nullable enable
using System;
using Lyking.GameplayAbilities.Core;
using Lyking.GameplayAbilities.Definitions;
using UnityEngine;

namespace Lyking.GameplayAbilities.Unity
{
    public sealed class GasDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private GasActor? player;
        [SerializeField] private GasActor? trainingDummy;
        [SerializeField] private GameplayAbilityDefinition? fireballDefinition;
        [SerializeField] private GasDemoPanel? panel;

        private void Start()
        {
            player ??= GameObject.Find("Player")?.GetComponent<GasActor>();
            trainingDummy ??= GameObject.Find("TrainingDummy")?.GetComponent<GasActor>();
            panel ??= FindObjectOfType<GasDemoPanel>();
            if (player is null || trainingDummy is null)
            {
                Debug.LogError("GasDemo 场景缺少 Player 或 TrainingDummy。");
                enabled = false;
                return;
            }

            GameplayAbility ability = fireballDefinition is null
                ? FireballAbility.Create(20, 2, 25)
                : fireballDefinition.ToRuntimeAbility();
            player.AbilitySystem.GrantAbility(ability);
            player.AbilitySystem.EventPublished += OnGameplayEvent;
            trainingDummy.AbilitySystem.EventPublished += OnGameplayEvent;
            panel?.Bind(player, trainingDummy);
            panel?.SetMessage("按 Space 施放火球");
        }

        private void Update()
        {
            if (player is null || trainingDummy is null)
            {
                return;
            }

            player.Tick(Time.deltaTime);
            trainingDummy.Tick(Time.deltaTime);
            ProcessInput(Input.GetKeyDown(KeyCode.Space) ? KeyCode.Space : KeyCode.None);
        }

        private void OnDestroy()
        {
            if (player is not null)
            {
                player.AbilitySystem.EventPublished -= OnGameplayEvent;
            }

            if (trainingDummy is not null)
            {
                trainingDummy.AbilitySystem.EventPublished -= OnGameplayEvent;
            }
        }

        public GasActor PlayerActor => player ?? throw new InvalidOperationException("玩家尚未装配。");

        public GasActor TrainingDummyActor => trainingDummy ?? throw new InvalidOperationException("训练假人尚未装配。");

        /// <summary>
        /// 处理一帧输入。输入读取与能力激活分开，便于测试和替换输入来源。
        /// </summary>
        public bool ProcessInput(KeyCode key)
        {
            return key == KeyCode.Space && TryCastFireball().Succeeded;
        }

        public AbilityActivationResult TryCastFireball()
        {
            double healthBefore = TrainingDummyActor.Health;
            AbilityActivationResult result = PlayerActor.AbilitySystem.TryActivate("Fireball", TrainingDummyActor.AbilitySystem);
            double damageDealt = healthBefore - TrainingDummyActor.Health;
            panel?.SetMessage(result.Succeeded
                ? $"火球命中，训练假人受到 {damageDealt:0.##} 点伤害。"
                : $"施法失败：{result.Reason}");
            return result;
        }

        private void OnGameplayEvent(GameplayEvent gameplayEvent)
        {
            panel?.AppendEvent(gameplayEvent);
        }
    }
}
