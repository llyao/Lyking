using System;
using System.Collections.Generic;

namespace Lyking.WaterSort.Core
{
    /// <summary>单次倒水的撤销记录：源瓶、目标瓶与倒入层数。</summary>
    internal readonly struct PourRecord
    {
        public PourRecord(int from, int to, int layerCount)
        {
            From = from;
            To = to;
            LayerCount = layerCount;
        }

        public int From { get; }

        public int To { get; }

        public int LayerCount { get; }
    }

    /// <summary>
    /// 水体排序核心棋局：确定性离散回合制状态机，无 Unity 依赖。
    /// 谜题由注入种子的 <see cref="Random"/> 把 N×4 层 Fisher-Yates 洗牌后依序装入 N 瓶并附 2 空瓶生成；
    /// 随机布局不保证可解，故生成后用 <see cref="WaterSortSolver"/> 验证，不可解则换种重试。
    /// <see cref="Select"/> 提供两段式"选中→倒入"，<see cref="Pour"/> 直接执行倒水：
    /// 源非空、目标未满且（目标为空或顶层同色）时，一次倒出源顶部连续同色层直到目标满或该色层倒完。
    /// 每次倒水入栈记录，<see cref="Undo"/> 恢复层数与步数；每瓶要么为空要么同色满即 <see cref="IsSolved"/>。
    /// </summary>
    public sealed class WaterSortPuzzle
    {
        private const int MaxEventLogs = 128;

        private readonly int _colorCount;
        private readonly List<int>[] _tubes;
        private readonly List<int>[] _initialTubes;
        private readonly List<PourRecord> _undoStack = new List<PourRecord>();
        private readonly List<WaterSortEventLog> _events = new List<WaterSortEventLog>();
        private int _selectedTube = -1;
        private int _moves;
        private int _seed;

        public WaterSortPuzzle(int seed = 0, int colorCount = WaterSortRules.DefaultColorCount)
        {
            WaterSortRules.ValidateColorCount(colorCount);
            _colorCount = colorCount;
            _tubes = new List<int>[TubeCount];
            _initialTubes = new List<int>[TubeCount];
            for (int i = 0; i < TubeCount; i++)
            {
                _tubes[i] = new List<int>(WaterSortRules.TubeCapacity);
                _initialTubes[i] = new List<int>(WaterSortRules.TubeCapacity);
            }

            Generate(seed);
            Log(WaterSortEventType.NewPuzzle, -1, -1, 0);
        }

        /// <summary>颜色总数，颜色编号为 1..N。</summary>
        public int ColorCount => _colorCount;

        /// <summary>每只瓶子的容量（颜色层数）。</summary>
        public int Capacity => WaterSortRules.TubeCapacity;

        /// <summary>总瓶数：N 只满瓶加 2 只空瓶。</summary>
        public int TubeCount => WaterSortRules.TotalTubes(_colorCount);

        /// <summary>生成当前谜题所用的种子。</summary>
        public int Seed => _seed;

        /// <summary>已完成的倒水次数（不含被拒绝的尝试）。</summary>
        public int Moves => _moves;

        /// <summary>两段式操作当前选中的瓶序号，未选中为 -1。</summary>
        public int SelectedTube => _selectedTube;

        /// <summary>是否还有可撤销的倒水。</summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>全部瓶子要么为空要么同色满即通关。</summary>
        public bool IsSolved
        {
            get
            {
                foreach (var tube in _tubes)
                {
                    if (tube.Count == 0) continue;
                    if (tube.Count != Capacity || !IsUniform(tube)) return false;
                }

                return true;
            }
        }

        public IReadOnlyList<WaterSortEventLog> Events => _events;

        /// <summary>当前各瓶自下而上的颜色层快照，修改副本不影响棋局。</summary>
        public int[][] Tubes
        {
            get
            {
                var snapshot = new int[TubeCount][];
                for (int i = 0; i < TubeCount; i++) snapshot[i] = _tubes[i].ToArray();
                return snapshot;
            }
        }

