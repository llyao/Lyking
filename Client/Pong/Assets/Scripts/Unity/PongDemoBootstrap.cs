using System;
using Lyking.Pong.Core;
using UnityEngine;

namespace Lyking.Pong.Unity
{
    /// <summary>
    /// 乒乓演示引导：装配核心对局、采集按键并按帧时长推进物理，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class PongDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private PongDemoPanel? panel;

        private PongGame? _game;
        private float _leftInput;

        public PongGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public PongDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<PongDemoPanel>();
            if (panel == null)
            {
                // 演示场景未预置面板时自动补建，保证打开场景即可游玩。
                panel = new GameObject("PongPanel").AddComponent<PongDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心对局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new PongGame(PongRules.Default, Environment.TickCount);
            _leftInput = 0f;
        }

        /// <summary>模拟入口：按给定输入推进核心物理，供测试与回放使用；核心内部按 1/120 秒固定子步推进防穿透。</summary>
        public void Step(float deltaTime, float leftMove)
        {
            if (HasGame) Game.Step(deltaTime, leftMove);
        }

        /// <summary>采集本帧输入：↑/W 上移、↓/S 下移，按住持续生效。</summary>
        private void ReadInput()
        {
            var move = 0f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) move += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) move -= 1f;
            _leftInput = move;
        }

        /// <summary>按已采集的输入推进一帧。</summary>
        private void Step(float deltaTime) => Step(deltaTime, _leftInput);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            ReadInput();
            if (!HasGame) return;
            Step(Time.deltaTime);
        }
    }
}
