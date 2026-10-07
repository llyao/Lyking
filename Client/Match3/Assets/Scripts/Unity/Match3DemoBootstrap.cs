using System;
using Lyking.Match3.Core;
using UnityEngine;

namespace Lyking.Match3.Unity
{
    /// <summary>
    /// 三消演示引导：装配核心棋局并采集回合输入。方向键移动光标、回车选中/交换、
    /// 鼠标点击由面板采集，R 重开、S 死局重排；核心交换一次离散结算到稳定，无逐帧时间累加。
    /// <see cref="Swap"/> 与 <see cref="Select"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class Match3DemoBootstrap : MonoBehaviour
    {
        [SerializeField] private Match3DemoPanel? panel;

        private Match3Board? _board;
        private int _cursorIndex;
        private int _selectedIndex = -1;

        public Match3Board Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public Match3DemoPanel? Panel => panel;

        /// <summary>键盘光标所在的格位。</summary>
        public int CursorIndex => _cursorIndex;

        /// <summary>已被选中的格位，未选中时为 -1。</summary>
        public int SelectedIndex => _selectedIndex;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<Match3DemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("Match3Panel").AddComponent<Match3DemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，并复位光标与选中。</summary>
        public void StartGame()
        {
            _board = new Match3Board(Environment.TickCount);
            _cursorIndex = 0;
            _selectedIndex = -1;
        }

        /// <summary>交换两格；非法格位、不相邻或无消除的交换由核心拒绝并回退。</summary>
        public bool Swap(int indexA, int indexB) => HasBoard && Board.Swap(indexA, indexB);

        /// <summary>死局时重排棋盘，保持无三连且有可行交换；对局结束后拒绝。</summary>
        public bool Shuffle() => HasBoard && Board.Shuffle();

        /// <summary>
        /// 选中或交换一格：首次点击选中，再点同格取消，点相邻异格发起交换。
        /// 键盘回车与鼠标点击共用此入口，选中状态无论交换成败都清空。
        /// </summary>
        public void Select(int index)
        {
            if (!HasBoard || index < 0 || index >= Board.CellCount || Board.IsFinished) return;

            if (_selectedIndex < 0)
            {
                _selectedIndex = index;
                return;
            }

            int first = _selectedIndex;
            _selectedIndex = -1;
            if (first != index) Swap(first, index);
        }

        /// <summary>按行列偏移移动光标，越出棋盘时保持原位。</summary>
        private void MoveCursor(int rowOffset, int columnOffset)
        {
            int row = _cursorIndex / Match3Rules.Columns + rowOffset;
            int column = _cursorIndex % Match3Rules.Columns + columnOffset;
            if (row < 0 || row >= Match3Rules.Rows || column < 0 || column >= Match3Rules.Columns) return;
            _cursorIndex = row * Match3Rules.Columns + column;
        }

        /// <summary>采集本帧按键：R 重开、S 重排、方向键移动光标、回车选中/交换；鼠标点击由面板采集。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
            if (Input.GetKeyDown(KeyCode.S)) Shuffle();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(0, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(0, 1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(-1, 0);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1, 0);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Select(_cursorIndex);
        }

        private void Update()
        {
            ReadInput();
        }
    }
}
