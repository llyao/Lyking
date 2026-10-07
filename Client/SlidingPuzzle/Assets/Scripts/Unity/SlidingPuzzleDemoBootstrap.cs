using System;
using Lyking.SlidingPuzzle.Core;
using UnityEngine;

namespace Lyking.SlidingPuzzle.Unity
{
    /// <summary>
    /// 数字华容道演示引导：装配核心棋局并按帧采集按键直接调用离散滑动的核心方法。
    /// 方向键或 WASD 表示把该方向一侧的相邻滑块推入空位——按 ← 把空位右侧的滑块左移（空位等效向右移），
    /// 与核心 <see cref="SlideDir"/> 语义一致；数字键 3/4 切换 3x3/4x4 棋盘，Z 撤销上一步，R 键以新种子重开。
    /// 输入读取与核心模拟分离，<see cref="Move"/> 与 <see cref="SlideTile"/> 即测试入口。
    /// </summary>
    public sealed class SlidingPuzzleDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private SlidingPuzzleDemoPanel? panel;

        private SlidingBoard? _board;
        private int _size = PuzzleRules.DefaultSize;
        private float _elapsedSeconds;

        public SlidingBoard Board => _board ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasBoard => _board is not null;

        public SlidingPuzzleDemoPanel? Panel => panel;

        /// <summary>当前棋盘边长（3 或 4）。</summary>
        public int Size => _size;

        /// <summary>本局已用秒数，还原成功后停止累计。</summary>
        public float ElapsedSeconds => _elapsedSeconds;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<SlidingPuzzleDemoPanel>();
            if (panel == null)
            {
                // 场景缺少面板物体时动态创建，保证演示始终可见。
                panel = new GameObject("SlidingPuzzlePanel").AddComponent<SlidingPuzzleDemoPanel>();
            }

            StartGame();
            if (panel != null) panel.Bind(this);
        }

        /// <summary>重开一局：按当前边长重建棋局并以当前时间作种子打乱，保留面板绑定。</summary>
        public void StartGame()
        {
            _board = new SlidingBoard(_size);
            Board.Shuffle(Environment.TickCount);
            _elapsedSeconds = 0f;
        }

        /// <summary>切换棋盘边长（仅支持 3 与 4，其他值忽略）并以新种子重开。</summary>
        public void SetSize(int size)
        {
            if (size < PuzzleRules.MinSize || size > PuzzleRules.MaxSize) return;
            _size = size;
            StartGame();
        }

        /// <summary>向指定方向滑动一步；空位旁无滑块或已还原时由核心拒绝。</summary>
        public bool Move(SlideDir dir) => HasBoard && Board.Move(dir);

        /// <summary>把与空位同行/同列的滑块一次滑入空位（面板鼠标点击入口），返回滑入块数。</summary>
        public int SlideTile(int tileIndex) => HasBoard ? Board.SlideTile(tileIndex) : 0;

        /// <summary>采集本帧按键：数字键 3/4 切换棋盘尺寸；方向键与 WASD 等效，取按下沿。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetSize(PuzzleRules.MinSize);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SetSize(PuzzleRules.MaxSize);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(SlideDir.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(SlideDir.Down);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(SlideDir.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(SlideDir.Right);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (HasBoard) Board.Undo();
                return;
            }

            ReadInput();

            if (HasBoard && !Board.IsSolved) _elapsedSeconds += Time.deltaTime;
        }
    }
}
