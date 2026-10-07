using System;
using Lyking.ConnectFour.Core;
using UnityEngine;

namespace Lyking.ConnectFour.Unity
{
    /// <summary>
    /// 四子连珠演示引导：装配核心对局、采集按键并把面板的鼠标点击换算为落子列，
    /// AI 回合立即应答。R 键重开一局并保留比分，输入读取与核心模拟分离，
    /// <see cref="PlayColumn"/> 等入口提供给测试直接驱动。
    /// </summary>
    public sealed class ConnectFourDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private ConnectFourDemoPanel? panel;

        private ConnectFourMatch? _match;
        private int _selectedColumn;

        public ConnectFourMatch Match => _match ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasMatch => _match is not null;

        public ConnectFourDemoPanel? Panel => panel;

        /// <summary>当前列光标（0 为最左列）。</summary>
        public int SelectedColumn => _selectedColumn;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<ConnectFourDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("ConnectFourPanel").AddComponent<ConnectFourDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重开一局：首次创建对局，之后清空棋盘并保留比分累计。</summary>
        public void StartGame()
        {
            if (_match == null) _match = new ConnectFourMatch();
            else _match.Restart();
            _selectedColumn = ConnectFourRules.Columns / 2;
        }

        /// <summary>把列光标移动到指定列；越界列被忽略。</summary>
        public void SelectColumn(int col)
        {
            if (col >= 0 && col < ConnectFourRules.Columns) _selectedColumn = col;
        }

        /// <summary>玩家在当前光标列落子；非法落点由核心规则拒绝。</summary>
        public void PlaySelectedColumn()
        {
            PlayColumn(_selectedColumn);
        }

        /// <summary>玩家往指定列落子；非法落点由核心规则拒绝，供鼠标点击与测试直接调用。</summary>
        public void PlayColumn(int col)
        {
            if (HasMatch) Match.PlayerDrop(col);
        }

        /// <summary>采集本帧按键：R 重开一局，←→ 移动列光标，回车或空格在光标列落子。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) PlayColumn(_selectedColumn);
        }

        /// <summary>列光标按方向移动一格并夹在棋盘范围内。</summary>
        private void MoveCursor(int delta)
        {
            _selectedColumn = Mathf.Clamp(_selectedColumn + delta, 0, ConnectFourRules.Columns - 1);
        }

        private void Update()
        {
            ReadInput();
            if (!HasMatch || Match.Board.IsFinished) return;

            // AI 立即响应：轮到黄方时由核心算出最优落子。
            if (Match.Board.CurrentTurn == Cell.Yellow) Match.AiDrop();
        }
    }
}
