using System;
using Lyking.Frogger.Core;
using UnityEngine;

namespace Lyking.Frogger.Unity
{
    /// <summary>
    /// 青蛙过河演示引导：装配核心棋局、采集按键并按固定子步长推进车流与浮排，R 键重开。
    /// 跳跃为离散事件，本帧按下沿先入队、随下一次 <see cref="Step"/> 在推进前结算；
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class FroggerDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private FroggerDemoPanel? panel;

        private FroggerBoard? _board;
        private FroggerDir? _pendingHop;

        public FroggerBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public FroggerDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<FroggerDemoPanel>();
            if (panel == null) panel = new GameObject("FroggerPanel").AddComponent<FroggerDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new FroggerBoard(FroggerRules.Default, Environment.TickCount);
            _pendingHop = null;
        }

        /// <summary>记下一跳方向；本帧已有时以最后一次按下为准，非法跳跃由核心拒绝。</summary>
        public void Enqueue(FroggerDir dir)
        {
            if (HasBoard) _pendingHop = dir;
        }

        /// <summary>模拟入口：按核心内固定子步长推进棋局，离散跳跃在推进前结算，供测试与回放使用。</summary>
        public void Step(float deltaTime, FroggerDir? hop)
        {
            if (HasBoard) Board.Step(deltaTime, hop);
        }

        /// <summary>采集本帧跳跃键：方向键与 WASD 等效，取按下沿。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Enqueue(FroggerDir.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Enqueue(FroggerDir.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Enqueue(FroggerDir.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Enqueue(FroggerDir.Right);
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
            Step(Time.deltaTime, _pendingHop);
            _pendingHop = null;
        }
    }
}