        /// <summary>
        /// 两段式选择：未选中时选中，再次点击同一瓶取消，已选中其他瓶则尝试倒入；
        /// 倒水被拒时保持选中便于换目标重试。胜利后或序号越界返回 false。
        /// </summary>
        public bool Select(int index)
        {
            if (index < 0 || index >= TubeCount || IsSolved) return false;

            if (_selectedTube == index)
            {
                _selectedTube = -1;
                Log(WaterSortEventType.Deselected, index, -1, 0);
                return true;
            }

            if (_selectedTube == -1)
            {
                _selectedTube = index;
                Log(WaterSortEventType.Selected, index, -1, 0);
                return true;
            }

            if (Pour(_selectedTube, index))
            {
                _selectedTube = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从源瓶向目标瓶倒水：源为空、目标已满、顶层异色、同瓶或越界时拒绝并记录事件；
        /// 成功则一次倒出源顶部连续同色层直到目标满或该色层倒完，步数 +1 并记录撤销，凑齐即记录通关。
        /// </summary>
        public bool Pour(int from, int to)
        {
            if (from < 0 || from >= TubeCount || to < 0 || to >= TubeCount) return false;
            if (from == to || IsSolved || _tubes[from].Count == 0 || _tubes[to].Count >= Capacity
                || (_tubes[to].Count > 0 && _tubes[to][^1] != _tubes[from][^1]))
            {
                Log(WaterSortEventType.PourRejected, from, to, 0);
                return false;
            }

            int amount = Math.Min(TopRunLength(_tubes[from]), Capacity - _tubes[to].Count);
            for (int i = 0; i < amount; i++)
            {
                _tubes[to].Add(_tubes[from][^1]);
                _tubes[from].RemoveAt(_tubes[from].Count - 1);
            }

            _undoStack.Add(new PourRecord(from, to, amount));
            _moves++;
            Log(WaterSortEventType.Poured, from, to, amount);
            if (IsSolved) Log(WaterSortEventType.Won, from, to, amount);
            return true;
        }

        /// <summary>撤销上一次倒水：按记录层数倒回源瓶并恢复步数。无步可撤返回 false。</summary>
        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;

            var record = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            for (int i = 0; i < record.LayerCount; i++)
            {
                _tubes[record.From].Add(_tubes[record.To][^1]);
                _tubes[record.To].RemoveAt(_tubes[record.To].Count - 1);
            }

            _selectedTube = -1;
            _moves--;
            Log(WaterSortEventType.Undone, record.From, record.To, record.LayerCount);
            return true;
        }

        /// <summary>重置当前谜题：回到初始布局并清空撤销栈与选中。</summary>
        public void Reset()
        {
            ClearProgress();
            Log(WaterSortEventType.Reset, -1, -1, 0);
        }

        /// <summary>换一局：以新种子重新生成谜题，步数、选中与撤销清零。</summary>
        public void NewPuzzle(int seed)
        {
            Generate(seed);
            Log(WaterSortEventType.NewPuzzle, -1, -1, 0);
        }

        /// <summary>
        /// 以注入种子生成可解谜题：洗牌装入后用求解器验证，不可解则以同一随机流派生的下一种子重试，
        /// 超过上限抛出异常；同一种子每次生成的布局完全一致。
        /// </summary>
        private void Generate(int seed)
        {
            _seed = seed;
            var random = new Random(seed);
            for (int attempt = 0; attempt < WaterSortRules.MaxGenerateAttempts; attempt++)
            {
                FillShuffled(random);
                if (WaterSortSolver.IsSolvable(_tubes, Capacity, WaterSortRules.MaxSolverStates))
                {
                    AdoptCurrentAsInitial();
                    return;
                }
            }

            throw new InvalidOperationException(
                $"种子 {seed} 在 {WaterSortRules.MaxGenerateAttempts} 次重试内未能生成可解谜题。");
        }

        /// <summary>把颜色 1..N 各 4 层 Fisher-Yates 洗牌后依序装入前 N 瓶，最后两瓶置空。</summary>
        private void FillShuffled(Random random)
        {
            var layers = new List<int>(_colorCount * WaterSortRules.TubeCapacity);
            for (int color = 1; color <= _colorCount; color++)
            {
                for (int i = 0; i < WaterSortRules.TubeCapacity; i++) layers.Add(color);
            }

            for (int i = layers.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (layers[i], layers[j]) = (layers[j], layers[i]);
            }

            for (int tube = 0; tube < _colorCount; tube++)
            {
                _tubes[tube].Clear();
                for (int i = 0; i < WaterSortRules.TubeCapacity; i++)
                {
                    _tubes[tube].Add(layers[tube * WaterSortRules.TubeCapacity + i]);
                }
            }

            for (int tube = _colorCount; tube < TubeCount; tube++) _tubes[tube].Clear();
        }

        /// <summary>把洗牌结果采纳为初始布局，并清空步数、选中与撤销栈。</summary>
        private void AdoptCurrentAsInitial()
        {
            for (int i = 0; i < TubeCount; i++)
            {
                _initialTubes[i].Clear();
                _initialTubes[i].AddRange(_tubes[i]);
            }

            ClearProgress();
        }

        /// <summary>从初始布局重建瓶层，并清空步数、选中与撤销栈。</summary>
        private void ClearProgress()
        {
            for (int i = 0; i < TubeCount; i++)
            {
                _tubes[i].Clear();
                _tubes[i].AddRange(_initialTubes[i]);
            }

            _selectedTube = -1;
            _moves = 0;
            _undoStack.Clear();
        }

        private static bool IsUniform(List<int> tube)
        {
            for (int i = 1; i < tube.Count; i++)
            {
                if (tube[i] != tube[0]) return false;
            }

            return true;
        }

        /// <summary>瓶顶部连续同色层的层数。</summary>
        private static int TopRunLength(List<int> tube)
        {
            int top = tube[^1];
            int run = 1;
            for (int i = tube.Count - 2; i >= 0 && tube[i] == top; i--) run++;
            return run;
        }

        private void Log(WaterSortEventType type, int fromTube, int toTube, int layerCount)
        {
            _events.Add(new WaterSortEventLog(_moves, type, fromTube, toTube, layerCount));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
