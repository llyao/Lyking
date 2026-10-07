using System;
using Lyking.MazeChase.Core;
using UnityEngine;

namespace Lyking.MazeChase.Unity
{
    /// <summary>
    /// 吃豆迷宫演示引导：装配核心棋局、采集按键并按核心内固定子步长推进，
    /// R 键重开。输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class MazeChaseDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private MazeChaseDemoPanel? panel;

        private MazeChaseGame? _game;

        public MazeChaseGame Game => _game ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasGame => _game is not null;

        public MazeChaseDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<MazeChaseDemoPanel>();
            if (panel == null) panel = new GameObject("MazeChasePanel").AddComponent<MazeChaseDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new MazeChaseGame(MazeChaseRules.Default, Environment.TickCount);
        }

        /// <summary>设置玩家意向方向并推进棋局：意向由核心在格点对齐时应用，供测试与回放使用。</summary>
        public void Step(float deltaTime, MazeDir intent)
        {
            if (!HasGame) return;
            if (intent != MazeDir.None) Game.SetIntent(intent);
            Game.Step(deltaTime);
        }

        /// <summary>采集本帧方向键：方向键与 WASD 等效，取按下沿；无按键时保持原意向。</summary>
        private MazeDir ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return MazeDir.Up;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return MazeDir.Down;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return MazeDir.Left;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return MazeDir.Right;
            return MazeDir.None;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (!HasGame) return;
            Step(Time.deltaTime, ReadInput());
        }
    }
}
