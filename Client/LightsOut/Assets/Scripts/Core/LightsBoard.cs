using System;
using System.Collections.Generic;

namespace Lyking.LightsOut.Core
{
    /// <summary>
    /// 点灯核心棋局：size×size 网格，按下翻转自身与上下左右四邻，全部熄灭即胜利。
    /// 无 Unity 依赖、无内建时钟；谜题由注入种子的 <see cref="Random"/> 从全灭状态
    /// 反向随机按下 K 下生成（同格重复按下经异或后等价一次），因此必然可解且
    /// 不超过 K 步即可解，同种子谜题完全一致。
    /// </summary>
    public sealed class LightsBoard
    {
        private const int MaxEventLogs = 128;

        private readonly int _size;
        private readonly bool[,] _lights;
        private readonly bool[,] _initialLights;
        private readonly List<LightsOutEventLog> _events = new List<LightsOutEventLog>();

        public LightsBoard(int seed = 0, int size = LightsOutRules.BoardSize)
        {
            if (size < LightsOutRules.MinSize || size > LightsOutRules.MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size,
                    $"棋盘边长须在 {LightsOutRules.MinSize} 到 {LightsOutRules.MaxSize} 之间。");
            }

            _size = size;
            _lights = new bool[size, size];
            _initialLights = new bool[size, size];
            Generate(seed);
            Log(LightsOutEventType.Generated);
        }

        /// <summary>棋盘边长。</summary>
        public int Size => _size;

        /// <summary>当前亮灯的格子数，归零即解题成功。</summary>
        public int LightCount { get; private set; }

        /// <summary>已用步数：每次有效按下记 1 步。</summary>
        public int Moves { get; private set; }

        /// <summary>谜题号：换一题递增，重开新棋局从 1 重新计数。</summary>
        public int PuzzleNumber { get; private set; } = 1;

        /// <summary>生成当前谜题时反向按下的次数，谜题可解且不超过该步数。</summary>
        public int PuzzlePresses { get; private set; }

        /// <summary>生成当前谜题所用的种子。</summary>
        public int Seed { get; private set; }

        /// <summary>全部灯熄灭即解题成功；胜利后拒绝继续按下，重置后恢复。</summary>
        public bool IsSolved { get; private set; }

        public IReadOnlyList<LightsOutEventLog> Events => _events;

        /// <summary>当前亮灯布局的副本：true 为亮，修改副本不影响棋局。</summary>
        public bool[,] Lights => (bool[,])_lights.Clone();

        /// <summary>查询格位是否亮灯。</summary>
        public bool IsLit(int row, int column)
        {
            EnsureCell(row, column);
            return _lights[row, column];
        }

        /// <summary>按下格位：翻转自身与上下左右四邻（越界邻居跳过）。胜利后拒绝，返回是否成功。</summary>
        public bool Press(int row, int column)
        {
            EnsureCell(row, column);
            if (IsSolved) return false;

            int flipped = FlipCross(row, column);
            Moves++;
            Log(LightsOutEventType.Pressed, row, column, flipped);
            if (LightCount == 0)
            {
                IsSolved = true;
                Log(LightsOutEventType.Won, row, column);
            }

            return true;
        }

        /// <summary>重置回谜题初始布局：步数清零、胜利状态解除，可重新解题。</summary>
        public void Reset()
        {
            Array.Copy(_initialLights, _lights, _lights.Length);
            LightCount = CountLights();
            Moves = 0;
            IsSolved = false;
            Log(LightsOutEventType.Reset);
        }

        /// <summary>换一题：以新种子重新生成谜题，步数清零，谜题号递增。</summary>
        public void NewPuzzle(int seed)
        {
            PuzzleNumber++;
            Generate(seed);
            Log(LightsOutEventType.Regenerated);
        }

        /// <summary>从全灭状态反向随机按下 K 下生成谜题并记录初始布局；同种子结果完全一致。</summary>
        private void Generate(int seed)
        {
            var random = new Random(seed);
            Seed = seed;
            Array.Clear(_lights, 0, _lights.Length);
            LightCount = 0;

            PuzzlePresses = random.Next(LightsOutRules.MinPuzzlePresses, LightsOutRules.MaxPuzzlePresses + 1);
            for (int i = 0; i < PuzzlePresses; i++)
            {
                int row = random.Next(_size);
                int column = random.Next(_size);
                FlipCross(row, column);
            }

            Array.Copy(_lights, _initialLights, _lights.Length);
            Moves = 0;
            IsSolved = false;
        }

        /// <summary>翻转自身与四邻，返回实际翻转的格子数，越界邻居跳过。</summary>
        private int FlipCross(int row, int column)
        {
            int flipped = 0;
            flipped += Flip(row, column);
            flipped += Flip(row - 1, column);
            flipped += Flip(row + 1, column);
            flipped += Flip(row, column - 1);
            flipped += Flip(row, column + 1);
            return flipped;
        }

        /// <summary>翻转单格并维护亮灯计数，越界跳过，返回是否翻转。</summary>
        private int Flip(int row, int column)
        {
            if (row < 0 || row >= _size || column < 0 || column >= _size) return 0;

            if (_lights[row, column]) LightCount--;
            else LightCount++;
            _lights[row, column] = !_lights[row, column];
            return 1;
        }

        private int CountLights()
        {
            int count = 0;
            for (int row = 0; row < _size; row++)
            {
                for (int column = 0; column < _size; column++)
                {
                    if (_lights[row, column]) count++;
                }
            }

            return count;
        }

        private void EnsureCell(int row, int column)
        {
            if (row < 0 || row >= _size) throw new ArgumentOutOfRangeException(nameof(row), row, "行越界。");
            if (column < 0 || column >= _size) throw new ArgumentOutOfRangeException(nameof(column), column, "列越界。");
        }

        private void Log(LightsOutEventType type, int row = -1, int column = -1, int flippedCount = 0)
        {
            _events.Add(new LightsOutEventLog(Moves, type, row, column, flippedCount));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
