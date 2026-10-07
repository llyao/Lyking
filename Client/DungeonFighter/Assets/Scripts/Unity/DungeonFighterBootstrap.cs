using Lyking.DungeonFighter.Core;
using UnityEngine;

namespace Lyking.DungeonFighter.Unity
{
    /// <summary>
    /// 地下城格斗演示引导：装配核心对局、采集按键并按帧驱动 Tick，R 键重开。
    /// 输入读取与核心模拟分离，<see cref="Step"/> 提供给测试与回放直接驱动。
    /// </summary>
    public sealed class DungeonFighterBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonFighterPanel? panel;

        private DungeonGame? _game;

        public DungeonGame Game => _game ?? throw new System.InvalidOperationException("对局尚未创建。");

        public bool HasGame => _game is not null;

        public DungeonFighterPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<DungeonFighterPanel>();
            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：重建核心对局，保留面板绑定。</summary>
        public void StartGame()
        {
            _game = new DungeonGame();
        }

        /// <summary>模拟入口：直接推进核心一步，供测试与回放使用。</summary>
        public void Step(float deltaTime, in PlayerInputFrame input)
        {
            Game.Tick(deltaTime, input);
        }

        /// <summary>采集本帧按键；移动取持续键，动作取按下沿。</summary>
        public PlayerInputFrame ReadInput()
        {
            float moveX = 0f;
            float moveY = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveY += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveY -= 1f;

            return new PlayerInputFrame
            {
                MoveX = moveX,
                MoveY = moveY,
                Jump = Input.GetKeyDown(KeyCode.Space),
                Attack = Input.GetKeyDown(KeyCode.J),
                SkillUpper = Input.GetKeyDown(KeyCode.K),
                SkillWave = Input.GetKeyDown(KeyCode.L),
            };
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (!HasGame) return;
            Game.Tick(Time.deltaTime, ReadInput());
        }
    }
}
