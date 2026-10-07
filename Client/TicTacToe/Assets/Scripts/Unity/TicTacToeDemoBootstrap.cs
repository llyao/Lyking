using System;
using Lyking.TicTacToe.Core;
using UnityEngine;

namespace Lyking.TicTacToe.Unity
{
    /// <summary>
    /// 井字棋演示引导：装配核心对局、采集按键并把面板的鼠标点击转换为落子，
    /// AI 回合立即应答。R 键重开一局并保留比分，输入读取与核心模拟分离，
    /// <see cref="PlayCell"/> 等入口提供给测试直接驱动。
    /// </summary>
    public sealed class TicTacToeDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private TicTacToeDemoPanel? panel;

        private TicTacToeMatch? _match;

        public TicTacToeMatch Match => _match ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasMatch => _match is not null;

        public TicTacToeDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<TicTacToeDemoPanel>();
            if (panel == null)
            {
                // 场景缺省时动态创建面板物体，保证演示不依赖手工摆放。
                panel = new GameObject("TicTacToePanel").AddComponent<TicTacToeDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重开一局：首次创建对局，之后清空棋盘并保留比分累计。</summary>
        public void StartGame()
        {
            if (_match == null) _match = new TicTacToeMatch();
            else _match.Restart();
        }

        /// <summary>玩家在指定格子落子；非法落点由核心规则拒绝。</summary>
        public void PlayCell(int index)
        {
            if (HasMatch) Match.PlayerMove(index);
        }

        /// <summary>采集本帧按键：R 重开一局。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartGame();
        }

        private void Update()
        {
            ReadInput();
            if (!HasMatch || Match.Board.IsFinished) return;

            // AI 立即响应：轮到 O 时由核心算出最优落子。
            if (Match.Board.CurrentTurn == Cell.O) Match.AiMove();
        }
    }
}
