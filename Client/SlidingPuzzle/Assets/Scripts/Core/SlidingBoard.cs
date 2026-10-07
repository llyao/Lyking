using System;
using System.Collections.Generic;

namespace Lyking.SlidingPuzzle.Core
{
    /// <summary>
    /// 数字华容道核心棋局：N x N 网格按行优先存放编号 1..N*N-1 的滑块，0 表示空位。
    /// 无 Unity 依赖、无内建时钟；打乱用注入种子从已解状态反向随机行走（避免立即回退上一步），
    /// 同种子布局完全一致，且由构造保证打乱后必可在不超过打乱步数内还原。
    /// 方向语义见 <see cref="SlideDir"/>：<see cref="SlideDir.Left"/> 表示空位右侧的滑块向左滑入空位。
    /// 已解状态（含初始局面与还原成功后）拒绝继续滑动。
    /// </summary>
    public sealed class SlidingBoard
    {
        private const int MaxEventLogs = 128;

        private static readonly SlideDir[] AllDirections = { SlideDir.Up, SlideDir.Down, SlideDir.Left, SlideDir.Right };

        private readonly List<SlideDir> _undoDirections = new List<SlideDir>();
        private readonly List<SlidingPuzzleEventLog> _events = new List<SlidingPuzzleEventLog>();
        private int[] _tiles = Array.Empty<int>();
        private int _size = PuzzleRules.DefaultSize;
        private int _blankIndex;

        /// <summary>以指定边长创建已解棋局：编号 1..N*N-1 顺序排列，空位在右下角。</summary>
        public SlidingBoard(int size = PuzzleRules.DefaultSize) => Reset(size);

        /// <summary>棋盘边长（3 或 4）。</summary>
        public int Size => _size;

        /// <summary>格位总数：边长 × 边长。</summary>
        public int TileCount => _tiles.Length;

        /// <summary>已用步数：每次成功滑入一枚滑块记 1 步（一次多格滑动按滑入块数累计），撤销回退。</summary>
        public int Steps { get; private set; }

        /// <summary>是否已还原：编号 1..N*N-1 顺序排列且空位在右下角。</summary>
        public bool IsSolved
        {
            get
            {
                if (_blankIndex != TileCount - 1) return false;
                for (int i = 0; i < TileCount - 1; i++)
                {
                    if (_tiles[i] != i + 1) return false;
                }

                return true;
            }
        }

        /// <summary>当前布局快照（行优先，0 表示空位），返回副本以防外部改动。</summary>
        public int[] Tiles => (int[])_tiles.Clone();

        public IReadOnlyList<SlidingPuzzleEventLog> Events => _events;

        /// <summary>该格上的滑块是否已归位（编号等于格位序号 +1；空位的归位格为右下角）。</summary>
        public bool IsTileHome(int index)
        {
            if (index < 0 || index >= TileCount) throw new ArgumentOutOfRangeException(nameof(index), index, "格位越界。");
            return index == TileCount - 1 ? _tiles[index] == 0 : _tiles[index] == index + 1;
        }

        /// <summary>
        /// 把指定方向一侧紧邻空位的滑块沿该方向推入空位：
        /// <see cref="SlideDir.Left"/> 表示空位右侧的滑块向左滑入空位（空位等效向右移动一格），其余方向同理。
        /// 方向无相邻滑块或棋局已还原时拒绝并记录事件，返回是否成功。
        /// </summary>
        public bool Move(SlideDir dir)
        {
            if (dir == SlideDir.None) return false;

            int tileIndex = MovableIndexOf(dir);
            if (IsSolved || tileIndex < 0)
            {
                Log(SlidingPuzzleEventType.SlideRejected, dir, -1);
                return false;
            }

            int tile = SlideRaw(tileIndex);
            Steps++;
            _undoDirections.Add(dir);
            Log(SlidingPuzzleEventType.Slid, dir, tile);
            if (IsSolved) Log(SlidingPuzzleEventType.Won, dir, tile);
            return true;
        }

        /// <summary>
        /// 把与空位同行或同列的滑块一次滑入空位，中间滑块依次跟随（对应面板鼠标点击远端滑块），
        /// 每滑入一枚滑块记 1 步，返回实际滑入的滑块数；指向空位、与空位不共线或棋局已还原时返回 0。
        /// </summary>
        public int SlideTile(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= TileCount) throw new ArgumentOutOfRangeException(nameof(tileIndex), tileIndex, "格位越界。");

            int tile = _tiles[tileIndex];
            if (tile == 0 || IsSolved || !TryGetSlideDirection(tileIndex, out SlideDir dir))
            {
                Log(SlidingPuzzleEventType.SlideRejected, SlideDir.None, tile > 0 ? tile : -1);
                return 0;
            }

            int count = 0;
            while (_blankIndex != tileIndex)
            {
                // 每次滑入的是紧邻空位、位于行进方向上的那枚滑块。
                int moved = SlideRaw(MovableIndexOf(dir));
                Steps++;
                _undoDirections.Add(dir);
                Log(SlidingPuzzleEventType.Slid, dir, moved);
                count++;
            }

            if (IsSolved) Log(SlidingPuzzleEventType.Won, dir, tile);
            return count;
        }

