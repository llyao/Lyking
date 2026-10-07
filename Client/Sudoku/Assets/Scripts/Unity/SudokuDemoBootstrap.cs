using System;
using Lyking.Sudoku.Core;
using UnityEngine;

namespace Lyking.Sudoku.Unity
{
    /// <summary>
    /// 数独演示引导：装配核心棋局、采集按键并直接调用离散回合制的核心方法。
    /// 方向键移动光标，数字键 1-9 在光标格填数，0 或退格清除，R 生成新谜题，H 提示；
    /// 鼠标点击选格由面板的 OnGUI 采集。输入读取与核心模拟分离，核心方法即测试入口。
    /// </summary>
    public sealed class SudokuDemoBootstrap : MonoBehaviour
    {
        private static readonly KeyCode[] DigitKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };

        private static readonly KeyCode[] PadDigitKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
        };

        [SerializeField] private SudokuDemoPanel? panel;

        private SudokuPuzzle? _puzzle;

        /// <summary>生成新谜题使用的难度档位。</summary>
        public SudokuDifficulty Difficulty { get; set; } = SudokuDifficulty.Easy;

        public SudokuPuzzle Puzzle => _puzzle ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasPuzzle => _puzzle is not null;

        public SudokuDemoPanel? Panel => panel;

        /// <summary>键盘光标所在格位，方向键移动、数字键对光标格填数。</summary>
        public int CursorRow { get; private set; }

        /// <summary>键盘光标所在格位，方向键移动、数字键对光标格填数。</summary>
        public int CursorColumn { get; private set; }

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SudokuDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态补建，保证单独加载场景也能完整呈现。
                panel = new GameObject("SudokuDemoPanel").AddComponent<SudokuDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>以当前时间作种子生成新谜题，保留面板绑定并复位光标。</summary>
        public void StartGame()
        {
            _puzzle = new SudokuPuzzle(Environment.TickCount, Difficulty);
            MoveCursor(0, 0);
        }

        /// <summary>在指定格填数；给定格、满盘或非法值由核心拒绝。</summary>
        public bool Fill(int row, int column, int value) => HasPuzzle && Puzzle.Fill(row, column, value);

        /// <summary>清除指定格的玩家填数；给定格或空格由核心拒绝。</summary>
        public bool Clear(int row, int column) => HasPuzzle && Puzzle.Clear(row, column);

        /// <summary>提示：由核心在随机空格填入正确答案。</summary>
        public bool Hint() => HasPuzzle && Puzzle.Hint(Environment.TickCount);

        /// <summary>移动光标到指定格位，越界时收敛到边界内。</summary>
        public void MoveCursor(int row, int column)
        {
            CursorRow = Mathf.Clamp(row, 0, SudokuRules.Size - 1);
            CursorColumn = Mathf.Clamp(column, 0, SudokuRules.Size - 1);
        }

        /// <summary>采集本帧按键：方向键移动光标，数字键填数，0 或退格清除。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(CursorRow - 1, CursorColumn);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(CursorRow + 1, CursorColumn);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(CursorRow, CursorColumn - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(CursorRow, CursorColumn + 1);

            for (int value = 1; value <= SudokuRules.Size; value++)
            {
                if (Input.GetKeyDown(DigitKeys[value - 1]) || Input.GetKeyDown(PadDigitKeys[value - 1]))
                    Fill(CursorRow, CursorColumn, value);
            }

            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0) ||
                Input.GetKeyDown(KeyCode.Backspace))
            {
                Clear(CursorRow, CursorColumn);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                Hint();
                return;
            }

            ReadInput();
        }
    }
}
