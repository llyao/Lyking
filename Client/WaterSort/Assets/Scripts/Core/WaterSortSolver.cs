using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lyking.WaterSort.Core
{
    /// <summary>
    /// 水体排序求解器：广度优先搜索最少倒水次数与倒水序列。
    /// 状态键为"各瓶自下而上颜色序列按序拼接再整体排序"的规范串，空瓶与同构局面自动合并；
    /// 搜索跳过"整瓶同色倒入空瓶"这类无意义倒法，且空瓶彼此等价只尝试序号最小的一只。
    /// 供谜题生成验证可解性与测试断言。
    /// </summary>
    public static class WaterSortSolver
    {
        /// <summary>判断局面是否可解；在状态预算内未找到解法返回 false。</summary>
        public static bool IsSolvable(
            IReadOnlyList<IReadOnlyList<int>> tubes, int capacity, int maxStates = WaterSortRules.MaxSolverStates)
        {
            return FindSolution(tubes, capacity, maxStates) is not null;
        }

        /// <summary>求最少倒水次数；在状态预算内无解返回 -1。</summary>
        public static int MinPours(
            IReadOnlyList<IReadOnlyList<int>> tubes, int capacity, int maxStates = WaterSortRules.MaxSolverStates)
        {
            var solution = Search(tubes, capacity, maxStates);
            return solution?.Count ?? -1;
        }

        /// <summary>求一组 (源瓶, 目标瓶) 倒水序列；在状态预算内无解返回 null。</summary>
        public static IReadOnlyList<(int From, int To)>? FindSolution(
            IReadOnlyList<IReadOnlyList<int>> tubes, int capacity, int maxStates = WaterSortRules.MaxSolverStates)
        {
            return Search(tubes, capacity, maxStates);
        }

        /// <summary>
        /// 广度优先搜索：队列携带真实局面（瓶序号与输入一致，解法可直接回放），
        /// 规范状态键仅用于同构去重；访问表记录每个状态的父状态与触发倒水的瓶序号，命中胜利即回溯出倒水序列。
        /// </summary>
        private static List<(int From, int To)>? Search(
            IReadOnlyList<IReadOnlyList<int>> tubes, int capacity, int maxStates)
        {
            if (tubes is null) throw new ArgumentNullException(nameof(tubes));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "瓶容量必须为正。");
            if (tubes.Count == 0) throw new ArgumentException("局面至少要有一只瓶子。", nameof(tubes));

            var start = CloneTubes(tubes);
            if (IsWin(start, capacity)) return new List<(int From, int To)>();

            var startKey = Encode(start);
            var parents = new Dictionary<string, (string? Parent, int From, int To)> { [startKey] = (null, 0, 0) };
            var queue = new Queue<(List<List<int>> State, string Key)>();
            queue.Enqueue((start, startKey));

            while (queue.Count > 0)
            {
                var (state, key) = queue.Dequeue();
                for (int from = 0; from < state.Count; from++)
                {
                    var source = state[from];
                    if (source.Count == 0) continue;

                    int top = source[^1];
                    for (int to = 0; to < state.Count; to++)
                    {
                        if (to == from) continue;
                        var target = state[to];
                        if (target.Count >= capacity) continue;
                        if (target.Count > 0 && target[^1] != top) continue;
                        // 整瓶同色倒入空瓶不会开启任何新局面；空瓶彼此等价，只尝试序号最小的一只。
                        if (target.Count == 0 && (IsUniform(source) || FirstEmptyIndex(state) != to)) continue;

                        int amount = Math.Min(TopRunLength(source), capacity - target.Count);
                        var next = CloneState(state);
                        for (int i = 0; i < amount; i++)
                        {
                            next[from].RemoveAt(next[from].Count - 1);
                            next[to].Add(top);
                        }

                        var nextKey = Encode(next);
                        if (parents.ContainsKey(nextKey)) continue;
                        parents[nextKey] = (key, from, to);

                        if (IsWin(next, capacity))
                        {
                            var moves = new List<(int From, int To)>();
                            var cursor = nextKey;
                            while (cursor != startKey)
                            {
                                var (parentKey, pourFrom, pourTo) = parents[cursor];
                                moves.Add((pourFrom, pourTo));
                                cursor = parentKey!;
                            }

                            moves.Reverse();
                            return moves;
                        }

                        if (parents.Count >= maxStates) return null;
                        queue.Enqueue((next, nextKey));
                    }
                }
            }

            return null;
        }

        private static bool IsWin(IReadOnlyList<IReadOnlyList<int>> tubes, int capacity)
        {
            foreach (var tube in tubes)
            {
                if (tube.Count == 0) continue;
                if (tube.Count != capacity) return false;
                for (int i = 1; i < tube.Count; i++)
                {
                    if (tube[i] != tube[0]) return false;
                }
            }

            return true;
        }

        private static bool IsUniform(IReadOnlyList<int> tube)
        {
            for (int i = 1; i < tube.Count; i++)
            {
                if (tube[i] != tube[0]) return false;
            }

            return true;
        }

        /// <summary>源顶部连续同色层的层数。</summary>
        private static int TopRunLength(IReadOnlyList<int> tube)
        {
            int top = tube[^1];
            int run = 1;
            for (int i = tube.Count - 2; i >= 0 && tube[i] == top; i--) run++;
            return run;
        }

        private static int FirstEmptyIndex(IReadOnlyList<IReadOnlyList<int>> tubes)
        {
            for (int i = 0; i < tubes.Count; i++)
            {
                if (tubes[i].Count == 0) return i;
            }

            return -1;
        }

        /// <summary>把局面编码为规范状态键：各瓶自下而上以逗号拼接（空瓶为空串），瓶序按字典序排序后以竖线相连。</summary>
        private static string Encode(IReadOnlyList<IReadOnlyList<int>> tubes)
        {
            var parts = new string[tubes.Count];
            for (int i = 0; i < tubes.Count; i++)
            {
                var builder = new System.Text.StringBuilder();
                var tube = tubes[i];
                for (int j = 0; j < tube.Count; j++)
                {
                    if (j > 0) builder.Append(',');
                    builder.Append(tube[j].ToString(CultureInfo.InvariantCulture));
                }

                parts[i] = builder.ToString();
            }

            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static List<List<int>> CloneTubes(IReadOnlyList<IReadOnlyList<int>> tubes)
        {
            var clone = new List<List<int>>(tubes.Count);
            foreach (var tube in tubes) clone.Add(new List<int>(tube));
            return clone;
        }

        private static List<List<int>> CloneState(List<List<int>> state)
        {
            var clone = new List<List<int>>(state.Count);
            foreach (var tube in state) clone.Add(new List<int>(tube));
            return clone;
        }
    }
}
