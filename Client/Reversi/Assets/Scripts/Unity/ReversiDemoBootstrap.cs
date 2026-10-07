using System;
using Lyking.Reversi.Core;
using UnityEngine;

namespace Lyking.Reversi.Unity
{
    /// <summary>
    /// 黑白棋演示引导：装配核心对局、采集按键与鼠标点击并把光标移动/落子交给核心规则裁决，
    /// AI 回合立即应答。R 键重开一局并保留比分，输入读取与核心模拟分离，
    /// <see cref="PlayAt"/> 等入口提供给测试直接驱动。离散回合制，无时间累加器。
    /// </summary>
    public sealed class ReversiDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private ReversiDemoPanel? panel;

        private ReversiMatch? _match;
        private int _cursorRow;
        private int _cursorCol;

        public ReversiMatch Match => _match ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasMatch => _match is not null;

        public ReversiDemoPanel? Panel => panel;

        /// <summary>玩家光标所在行（从 0 起）。</summary>
        public int CursorRow => _cursorRow;

        /// <summary>玩家光标所在列（从 0 起）。</summary>
        public int CursorCol => _cursorCol;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<ReversiDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("ReversiPanel").AddComponent<ReversiDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重开一局：首次创建对局，之后清空棋盘并保留比分累计。</summary>
        public void StartGame()
        {
            if (_match == null) _match = new ReversiMatch();
            else _match.Restart();

            // 光标回到棋盘中心。
            _cursorRow = ReversiRules.Size / 2 - 1;
            _cursorCol = ReversiRules.Size / 2 - 1;
        }

        /// <summary>玩家在指定格子落子；非法落点由核心规则拒绝。</summary>
        public void PlayAt(int row, int col)
        {
            if (HasMatch) Match.PlayerMove(row, col);
        }

        /// <summary>在光标位置尝试落子（回车键）。</summary>
        public void PlayAtCursor() => PlayAt(_cursorRow, _cursorCol);

        /// <summary>把光标移动 delta 格并夹取在棋盘范围内。</summary>
        public void MoveCursor(int deltaRow, int deltaCol)
        {
            _cursorRow = Mathf.Clamp(_cursorRow + deltaRow, 0, ReversiRules.Size - 1);
            _cursorCol = Mathf.Clamp(_cursorCol + deltaCol, 0, ReversiRules.Size - 1);
        }

        /// <summary>采集本帧按键：方向键或 WASD 移动光标，回车落子。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveCursor(-1, 0);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveCursor(1, 0);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveCursor(0, -1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveCursor(0, 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PlayAtCursor();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            ReadInput();
            if (!HasMatch || Match.Board.IsFinished) return;

            // AI 立即响应：轮到白方时由核心算出最优落子；己方过手后下一帧继续应答。
            if (Match.Board.CurrentTurn == Cell.White) Match.AiMove();
        }
    }
}
