using System;
using Lyking.Simon.Core;
using UnityEngine;

namespace Lyking.Simon.Unity
{
    /// <summary>
    /// 西蒙记忆演示引导：装配核心对局并按帧推进播放节奏与复述超时，R 键重开。
    /// 鼠标点击四色块由面板采集，键盘（数字 1-4 或方向键）在此读取；
    /// <see cref="Step"/> 与 <see cref="Press"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class SimonDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private SimonDemoPanel? panel;

        private SimonGame? _game;

        public SimonGame Game => _game ?? throw new InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public SimonDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SimonDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("SimonPanel").AddComponent<SimonDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心对局，最长纪录由核心跨局保留。</summary>
        public void StartGame()
        {
            if (_game == null) _game = new SimonGame(Environment.TickCount);
            else _game.Reset(Environment.TickCount);
        }

        /// <summary>输入一个色块；非法格位或当前阶段不允许的输入由核心拒绝。</summary>
        public bool Press(int padIndex) => HasGame && Game.Press(padIndex);

        /// <summary>模拟入口：推进核心的播放节奏与复述超时，供测试与回放使用。</summary>
        public void Step(float deltaTime)
        {
            if (HasGame) Game.Step(deltaTime);
        }

        /// <summary>采集本帧按键：R 重开一局；数字 1-4 与方向键映射四色块，鼠标点击由面板 OnGUI 采集。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            int pad = ReadPadKey();
            if (pad >= 0) Press(pad);
        }

        /// <summary>把数字键 1-4 与方向键换算为色块编号：1/左 上左块、2/上 右上块、3/下 左下块、4/右 右下块。</summary>
        private static int ReadPadKey()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.LeftArrow)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.UpArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.DownArrow)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.RightArrow)) return 3;
            return -1;
        }

        private void Update()
        {
            ReadInput();
            if (!HasGame) return;
            Step(Time.deltaTime);
        }
    }
}
