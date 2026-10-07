using System;
using Lyking.Snake.Core;
using UnityEngine;

namespace Lyking.Snake.Unity
{
    /// <summary>
    /// 贪吃蛇演示引导：装配核心棋局、采集按键并按 <see cref="SnakeBoard.StepSeconds"/> 的节奏推进，
    /// R 键重开。输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class SnakeDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private SnakeDemoPanel? panel;

        private SnakeBoard? _board;
        private float _accumulator;

        public SnakeBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public SnakeDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SnakeDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new SnakeBoard(SnakeRules.Default, Environment.TickCount);
            _accumulator = 0f;
        }

        /// <summary>入队一个转向方向；非法方向由核心忽略。</summary>
        public void Enqueue(SnakeDir dir)
        {
            if (HasBoard) Board.EnqueueDirection(dir);
        }

        /// <summary>模拟入口：按核心步进节奏推进棋局，供测试与回放使用。</summary>
        public void Step(float deltaTime)
        {
            _accumulator += deltaTime;
            float interval = Board.StepSeconds;
            while (_accumulator >= interval)
            {
                _accumulator -= interval;
                if (!Board.Tick())
                {
                    _accumulator = 0f;
                    return;
                }

                interval = Board.StepSeconds;
            }
        }

        /// <summary>采集本帧方向键：方向键与 WASD 等效，取按下沿。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Enqueue(SnakeDir.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Enqueue(SnakeDir.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Enqueue(SnakeDir.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Enqueue(SnakeDir.Right);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            ReadInput();
            if (!HasBoard) return;
            Step(Time.deltaTime);
        }
    }
}
