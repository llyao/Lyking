using System;
using Lyking.EndlessRunner.Core;
using UnityEngine;

namespace Lyking.EndlessRunner.Unity
{
    /// <summary>
    /// 无尽跑酷演示引导：装配核心棋局、采集按键并按固定子步长推进，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step(float, in RunnerInput)"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class EndlessRunnerDemoBootstrap : MonoBehaviour
    {
        /// <summary>物理子步长：把帧时长切成固定小步推进，保证模拟确定性。</summary>
        private const float SubStepSeconds = 1f / 120f;

        [SerializeField] private EndlessRunnerDemoPanel? panel;

        private EndlessRunnerGame? _game;
        private float _accumulator;

        public EndlessRunnerGame Game => _game ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasGame => _game is not null;

        public EndlessRunnerDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<EndlessRunnerDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态创建并挂到新物体上，保证演示始终可见。
                panel = new GameObject("EndlessRunnerDemoPanel").AddComponent<EndlessRunnerDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心状态，保留面板绑定。</summary>
        public void StartGame()
        {
            if (_game is null) _game = new EndlessRunnerGame(RunnerRules.Default, Environment.TickCount);
            else _game.Reset(Environment.TickCount);
            _accumulator = 0f;
        }

        /// <summary>模拟入口：采集本帧按键并推进棋局。</summary>
        public void Step(float deltaTime)
        {
            Step(deltaTime, ReadInput());
        }

        /// <summary>模拟入口：按固定子步长推进棋局，本帧的跳跃按下沿只作用于第一个子步。</summary>
        public void Step(float deltaTime, in RunnerInput input)
        {
            if (!HasGame) return;
            _accumulator += deltaTime;
            bool jumpConsumed = false;
            while (_accumulator >= SubStepSeconds)
            {
                _accumulator -= SubStepSeconds;
                var subInput = new RunnerInput(input.JumpPressed && !jumpConsumed, input.FastFallHeld);
                if (!Game.Advance(SubStepSeconds, subInput))
                {
                    _accumulator = 0f;
                    return;
                }

                jumpConsumed = true;
            }
        }

        /// <summary>采集本帧按键：空格或上方向键的按下沿起跳，下方向键持续按住快落。</summary>
        private RunnerInput ReadInput()
        {
            bool jump = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow);
            bool fastFall = Input.GetKey(KeyCode.DownArrow);
            return new RunnerInput(jump, fastFall);
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