        /// <summary>撤销上一次成功滑动：滑块按相反方向滑回，步数 -1。无步可撤返回 false。</summary>
        public bool Undo()
        {
            if (_undoDirections.Count == 0) return false;

            SlideDir dir = _undoDirections[^1];
            _undoDirections.RemoveAt(_undoDirections.Count - 1);
            // 刚被滑入的滑块必然紧邻空位，其反方向滑动一定合法。
            int moved = SlideRaw(MovableIndexOf(dir.Opposite()));
            Steps--;
            Log(SlidingPuzzleEventType.Undone, dir, moved);
            return true;
        }

        /// <summary>重置回当前尺寸的已解状态并清空撤销记录。</summary>
        public void Reset() => Reset(_size);

        /// <summary>切换棋盘边长并重置回该尺寸的已解状态，用于 3x3/4x4 切换。</summary>
        public void Reset(int size)
        {
            BuildSolved(PuzzleRules.ValidateSize(size));
            Log(SlidingPuzzleEventType.Reset, SlideDir.None, -1);
        }

        /// <summary>
        /// 打乱：先重置回已解状态，再用注入种子随机反向行走 <see cref="PuzzleRules.ShuffleStepsFor"/> 步，
        /// 每步避免立即回退上一步。返回实际行走的方向序列——逆序执行其相反方向必回到已解状态，
        /// 谜题可解性由构造保证。打乱后步数清零、撤销记录清空。
        /// </summary>
        public IReadOnlyList<SlideDir> Shuffle(int seed)
        {
            BuildSolved(_size);
            var random = new Random(seed);
            int shuffleSteps = PuzzleRules.ShuffleStepsFor(_size);
            var path = new List<SlideDir>(shuffleSteps);
            SlideDir previous = SlideDir.None;
            for (int i = 0; i < shuffleSteps; i++)
            {
                SlideDir dir = NextShuffleDirection(random, previous);
                SlideRaw(MovableIndexOf(dir));
                path.Add(dir);
                previous = dir;
            }

            Log(SlidingPuzzleEventType.Shuffled, SlideDir.None, -1);
            return path;
        }

        /// <summary>该方向上紧邻空位的滑块所在格位；方向无相邻滑块时返回 -1。</summary>
        private int MovableIndexOf(SlideDir dir)
        {
            int row = _blankIndex / _size;
            int column = _blankIndex % _size;
            return dir switch
            {
                SlideDir.Left => column + 1 < _size ? _blankIndex + 1 : -1,
                SlideDir.Right => column - 1 >= 0 ? _blankIndex - 1 : -1,
                SlideDir.Up => row + 1 < _size ? _blankIndex + _size : -1,
                SlideDir.Down => row - 1 >= 0 ? _blankIndex - _size : -1,
                _ => -1,
            };
        }

        /// <summary>与空位同行或同列时，给出把该格滑块推向空位的方向；否则返回 false。</summary>
        private bool TryGetSlideDirection(int tileIndex, out SlideDir dir)
        {
            int row = tileIndex / _size;
            int column = tileIndex % _size;
            int blankRow = _blankIndex / _size;
            int blankColumn = _blankIndex % _size;
            if (row == blankRow) dir = column < blankColumn ? SlideDir.Right : SlideDir.Left;
            else if (column == blankColumn) dir = row < blankRow ? SlideDir.Down : SlideDir.Up;
            else dir = SlideDir.None;
            return dir != SlideDir.None;
        }

        /// <summary>打乱行走的一步：在合法方向中随机选取，排除会立即回退上一步的方向。</summary>
        private SlideDir NextShuffleDirection(Random random, SlideDir previous)
        {
            var legal = new List<SlideDir>(AllDirections.Length);
            var forward = new List<SlideDir>(AllDirections.Length);
            foreach (SlideDir dir in AllDirections)
            {
                if (MovableIndexOf(dir) < 0) continue;
                legal.Add(dir);
                if (dir != previous.Opposite()) forward.Add(dir);
            }

            // 空位至少有两个相邻格且回退方向总在其中，forward 实际不会为空，legal 仅作兜底。
            var pool = forward.Count > 0 ? forward : legal;
            return pool[random.Next(pool.Count)];
        }

        /// <summary>把 tileIndex 格上的滑块滑入空位（调用方保证该格紧邻空位），返回滑块编号。</summary>
        private int SlideRaw(int tileIndex)
        {
            int tile = _tiles[tileIndex];
            _tiles[_blankIndex] = tile;
            _tiles[tileIndex] = 0;
            _blankIndex = tileIndex;
            return tile;
        }

        /// <summary>重建指定尺寸的已解布局并清空步数与撤销记录。</summary>
        private void BuildSolved(int size)
        {
            _size = size;
            _tiles = new int[size * size];
            for (int i = 0; i < _tiles.Length - 1; i++) _tiles[i] = i + 1;
            _tiles[_tiles.Length - 1] = 0; // 空位固定在右下角，new 数组本为 0，此处显式写出约定
            _blankIndex = _tiles.Length - 1;
            Steps = 0;
            _undoDirections.Clear();
        }

        private void Log(SlidingPuzzleEventType type, SlideDir direction, int tile)
        {
            _events.Add(new SlidingPuzzleEventLog(Steps, type, direction, tile));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
