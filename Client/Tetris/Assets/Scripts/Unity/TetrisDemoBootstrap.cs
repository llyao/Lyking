using System;
using Lyking.Tetris.Core;
using UnityEngine;

namespace Lyking.Tetris.Unity
{
    /// <summary>
    /// 俄罗斯方块演示引导：装配核心棋局、采集按键并按 <see cref="TetrisBoard.StepSeconds"/> 的节奏推进，
    /// R 键重开。输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class TetrisDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private TetrisDemoPanel? panel;

        private TetrisBoard? _board;
        private float _accumulator;

        public TetrisBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public TetrisDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<TetrisDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new TetrisBoard(TetrisRules.Default, Environment.TickCount);
            _accumulator = 0f;
        }

        /// <summary>左移一格；非法移动由核心忽略。</summary>
        public void MoveLeft()
        {
            if (HasBoard) Board.MoveLeft();
        }

        /// <summary>右移一格；非法移动由核心忽略。</summary>
        public void MoveRight()
        {
            if (HasBoard) Board.MoveRight();
        }

        /// <summary>顺时针旋转；踢墙失败时由核心保持原朝向。</summary>
        public void Rotate()
        {
            if (HasBoard) Board.Rotate();
        }

        /// <summary>软降一格；触底时由核心锁定。</summary>
        public void SoftDrop()
        {
            if (HasBoard) Board.SoftDrop();
        }

        /// <summary>硬降：直接落底锁定。</summary>
        public void HardDrop()
        {
            if (HasBoard) Board.HardDrop();
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

        /// <summary>采集本帧按键：方向键与 WASD 等效、空格硬降，取按下沿。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveRight();
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) SoftDrop();
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Rotate();
            if (Input.GetKeyDown(KeyCode.Space)) HardDrop();
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
