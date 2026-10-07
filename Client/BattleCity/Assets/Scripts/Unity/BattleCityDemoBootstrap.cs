using System;
using Lyking.BattleCity.Core;
using UnityEngine;

namespace Lyking.BattleCity.Unity
{
    /// <summary>
    /// 坦克大战演示引导：装配核心战局、采集按键并按固定子步长推进，
    /// R 键重开。输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class BattleCityDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleCityDemoPanel? panel;

        private BattleCityGame? _game;

        public BattleCityGame Game => _game ?? throw new InvalidOperationException("战局尚未创建。");

        public bool HasGame => _game is not null;

        public BattleCityDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<BattleCityDemoPanel>();
            if (panel == null) panel = new GameObject("BattleCityPanel").AddComponent<BattleCityDemoPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：以当前时间作种子重建核心战局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new BattleCityGame(BattleCityRules.Default, Environment.TickCount);
        }

        /// <summary>模拟入口：按核心内固定子步长推进战局，供测试与回放使用。</summary>
        public void Step(float deltaTime, in TankInput input)
        {
            if (HasGame) Game.Step(deltaTime, input);
        }

        /// <summary>
        /// 采集本帧输入：方向键与 WASD 四向移动（按住生效，同按按上、下、左、右优先级取一），
        /// 空格或 J 射击（取按下沿）。
        /// </summary>
        private TankInput ReadInput()
        {
            TankDir moveDir = TankDir.None;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) moveDir = TankDir.Up;
            else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) moveDir = TankDir.Down;
            else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) moveDir = TankDir.Left;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) moveDir = TankDir.Right;

            bool fire = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
            return new TankInput(moveDir, fire);
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
