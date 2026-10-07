using System;
using Lyking.Breakout.Core;
using UnityEngine;

namespace Lyking.Breakout.Unity
{
    /// <summary>
    /// 打砖块演示引导：装配核心对局、采集按键并按帧时长推进物理，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class BreakoutDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private BreakoutDemoPanel? panel;

        private BreakoutGame? _game;

        public BreakoutGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public BreakoutDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<BreakoutDemoPanel>();
            if (panel == null)
            {
                // 演示场景未预置面板时自动补建，保证打开场景即可游玩。
                panel = new GameObject("BreakoutPanel").AddComponent<BreakoutDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心对局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new BreakoutGame(BreakoutRules.Default, Environment.TickCount);
        }

        /// <summary>设置挡板移动方向：-1 左移、0 停止、+1 右移。</summary>
        public void MovePaddle(float direction)
        {
            if (HasGame) Game.MovePaddle(direction);
        }

        /// <summary>发球；对局结束或球已在飞行中时由核心忽略。</summary>
        public void Launch()
        {
            if (HasGame) Game.Launch();
        }

        /// <summary>模拟入口：推进核心物理，供测试与回放使用；核心内部按 1/120 秒固定子步推进防穿透。</summary>
        public void Step(float deltaTime)
        {
            if (HasGame) Game.Step(deltaTime);
        }

        /// <summary>采集本帧输入：方向键与 A/D 等效控制挡板，空格发球。</summary>
        private void ReadInput()
        {
            var direction = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direction -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direction += 1f;
            MovePaddle(direction);

            if (Input.GetKeyDown(KeyCode.Space)) Launch();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            ReadInput();
            if (!HasGame) return;
            Step(Time.deltaTime);
        }
    }
}
