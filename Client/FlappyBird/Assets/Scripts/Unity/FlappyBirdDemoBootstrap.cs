using System;
using Lyking.FlappyBird.Core;
using UnityEngine;

namespace Lyking.FlappyBird.Unity
{
    /// <summary>
    /// Flappy 演示引导：装配核心棋局、采集点按并按固定子步长推进，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class FlappyBirdDemoBootstrap : MonoBehaviour
    {
        /// <summary>物理子步长：把帧时长切成固定小步推进，保证模拟确定性。</summary>
        private const float SubStepSeconds = 1f / 120f;

        [SerializeField] private FlappyBirdDemoPanel? panel;

        private FlappyBirdGame? _board;
        private float _accumulator;

        public FlappyBirdGame Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public FlappyBirdDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<FlappyBirdDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心状态，保留面板绑定。</summary>
        public void StartGame()
        {
            if (_board is null) _board = new FlappyBirdGame(FlappyRules.Default, Environment.TickCount);
            else _board.Reset(Environment.TickCount);
            _accumulator = 0f;
        }

        /// <summary>模拟入口：按固定子步长推进棋局，本帧的点按只作用于第一个子步。</summary>
        public void Step(float deltaTime, bool flap)
        {
            if (!HasBoard) return;
            _accumulator += deltaTime;
            bool flapConsumed = false;
            while (_accumulator >= SubStepSeconds)
            {
                _accumulator -= SubStepSeconds;
                if (!Board.Advance(SubStepSeconds, flap && !flapConsumed))
                {
                    _accumulator = 0f;
                    return;
                }

                flapConsumed = true;
            }
        }

        /// <summary>采集本帧点按：空格或鼠标左键的按下沿。</summary>
        private bool ReadInput()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            bool flap = ReadInput();
            if (!HasBoard) return;
            Step(Time.deltaTime, flap);
        }
    }
}
