using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.WaterSort.Core;
using Xunit;

namespace Lyking.WaterSort.Core.Tests
{
    /// <summary>水体排序核心规则测试：生成守恒、可解性、倒水、拒绝、撤销、重置、通关与求解器。</summary>
    public class WaterSortPuzzleTests
    {
        [Fact]
        public void 相同种子生成完全一致的谜题()
        {
            var first = new WaterSortPuzzle(7);
            var second = new WaterSortPuzzle(7);

            Assert.Equal(first.TubeCount, second.TubeCount);
            Assert.Equal(first.Seed, second.Seed);
            Assert.Equal(first.Tubes, second.Tubes);
            Assert.Equal(first.Moves, second.Moves);
            Assert.Equal(first.Events.Count, second.Events.Count);
        }

        [Fact]
        public void 不同种子生成不同布局()
        {
            var first = new WaterSortPuzzle(0);
            var second = new WaterSortPuzzle(1);

            Assert.NotEqual(first.Tubes, second.Tubes);
        }

        [Fact]
        public void 初始布局颜色计数守恒且附带两个空瓶()
        {
            var puzzle = new WaterSortPuzzle(3);

            Assert.Equal(5, puzzle.ColorCount);
            Assert.Equal(WaterSortRules.TubeCapacity, puzzle.Capacity);
            Assert.Equal(7, puzzle.TubeCount);

            var counts = new Dictionary<int, int>();
            for (int i = 0; i < puzzle.TubeCount; i++)
            {
                var tube = puzzle.Tubes[i];
                Assert.True(tube.Length <= puzzle.Capacity, $"瓶 {i} 超过容量。");
                if (i >= puzzle.ColorCount)
                {
                    Assert.Empty(tube);
                }

                foreach (var color in tube)
                {
                    Assert.InRange(color, 1, puzzle.ColorCount);
                    counts[color] = counts.GetValueOrDefault(color) + 1;
                }
            }

            Assert.Equal(5, counts.Count);
            foreach (var count in counts.Values) Assert.Equal(WaterSortRules.TubeCapacity, count);
            Assert.False(puzzle.IsSolved);
            Assert.Equal(0, puzzle.Moves);
            Assert.False(puzzle.CanUndo);
            Assert.Equal(-1, puzzle.SelectedTube);
            Assert.Equal(WaterSortEventType.NewPuzzle, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 倒入空瓶合法且计入步数()
        {
            var puzzle = new WaterSortPuzzle(0);
            var before = puzzle.Tubes[0];
            int emptyTube = puzzle.ColorCount;

            Assert.True(puzzle.Pour(0, emptyTube));

            var source = puzzle.Tubes[0];
            var target = puzzle.Tubes[emptyTube];
            Assert.Equal(before.Length - TopRun(before), source.Length);
            Assert.Equal(TopRun(before), target.Length);
            Assert.Equal(before[^1], target[^1]);
            Assert.Equal(1, puzzle.Moves);
            Assert.True(puzzle.CanUndo);
            Assert.Equal(WaterSortEventType.Poured, puzzle.Events[^1].Type);
            Assert.Equal(TopRun(before), puzzle.Events[^1].LayerCount);
        }

        [Fact]
        public void 部分倒只倒顶部连续同色层()
        {
            var (seed, from, to) = FindPair(24, (p, f, t) =>
                p.Tubes[f].Length > 0 && TopRun(p.Tubes[f]) < p.Capacity && p.Tubes[t].Length == 0);

            var puzzle = new WaterSortPuzzle(seed);
            var before = puzzle.Tubes[from];
            int run = TopRun(before);

            Assert.True(puzzle.Pour(from, to));

            Assert.Equal(before.Length - run, puzzle.Tubes[from].Length);
            Assert.Single(puzzle.Tubes[to]);
            Assert.Equal(before[^1], puzzle.Tubes[to][^1]);
            Assert.Equal(1, puzzle.Moves);
        }

        [Fact]
        public void 不同色顶层倒入被拒绝()
        {
            // 先部分倒制造一只未满且非空的目标瓶，源瓶剩余顶层与该目标顶层必然异色。
            var (seed, from, to) = FindPair(24, (p, f, t) =>
                p.Tubes[f].Length > 0 && TopRun(p.Tubes[f]) < p.Tubes[f].Length && p.Tubes[t].Length == 0);

            var puzzle = new WaterSortPuzzle(seed);
            Assert.True(puzzle.Pour(from, to));

            var snapshot = CloneTubes(puzzle.Tubes);
            int moves = puzzle.Moves;

            Assert.False(puzzle.Pour(from, to));

            Assert.Equal(snapshot, puzzle.Tubes);
            Assert.Equal(moves, puzzle.Moves);
            Assert.Equal(WaterSortEventType.PourRejected, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 目标满瓶拒绝()
        {
            // 满瓶且顶层与源瓶同色：颜色条件满足，拒绝只能来自容量已满。
            var (seed, from, to) = FindPair(24, (p, f, t) =>
                p.Tubes[f].Length > 0 && p.Tubes[t].Length == p.Capacity && p.Tubes[f][^1] == p.Tubes[t][^1]);

            var puzzle = new WaterSortPuzzle(seed);

            Assert.False(puzzle.Pour(from, to));
            Assert.Equal(0, puzzle.Moves);
            Assert.Equal(WaterSortEventType.PourRejected, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 空瓶作为源被拒绝()
        {
            var puzzle = new WaterSortPuzzle(0);
            int emptyTube = puzzle.ColorCount;

            Assert.False(puzzle.Pour(emptyTube, 0));

            Assert.Empty(puzzle.Tubes[emptyTube]);
            Assert.Equal(0, puzzle.Moves);
            Assert.Equal(WaterSortEventType.PourRejected, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 选中自己拒绝且再次点击取消选中()
        {
            var puzzle = new WaterSortPuzzle(0);

            Assert.False(puzzle.Pour(2, 2));
            Assert.Equal(0, puzzle.Moves);
            Assert.Equal(WaterSortEventType.PourRejected, puzzle.Events[^1].Type);

            Assert.True(puzzle.Select(2));
            Assert.Equal(2, puzzle.SelectedTube);
            Assert.Equal(WaterSortEventType.Selected, puzzle.Events[^1].Type);

            Assert.True(puzzle.Select(2));
            Assert.Equal(-1, puzzle.SelectedTube);
            Assert.Equal(WaterSortEventType.Deselected, puzzle.Events[^1].Type);
            Assert.Equal(0, puzzle.Moves);
        }

        [Fact]
        public void 两段式选中倒入且被拒时保持选中()
        {
            var puzzle = new WaterSortPuzzle(0);

            // 满瓶作为目标必被拒绝，选中应保持以便换目标重试。
            Assert.True(puzzle.Select(0));
            Assert.False(puzzle.Select(1));
            Assert.Equal(0, puzzle.SelectedTube);
            Assert.Equal(0, puzzle.Moves);

            // 尾部空瓶必可接收，倒入成功后选中清空。
            int emptyTube = puzzle.ColorCount;
            Assert.True(puzzle.Select(emptyTube));
            Assert.Equal(-1, puzzle.SelectedTube);
            Assert.Equal(1, puzzle.Moves);
            Assert.Equal(WaterSortEventType.Poured, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 步数只统计成功倒水()
        {
            var puzzle = new WaterSortPuzzle(0);
            int emptyTube = puzzle.ColorCount;

            Assert.True(puzzle.Pour(0, emptyTube));
            Assert.False(puzzle.Pour(2, 3));
            Assert.True(puzzle.Pour(1, emptyTube + 1));

            Assert.Equal(2, puzzle.Moves);
            Assert.Equal(2, puzzle.Events.Count(e => e.Type == WaterSortEventType.Poured));
            Assert.Equal(1, puzzle.Events.Count(e => e.Type == WaterSortEventType.PourRejected));
        }

        [Fact]
        public void 撤销恢复全部层状态与步数()
        {
            var puzzle = new WaterSortPuzzle(0);
            var initial = CloneTubes(puzzle.Tubes);
            int emptyTube = puzzle.ColorCount;

            Assert.True(puzzle.Pour(0, emptyTube));
            Assert.True(puzzle.Pour(1, emptyTube + 1));
            Assert.Equal(2, puzzle.Moves);

            Assert.True(puzzle.Undo());
            Assert.Equal(1, puzzle.Moves);
            Assert.Equal(initial[1], puzzle.Tubes[1]);
            Assert.Empty(puzzle.Tubes[emptyTube + 1]);

            Assert.True(puzzle.Undo());
            Assert.Equal(0, puzzle.Moves);
            Assert.Equal(initial, puzzle.Tubes);
            Assert.False(puzzle.CanUndo);
            Assert.Equal(WaterSortEventType.Undone, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 无步可撤时撤销返回false()
        {
            var puzzle = new WaterSortPuzzle(0);

            Assert.False(puzzle.Undo());

            Assert.True(puzzle.Pour(0, puzzle.ColorCount));
            Assert.True(puzzle.Undo());
            Assert.False(puzzle.Undo());
        }

        [Fact]
        public void 重置恢复初始布局并清空撤销()
        {
            var puzzle = new WaterSortPuzzle(0);
            var initial = CloneTubes(puzzle.Tubes);

            Assert.True(puzzle.Pour(0, puzzle.ColorCount));
            Assert.True(puzzle.Pour(1, puzzle.ColorCount + 1));
            puzzle.Reset();

            Assert.Equal(initial, puzzle.Tubes);
            Assert.Equal(0, puzzle.Moves);
            Assert.False(puzzle.CanUndo);
            Assert.Equal(-1, puzzle.SelectedTube);
            Assert.Equal(WaterSortEventType.Reset, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 换一局以新种子重新生成并复位计数()
        {
            var puzzle = new WaterSortPuzzle(0);
            Assert.True(puzzle.Pour(0, puzzle.ColorCount));

            puzzle.NewPuzzle(123);
            var fresh = new WaterSortPuzzle(123);

            Assert.Equal(123, puzzle.Seed);
            Assert.Equal(fresh.Tubes, puzzle.Tubes);
            Assert.Equal(0, puzzle.Moves);
            Assert.False(puzzle.CanUndo);
            Assert.Equal(WaterSortEventType.NewPuzzle, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 越界倒水与选中被拒绝()
        {
            var puzzle = new WaterSortPuzzle(0);
            int events = puzzle.Events.Count;

            Assert.False(puzzle.Pour(-1, 0));
            Assert.False(puzzle.Pour(0, puzzle.TubeCount));
            Assert.False(puzzle.Select(-1));
            Assert.False(puzzle.Select(puzzle.TubeCount));

            Assert.Equal(-1, puzzle.SelectedTube);
            Assert.Equal(0, puzzle.Moves);
            Assert.Equal(events, puzzle.Events.Count);
        }

        [Fact]
        public void 构造校验颜色数越界抛出异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterSortPuzzle(0, WaterSortRules.MinColorCount - 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterSortPuzzle(0, WaterSortRules.MaxColorCount + 1));

            // 最少颜色数 2 可以正常构造：2 满瓶 + 2 空瓶。
            var puzzle = new WaterSortPuzzle(0, WaterSortRules.MinColorCount);
            Assert.Equal(2, puzzle.ColorCount);
            Assert.Equal(4, puzzle.TubeCount);
        }

        [Fact]
        public void 生成谜题必然可解且回放解法通关()
        {
            foreach (var seed in new[] { 0, 1, 2 })
            {
                var puzzle = new WaterSortPuzzle(seed);
                var solution = WaterSortSolver.FindSolution(puzzle.Tubes, puzzle.Capacity);
                Assert.True(solution is not null, $"种子 {seed} 生成的谜题无解。");
                Assert.InRange(solution!.Count, 1, 64);

                var replay = new WaterSortPuzzle(seed);
                foreach (var (from, to) in solution)
                {
                    Assert.True(replay.Pour(from, to), $"种子 {seed} 的解法在 瓶{from}->瓶{to} 处被拒绝。");
                }

                Assert.True(replay.IsSolved);
                Assert.Contains(replay.Events, e => e.Type == WaterSortEventType.Won);
            }
        }

        [Fact]
        public void 完成瓶拒绝作为目标且胜利后拒绝继续倒水()
        {
            var puzzle = new WaterSortPuzzle(0);
            var solution = WaterSortSolver.FindSolution(puzzle.Tubes, puzzle.Capacity);
            Assert.NotNull(solution);

            foreach (var (from, to) in solution!)
            {
                Assert.True(puzzle.Pour(from, to));
            }

            Assert.True(puzzle.IsSolved);
            int moves = puzzle.Moves;

            // 通关后的瓶子全部同色满，继续倒水应被拒绝且不改变步数。
            Assert.False(puzzle.Pour(0, 1));
            Assert.False(puzzle.Select(puzzle.ColorCount));
            Assert.Equal(moves, puzzle.Moves);
            Assert.Equal(WaterSortEventType.PourRejected, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 求解器识别最少步数与不可解局面()
        {
            // 只差一次倒入即可完成的局面。
            var almostDone = new[] { new[] { 1 }, new[] { 1, 1, 1 }, Array.Empty<int>(), Array.Empty<int>() };
            Assert.Equal(1, WaterSortSolver.MinPours(almostDone, WaterSortRules.TubeCapacity));
            Assert.True(WaterSortSolver.IsSolvable(almostDone, WaterSortRules.TubeCapacity));

            // 两瓶交错填充且没有空瓶：任何倒法都被拒，必然不可解。
            var stuck = new[] { new[] { 1, 2, 1, 2 }, new[] { 2, 1, 2, 1 } };
            Assert.False(WaterSortSolver.IsSolvable(stuck, WaterSortRules.TubeCapacity));
            Assert.Equal(-1, WaterSortSolver.MinPours(stuck, WaterSortRules.TubeCapacity));
            Assert.Null(WaterSortSolver.FindSolution(stuck, WaterSortRules.TubeCapacity));
        }

        [Fact]
        public void 连续被拒的事件日志封顶裁剪()
        {
            var puzzle = new WaterSortPuzzle(0);

            for (int i = 0; i < 300; i++) Assert.False(puzzle.Pour(0, 1));

            Assert.Equal(128, puzzle.Events.Count);
        }

        /// <summary>瓶顶部连续同色层的层数。</summary>
        private static int TopRun(int[] tube)
        {
            int run = 1;
            for (int i = tube.Length - 2; i >= 0 && tube[i] == tube[^1]; i--) run++;
            return run;
        }

        private static int[][] CloneTubes(int[][] tubes) =>
            tubes.Select(tube => (int[])tube.Clone()).ToArray();

        /// <summary>
        /// 在固定种子范围内寻找满足条件的 (源瓶, 目标瓶) 组合；
        /// 生成与扫描全部确定性，同一断言每次运行结果一致。
        /// </summary>
        private static (int Seed, int From, int To) FindPair(int maxSeed, Func<WaterSortPuzzle, int, int, bool> match)
        {
            for (int seed = 0; seed <= maxSeed; seed++)
            {
                var puzzle = new WaterSortPuzzle(seed);
                for (int from = 0; from < puzzle.TubeCount; from++)
                {
                    for (int to = 0; to < puzzle.TubeCount; to++)
                    {
                        if (from != to && match(puzzle, from, to)) return (seed, from, to);
                    }
                }
            }

            throw new InvalidOperationException($"在种子 0..{maxSeed} 内未找到满足条件的倒水组合。");
        }
    }
}
