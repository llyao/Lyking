using System;
using Lyking.Bomberman.Core;
using UnityEngine;

namespace Lyking.Bomberman.Unity
{
    /// <summary>
    /// 炸弹人演示引导：装配核心战局、采集按键并按时钟推进核心。
    /// 方向键或 WASD 网格移动（按下沿），Space 放置炸弹，R 重开。
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class BombermanDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private BombermanDemoPanel? panel;

        private BombermanGame? _game;

        public BombermanGame Game => _game ?? throw new InvalidOperationException("战局尚未创建。");

        public bool HasGame => _game is not null;

        public BombermanDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<BombermanDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态补建，保证单独加载场景也能完整呈现。
                panel = new GameObject("BombermanDemoPanel").AddComponent<BombermanDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心战局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new BombermanGame(Environment.TickCount);
        }

        /// <summary>模拟入口：先即时结算本帧输入（移动与放弹），再按时钟推进战局，供测试与回放使用。</summary>
        public void Step(float deltaTime, BombInput input)
        {
            if (HasGame) Game.Step(deltaTime, input);
        }

        /// <summary>采集本帧按键：方向键与 WASD 等效取按下沿，Space 放置炸弹。</summary>
        private BombInput ReadInput()
        {
            var dir = BombermanDir.None;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = BombermanDir.Up;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = BombermanDir.Down;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dir = BombermanDir.Left;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = BombermanDir.Right;
            return new BombInput(dir, Input.GetKeyDown(KeyCode.Space));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            Step(Time.deltaTime, ReadInput());
        }
    }
}
