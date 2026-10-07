using System;
using Lyking.WhackAMole.Core;
using UnityEngine;

namespace Lyking.WhackAMole.Unity
{
    /// <summary>
    /// 打地鼠演示引导：装配核心对局并按帧推进倒计时与地鼠调度，R 键重开。
    /// 打击输入由面板的鼠标点击与数字键 1-9 采集，首次打击负责开局，
    /// <see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class WhackAMoleDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private WhackAMoleDemoPanel? panel;

        private WhackAMoleGame? _game;

        public WhackAMoleGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public WhackAMoleDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<WhackAMoleDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("WhackAMolePanel").AddComponent<WhackAMoleDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心对局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new WhackAMoleGame(Environment.TickCount);
        }

        /// <summary>打击一个地洞；Ready 阶段的首次输入负责开局，非法打击由核心拒绝。</summary>
        public bool Whack(int holeIndex) => HasGame && Game.Whack(holeIndex);

        /// <summary>模拟入口：推进核心的倒计时与地鼠调度，供测试与回放使用。</summary>
        public void Step(float deltaTime)
        {
            if (HasGame) Game.Tick(deltaTime);
        }

        /// <summary>采集本帧按键：数字键 1-9 与小键盘 1-9 等效点击对应地洞（行优先、0 号在左上）。</summary>
        private void ReadInput()
        {
            for (int hole = 0; hole < WhackAMoleRules.HoleCount; hole++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + hole) || Input.GetKeyDown(KeyCode.Numpad1 + hole)) Whack(hole);
            }
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
