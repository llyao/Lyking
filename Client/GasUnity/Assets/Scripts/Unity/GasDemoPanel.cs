using System.Collections.Generic;
using Lyking.GameplayAbilities.Core;
using UnityEngine;

namespace Lyking.GameplayAbilities.Unity
{
    public sealed class GasDemoPanel : MonoBehaviour
    {
        private readonly Queue<string> _events = new Queue<string>();
        private GasActor? _player;
        private GasActor? _trainingDummy;
        private string _message = string.Empty;

        public void Bind(GasActor player, GasActor trainingDummy)
        {
            _player = player;
            _trainingDummy = trainingDummy;
        }

        public void SetMessage(string message) => _message = message;

        public void AppendEvent(GameplayEvent gameplayEvent)
        {
            string detail = gameplayEvent.Type == GameplayEventType.AbilityRejected
                ? $"{gameplayEvent.Type}: {gameplayEvent.Reason}"
                : gameplayEvent.Type.ToString();
            _events.Enqueue(detail);
            while (_events.Count > 6)
            {
                _events.Dequeue();
            }
        }

        private void OnGUI()
        {
            if (_player is null || _trainingDummy is null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(20, 20, 420, 300), GUI.skin.box);
            GUILayout.Label("Lyking GAS 演示");
            GUILayout.Label($"玩家 Mana: {_player.Mana:0}    训练假人 Health: {_trainingDummy.Health:0}");
            GUILayout.Label("Space：激活 Fireball");
            GUILayout.Label(_message);
            GUILayout.Label("事件：");
            foreach (string item in _events)
            {
                GUILayout.Label("- " + item);
            }

            GUILayout.EndArea();
        }
    }
}
