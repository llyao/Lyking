using System;
using Lyking.Gomoku.Core;
using UnityEngine;

namespace Lyking.Gomoku.Unity
{
    /// <summary>
    /// 五子棋演示引导：装配核心对局、采集按键并把面板的鼠标点击换算为落子坐标，
    /// AI 回合立即应答。R 键重开一局并保留比分，输入读取与核心模拟分离，
    /// <see cref="PlayAt"/> 等入口提供给测试直接驱动。
    /// </summary>
    public sealed class GomokuDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private GomokuDemoPanel? panel;

        private GomokuMatch? _match;
        private int _selectedRow;
        private int _selectedCol;

        public GomokuMatch Match => _match ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasMatch => _match is not null;

        public GomokuDemoPanel? Panel => panel;

        /// <summary>当前光标行（0 为最上行）。</summary>
        public int SelectedRow => _selectedRow;

        /// <summary>当前光标列（0 为最左列）。</summary>
        public int SelectedCol => _selectedCol;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<GomokuDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("GomokuPanel").AddComponent<GomokuDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重开一局：首次创建对局，之后清空棋盘并保留比分累计。</summary>
        public void StartGame()
        {
            if (_match == null) _match = new GomokuMatch();
            else _match.Restart();
            _selectedRow = GomokuRules.Center;
            _selectedCol = GomokuRules.Center;
        }

        /// <summary>把光标移动到指定坐标；越界坐标被忽略。</summary>
        public void SelectCell(int row, int col)
        {
            if (GomokuRules.IsInside(row, col))
            {
                _selectedRow = row;
                _selectedCol = col;
            }
        }

        /// <summary>玩家在当前光标坐标落子；非法落点由核心规则拒绝。</summary>
        public void PlaySelected() => PlayAt(_selectedRow, _selectedCol);

        /// <summary>玩家在指定坐标落子；非法落点由核心规则拒绝，供鼠标点击与测试直接调用。</summary>
        public void PlayAt(int row, int col)
        {
            if (HasMatch) Match.PlayerMove(row, col);
        }

        /// <summary>采集本帧按键：R 重开一局，方向键或 A/D 移动光标，回车在光标处落子。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveCursor(0, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveCursor(0, 1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(-1, 0);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(1, 0);
            if (Input.GetKeyDown(KeyCode.Return)) PlaySelected();
        }

        /// <summary>光标按方向移动一格并夹在棋盘范围内。</summary>
        private void MoveCursor(int deltaRow, int deltaCol)
        {
            _selectedRow = Mathf.Clamp(_selectedRow + deltaRow, 0, GomokuRules.Size - 1);
            _selectedCol = Mathf.Clamp(_selectedCol + deltaCol, 0, GomokuRules.Size - 1);
        }

        private void Update()
        {
            ReadInput();
            if (!HasMatch || Match.Board.IsFinished) return;

            // AI 立即响应：轮到白方时由核心算出最优落子。
            if (Match.Board.CurrentTurn == Cell.White) Match.AiMove();
        }
    }
}
