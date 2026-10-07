using System;
using Lyking.FloodIt.Core;
using UnityEngine;

namespace Lyking.FloodIt.Unity
{
    /// <summary>
    /// 色彩淹没演示引导：装配核心棋局并采集键盘输入，
    /// 数字键 1-6 选色、R 重开一局、N 换一题；色块按钮的鼠标点击由面板 OnGUI 采集。
    /// <see cref="Step"/> 提供给测试与回放直接驱动核心。
    /// </summary>
    public sealed class FloodItDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private FloodItDemoPanel? panel;

        private FloodBoard? _board;

        public FloodBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public FloodItDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<FloodItDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("FloodItPanel").AddComponent<FloodItDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new FloodBoard(Environment.TickCount);
        }

        /// <summary>换一题：以当前时间作种子重建棋盘，步数清零。</summary>
        public void NewPuzzle()
        {
            if (HasBoard) Board.NewPuzzle(Environment.TickCount);
        }

        /// <summary>选择颜色；非法颜色或终局后的选色由核心拒绝。</summary>
        public bool Choose(int color) => HasBoard && Board.Choose(color);

        /// <summary>模拟入口：直接驱动核心的选色，供测试与回放使用。</summary>
        public bool Step(int color) => Choose(color);

        /// <summary>采集本帧按键：数字键 1-6 选色，R 重开一局，N 换一题。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
            if (Input.GetKeyDown(KeyCode.N)) NewPuzzle();

            if (!HasBoard) return;
            for (int color = 0; color < FloodItRules.ColorCount; color++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + color))) Board.Choose(color);
            }
        }

        private void Update()
        {
            ReadInput();
        }
    }
}
