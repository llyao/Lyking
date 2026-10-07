using System;
using System.Collections.Generic;

namespace Lyking.FloodIt.Core
{
    /// <summary>
    /// 色彩淹没核心棋局：14x14 六色网格，无 Unity 依赖、无内建时钟。
    /// 构造时以注入种子的 <see cref="Random"/> 逐格取色，同种子布局完全一致。
    /// 淹没区是从左上角出发的同色连通块；每次有效选色把淹没区整体改成所选颜色，
    /// 再重算左上角连通块作为新的淹没区；与淹没区同色的选择被拒绝且不计步。
    /// </summary>
    public sealed class FloodBoard
    {
        private const int MaxEventLogs = 128;

        private readonly int[,] _colors;
        private readonly int[,] _initialColors;
        private readonly bool[,] _flooded;
        private readonly List<FloodItEventLog> _events = new List<FloodItEventLog>();

        public FloodBoard(int seed = 0, int size = FloodItRules.Size)
        {
            if (size < FloodItRules.MinBoardSize || size > FloodItRules.MaxBoardSize)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size,
                    $"棋盘边长须在 {FloodItRules.MinBoardSize} 到 {FloodItRules.MaxBoardSize} 之间。");
            }

            Size = size;
            _colors = new int[size, size];
            _initialColors = new int[size, size];
            _flooded = new bool[size, size];
            Generate(seed);
        }

        /// <summary>棋盘边长，行数与列数相同。</summary>
        public int Size { get; }

        public int Rows => Size;

        public int Columns => Size;

        /// <summary>格子总数：行数 × 列数。</summary>
        public int CellCount => Size * Size;

        public int ColorCount => FloodItRules.ColorCount;

        public int MaxMoves => FloodItRules.MaxMoves;

        /// <summary>当前棋题种子，换题后更新。</summary>
        public int Seed { get; private set; }

        /// <summary>已用步数：每次有效选色记 1 步。</summary>
        public int Steps { get; private set; }

        /// <summary>剩余步数，归零且未全盘同色即失败。</summary>
        public int RemainingMoves => MaxMoves - Steps;

        /// <summary>已淹没格数，全盘同色时等于 <see cref="CellCount"/>。</summary>
        public int FloodedCount { get; private set; }

        /// <summary>全盘同色即胜利。</summary>
        public bool IsWon { get; private set; }

        /// <summary>步数用尽且未全盘同色即失败。</summary>
        public bool IsLost { get; private set; }

        public IReadOnlyList<FloodItEventLog> Events => _events;

        /// <summary>读取格位颜色编号（0 到 ColorCount-1）。</summary>
        public int GetColor(int row, int column)
        {
            CheckRange(row, column);
            return _colors[row, column];
        }

        /// <summary>格位是否属于当前淹没区（从左上角出发的同色连通块）。</summary>
        public bool IsFlooded(int row, int column)
        {
            CheckRange(row, column);
            return _flooded[row, column];
        }

        /// <summary>整盘颜色快照，返回副本，修改副本不影响棋局。</summary>
        public int[,] Colors
        {
            get
            {
                var snapshot = new int[Size, Size];
                Array.Copy(_colors, snapshot, _colors.Length);
                return snapshot;
            }
        }

        /// <summary>按种子逐格取色重建棋题：步数、终局状态与淹没区全部回到开局。</summary>
        public void Generate(int seed)
        {
            var random = new Random(seed);
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    _colors[row, column] = random.Next(ColorCount);
                }
            }

            Seed = seed;
            Steps = 0;
            IsWon = false;
            IsLost = false;
            Array.Copy(_colors, _initialColors, _colors.Length);
            RecomputeFlooded();
            Log(new FloodItEventLog(Steps, FloodItEventType.Generated, -1, 0));
        }

        /// <summary>换一题：以新种子重建棋盘，步数清零。</summary>
        public void NewPuzzle(int seed) => Generate(seed);

        /// <summary>重置回当前棋题的初始布局，步数与终局状态清零。</summary>
        public void Reset()
        {
            Array.Copy(_initialColors, _colors, _colors.Length);
            Steps = 0;
            IsWon = false;
            IsLost = false;
            RecomputeFlooded();
            Log(new FloodItEventLog(Steps, FloodItEventType.Reset, -1, 0));
        }

        /// <summary>
        /// 选择一种颜色：淹没区整体改成该色并重算连通块，返回是否生效。
        /// 与当前淹没色相同或终局后的选择被拒绝且不计步；非法颜色编号抛出越界异常。
        /// </summary>
        public bool Choose(int color)
        {
            if (color < 0 || color >= ColorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(color), color,
                    $"颜色编号须在 0 到 {ColorCount - 1} 之间。");
            }

            if (IsWon || IsLost || color == _colors[0, 0])
            {
                Log(new FloodItEventLog(Steps, FloodItEventType.Rejected, color, 0));
                return false;
            }

            Steps++;
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (_flooded[row, column]) _colors[row, column] = color;
                }
            }

            int before = FloodedCount;
            RecomputeFlooded();
            Log(new FloodItEventLog(Steps, FloodItEventType.Chosen, color, FloodedCount - before));

            if (FloodedCount == CellCount)
            {
                IsWon = true;
                Log(new FloodItEventLog(Steps, FloodItEventType.Won, color, 0));
            }
            else if (RemainingMoves == 0)
            {
                IsLost = true;
                Log(new FloodItEventLog(Steps, FloodItEventType.Lost, color, 0));
            }

            return true;
        }

        /// <summary>从左上角 BFS 重算同色连通块作为淹没区，并更新淹没格数。</summary>
        private void RecomputeFlooded()
        {
            Array.Clear(_flooded, 0, _flooded.Length);

            int color = _colors[0, 0];
            var queue = new Queue<(int Row, int Column)>();
            _flooded[0, 0] = true;
            FloodedCount = 1;
            queue.Enqueue((0, 0));
            while (queue.Count > 0)
            {
                (int row, int column) = queue.Dequeue();
                TryFlood(queue, color, row - 1, column);
                TryFlood(queue, color, row + 1, column);
                TryFlood(queue, color, row, column - 1);
                TryFlood(queue, color, row, column + 1);
            }
        }

        /// <summary>邻居与左上角同色且未淹没时并入淹没区，越界邻居跳过。</summary>
        private void TryFlood(Queue<(int Row, int Column)> queue, int color, int row, int column)
        {
            if (row < 0 || row >= Size || column < 0 || column >= Size) return;
            if (_flooded[row, column] || _colors[row, column] != color) return;

            _flooded[row, column] = true;
            FloodedCount++;
            queue.Enqueue((row, column));
        }

        private void CheckRange(int row, int column)
        {
            if (row < 0 || row >= Size || column < 0 || column >= Size)
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, "格位越界。");
            }
        }

        private void Log(FloodItEventLog log)
        {
            _events.Add(log);
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
