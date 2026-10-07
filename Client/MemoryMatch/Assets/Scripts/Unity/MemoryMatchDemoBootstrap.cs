using System;
using Lyking.MemoryMatch.Core;
using UnityEngine;

namespace Lyking.MemoryMatch.Unity
{
    /// <summary>
    /// 记忆翻牌演示引导：装配核心棋局并按帧推进翻回倒计时，R 键重开。
    /// 翻牌输入由面板的鼠标点击采集，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class MemoryMatchDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private MemoryMatchDemoPanel? panel;

        private MemoryBoard? _board;

        public MemoryBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public MemoryMatchDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<MemoryMatchDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("MemoryMatchPanel").AddComponent<MemoryMatchDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new MemoryBoard(Environment.TickCount);
        }

        /// <summary>翻开一张卡；非法格位或当前阶段不允许的翻牌由核心拒绝。</summary>
        public bool Flip(int index) => HasBoard && Board.Flip(index);

        /// <summary>模拟入口：推进核心的翻回倒计时，供测试与回放使用。</summary>
        public void Step(float deltaTime)
        {
            if (HasBoard) Board.Tick(deltaTime);
        }

        /// <summary>采集本帧按键：R 重开一局；翻牌点击由面板 OnGUI 采集。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
        }

        private void Update()
        {
            ReadInput();
            if (!HasBoard) return;
            Step(Time.deltaTime);
        }
    }
}
