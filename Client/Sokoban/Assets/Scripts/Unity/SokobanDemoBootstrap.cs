using Lyking.Sokoban.Core;
using UnityEngine;

namespace Lyking.Sokoban.Unity
{
    /// <summary>
    /// 推箱子演示引导：装配核心棋局、采集按键并直接调用离散回合制的核心方法。
    /// 方向键或 WASD 走一步，R 重玩当前关，N 下一关（仅当前关已通关时核心接受），Z 撤销。
    /// 输入读取与核心模拟分离，核心方法即测试入口。
    /// </summary>
    public sealed class SokobanDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private SokobanDemoPanel? panel;

        private SokobanPuzzle? _puzzle;

        public SokobanPuzzle Puzzle => _puzzle ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasPuzzle => _puzzle is not null;

        public SokobanDemoPanel? Panel => panel;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SokobanDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态补建，保证单独加载场景也能完整呈现。
                panel = new GameObject("SokobanDemoPanel").AddComponent<SokobanDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>重建第一关棋局，保留面板绑定。</summary>
        public void StartGame()
        {
            _puzzle = new SokobanPuzzle(SokobanRules.Default, 0);
        }

        /// <summary>向指定方向走一步；被核心拒绝时无副作用。</summary>
        public void Move(SokobanDir dir)
        {
            if (HasPuzzle) Puzzle.Move(dir);
        }

        /// <summary>采集本帧方向键：方向键与 WASD 等效，取按下沿。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(SokobanDir.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(SokobanDir.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(SokobanDir.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(SokobanDir.Right);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (HasPuzzle) Puzzle.Reset();
                return;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                if (HasPuzzle) Puzzle.NextLevel();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (HasPuzzle) Puzzle.Undo();
                return;
            }

            ReadInput();
        }
    }
}
