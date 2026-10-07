using System;
using Lyking.SpaceShooter.Core;
using UnityEngine;

namespace Lyking.SpaceShooter.Unity
{
    /// <summary>
    /// 太空射击演示引导：装配核心战局、采集按键并按固定子步长推进，
    /// R 键重开。输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class SpaceShooterDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private SpaceShooterDemoPanel? panel;

        private SpaceShooterGame? _game;

        public SpaceShooterGame Game => _game ?? throw new InvalidOperationException("战局尚未创建。");

        public bool HasGame => _game is not null;

        public SpaceShooterDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SpaceShooterDemoPanel>();
            if (panel == null) panel = new GameObject("SpaceShooterPanel").AddComponent<SpaceShooterDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心战局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new SpaceShooterGame(ShooterRules.Default, Environment.TickCount);
        }

        /// <summary>模拟入口：按核心内固定子步长推进战局，供测试与回放使用。</summary>
        public void Step(float deltaTime, in ShooterInput input)
        {
            if (HasGame) Game.Step(deltaTime, input);
        }

        /// <summary>采集本帧输入：方向键与 A/D 横移（按住生效），空格或 J 射击（取按下沿）。</summary>
        private ShooterInput ReadInput()
        {
            float moveX = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) moveX += 1f;
            bool fire = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
            return new ShooterInput(moveX, fire);
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
