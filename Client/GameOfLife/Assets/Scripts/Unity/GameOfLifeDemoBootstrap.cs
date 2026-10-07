using System;
using Lyking.GameOfLife.Core;
using UnityEngine;

namespace Lyking.GameOfLife.Unity
{
    /// <summary>
    /// 生命游戏演示引导：装配核心棋局并采集键盘输入，空格暂停/继续、N 单步、
    /// C 清空、P 载入滑翔机枪、R 随机重撒；鼠标绘制由面板的 OnGUI 采集。
    /// 运行态按 <see cref="LifeRules.StepSeconds"/> 的固定节拍推进，
    /// <see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class GameOfLifeDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private GameOfLifeDemoPanel? panel;

        private LifeBoard? _board;
        private float _accumulator;

        public LifeBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public GameOfLifeDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<GameOfLifeDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("GameOfLifePanel").AddComponent<GameOfLifeDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：新建空棋局并以当前时间作种子随机撒子，节拍累积清零。</summary>
        public void StartGame()
        {
            _board = new LifeBoard();
            _board.Randomize(Environment.TickCount, LifeRules.DefaultDensity);
            _accumulator = 0f;
        }

        /// <summary>随机重撒：以当前时间作种子按默认密度重掷初始布局，代数归零。</summary>
        public void Randomize()
        {
            if (HasBoard) Board.Randomize(Environment.TickCount, LifeRules.DefaultDensity);
        }

        /// <summary>清空棋盘：全部死亡、代数归零，等待人工绘制或重新撒子。</summary>
        public void Clear()
        {
            if (HasBoard) Board.Clear();
        }

        /// <summary>载入滑翔机枪图案：清空旧局面后按包围盒居中放置。</summary>
        public void LoadGun()
        {
            if (HasBoard) Board.LoadPattern(LifePatterns.GosperGliderGun, "滑翔机枪");
        }

        /// <summary>切换暂停状态；暂停只停固定节拍，单步推进不受限。</summary>
        public void SetPaused(bool paused)
        {
            if (HasBoard) Board.SetPaused(paused);
        }

        /// <summary>单步推进一代（N 键）；暂停时也可手动推进。</summary>
        public void StepOnce()
        {
            if (HasBoard) Board.Step();
        }

        /// <summary>模拟入口：按固定节拍推进棋局，暂停时不推进并清空累积。供测试与回放使用。</summary>
        public void Step(float deltaTime)
        {
            if (!HasBoard || Board.IsPaused)
            {
                _accumulator = 0f;
                return;
            }

            _accumulator += deltaTime;
            float interval = LifeRules.StepSeconds;
            while (_accumulator >= interval)
            {
                _accumulator -= interval;
                Board.Step();
            }
        }

        /// <summary>采集本帧按键：空格暂停/继续、N 单步、C 清空、P 载入滑翔机枪、R 随机重撒。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) SetPaused(!Board.IsPaused);
            if (Input.GetKeyDown(KeyCode.N)) StepOnce();
            if (Input.GetKeyDown(KeyCode.C)) Clear();
            if (Input.GetKeyDown(KeyCode.P)) LoadGun();
            if (Input.GetKeyDown(KeyCode.R)) Randomize();
        }

        private void Update()
        {
            if (!HasBoard) return;

            ReadInput();
            Step(Time.deltaTime);
        }
    }
}
