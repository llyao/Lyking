using System;
using Lyking.DoodleJumper.Core;
using UnityEngine;

namespace Lyking.DoodleJumper.Unity
{
    /// <summary>
    /// 涂鸦跳跃演示引导：装配核心棋局、采集按键并按固定子步长推进，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step(float, in JumperInput)"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class DoodleJumperDemoBootstrap : MonoBehaviour
    {
        /// <summary>物理子步长：把帧时长切成固定小步推进，保证模拟确定性。</summary>
        private const float SubStepSeconds = 1f / 120f;

        [SerializeField] private DoodleJumperDemoPanel? panel;

        private JumperGame? _game;
        private float _accumulator;

        public JumperGame Game => _game ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasGame => _game is not null;

        public DoodleJumperDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<DoodleJumperDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态创建并挂到新物体上，保证演示始终可见。
                panel = new GameObject("DoodleJumperDemoPanel").AddComponent<DoodleJumperDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心状态，保留面板绑定。</summary>
        public void StartGame()
        {
            if (_game is null) _game = new JumperGame(JumperRules.Default, Environment.TickCount);
            else _game.Reset(Environment.TickCount);
            _accumulator = 0f;
        }

        /// <summary>模拟入口：采集本帧按键并推进棋局。</summary>
        public void Step(float deltaTime)
        {
            Step(deltaTime, ReadInput());
        }

        /// <summary>模拟入口：按固定子步长推进棋局，水平与快落均为持续输入，每个子步都生效。</summary>
        public void Step(float deltaTime, in JumperInput input)
        {
            if (!HasGame) return;
            _accumulator += deltaTime;
            while (_accumulator >= SubStepSeconds)
            {
                _accumulator -= SubStepSeconds;
                if (!Game.Advance(SubStepSeconds, input))
                {
                    _accumulator = 0f;
                    return;
                }
            }
        }

        /// <summary>采集本帧按键：左右方向键或 A/D 的按住状态合成水平意图，下方向键持续按住快落。</summary>
        private JumperInput ReadInput()
        {
            float moveX = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) moveX += 1f;
            bool fastFall = Input.GetKey(KeyCode.DownArrow);
            return new JumperInput(moveX, fastFall);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            Step(Time.deltaTime);
        }
    }
}
