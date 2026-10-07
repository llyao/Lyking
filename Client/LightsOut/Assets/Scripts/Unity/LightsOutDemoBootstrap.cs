using System;
using Lyking.LightsOut.Core;
using UnityEngine;

namespace Lyking.LightsOut.Unity
{
    /// <summary>
    /// 点灯演示引导：装配核心棋局并采集键盘输入，R 重开、N 换一题、
    /// 方向键移动光标、回车按下光标格；鼠标点击由面板的 OnGUI 采集。
    /// <see cref="Step"/> 提供给测试与回放直接驱动核心。
    /// </summary>
    public sealed class LightsOutDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private LightsOutDemoPanel? panel;

        private LightsBoard? _board;

        public LightsBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public LightsOutDemoPanel? Panel => panel;

        /// <summary>键盘光标所在格位，方向键移动、回车按下。</summary>
        public int CursorRow { get; private set; }

        public int CursorColumn { get; private set; }

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<LightsOutDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("LightsOutPanel").AddComponent<LightsOutDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new LightsBoard(Environment.TickCount);
            MoveCursor(0, 0);
        }

        /// <summary>换一题：以当前时间作种子重建谜题，步数清零，谜题号递增。</summary>
        public void NewPuzzle()
        {
            if (HasBoard) Board.NewPuzzle(Environment.TickCount);
        }

        /// <summary>按下格位；非法格位或胜利后的按下由核心拒绝。</summary>
        public bool Press(int row, int column) => HasBoard && Board.Press(row, column);

        /// <summary>模拟入口：直接驱动核心的按下，供测试与回放使用。</summary>
        public bool Step(int row, int column) => Press(row, column);

        /// <summary>采集本帧按键：R 重开、N 换一题、方向键移动光标、回车按下光标格。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
            if (Input.GetKeyDown(KeyCode.N)) NewPuzzle();

            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(CursorRow - 1, CursorColumn);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(CursorRow + 1, CursorColumn);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(CursorRow, CursorColumn - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(CursorRow, CursorColumn + 1);
            if (Input.GetKeyDown(KeyCode.Return) && HasBoard) Board.Press(CursorRow, CursorColumn);
        }

        private void MoveCursor(int row, int column)
        {
            if (!HasBoard) return;

            CursorRow = Mathf.Clamp(row, 0, Board.Size - 1);
            CursorColumn = Mathf.Clamp(column, 0, Board.Size - 1);
        }

        private void Update()
        {
            ReadInput();
        }
    }
}
