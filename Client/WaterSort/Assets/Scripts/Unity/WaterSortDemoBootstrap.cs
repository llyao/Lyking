using System;
using Lyking.WaterSort.Core;
using UnityEngine;

namespace Lyking.WaterSort.Unity
{
    /// <summary>
    /// 水体排序演示引导：装配核心棋局、采集按键并直接调用离散回合制的核心方法。
    /// 左右方向键或 A/D 移动选择光标，回车对光标所在瓶执行选中/倒入，数字键 1-8 直接选瓶，
    /// U 撤销，R 重开当前局。输入读取与核心模拟分离，核心方法即测试入口。
    /// </summary>
    public sealed class WaterSortDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private WaterSortDemoPanel? panel;

        private WaterSortPuzzle? _puzzle;
        private int _cursor;

        public WaterSortPuzzle Puzzle => _puzzle ?? throw new InvalidOperationException("棋局尚未创建。");

        public bool HasPuzzle => _puzzle is not null;

        public WaterSortDemoPanel? Panel => panel;

        /// <summary>选择光标所在的瓶序号；棋局未创建时停留在第一瓶。</summary>
        public int Cursor => HasPuzzle ? Mathf.Clamp(_cursor, 0, Puzzle.TubeCount - 1) : 0;

        private void Start()
        {
            if (panel == null) panel = FindObjectOfType<WaterSortDemoPanel>();
            if (panel == null)
            {
                // 场景未预置面板时动态补建，保证单独加载场景也能完整呈现。
                panel = new GameObject("WaterSortDemoPanel").AddComponent<WaterSortDemoPanel>();
            }

            StartGame();
            panel.Bind(this);
        }

        /// <summary>以系统启动刻度为种子重建谜题，保留面板绑定。</summary>
        public void StartGame()
        {
            _puzzle = new WaterSortPuzzle(Environment.TickCount);
            _cursor = 0;
        }

        /// <summary>两段式选瓶：第一次选中，选中其他瓶即倒入；序号越界或被核心拒绝时保持光标与选中。</summary>
        public void Select(int index)
        {
            if (!HasPuzzle || index < 0 || index >= Puzzle.TubeCount) return;

            Puzzle.Select(index);
            _cursor = index;
        }

        /// <summary>撤销上一次倒水；被核心拒绝时无副作用。</summary>
        public void Undo()
        {
            if (HasPuzzle) Puzzle.Undo();
        }

        /// <summary>重开当前局：回到谜题初始布局并清空撤销。</summary>
        public void ResetGame()
        {
            if (HasPuzzle) Puzzle.Reset();
        }

        /// <summary>向左或向右移动选择光标，越过两端循环。</summary>
        public void MoveCursor(int delta)
        {
            if (!HasPuzzle) return;

            int count = Puzzle.TubeCount;
            _cursor = ((_cursor + delta) % count + count) % count;
        }

        /// <summary>采集本帧选瓶按键：方向键与 A/D 移动光标，回车确认，数字键直接选瓶。</summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveCursor(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveCursor(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Select(_cursor);
            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                Undo();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetGame();
                return;
            }

            ReadInput();
        }
    }
}
