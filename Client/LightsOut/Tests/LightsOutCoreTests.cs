using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.LightsOut.Core;
using Xunit;

namespace Lyking.LightsOut.Core.Tests
{
    /// <summary>点灯核心规则测试：确定性谜题生成、十字翻转、异或叠加、胜利判定、重置换题与事件日志。</summary>
    public class LightsBoardTests
    {
        [Fact]
        public void 构造后谜题就绪且步数为零()
        {
            var board = new LightsBoard(seed: 42);

            Assert.Equal(LightsOutRules.BoardSize, board.Size);
            Assert.Equal(0, board.Moves);
            Assert.Equal(1, board.PuzzleNumber);
            Assert.False(board.IsSolved);
            Assert.True(board.LightCount > 0);
            Assert.InRange(board.PuzzlePresses, LightsOutRules.MinPuzzlePresses, LightsOutRules.MaxPuzzlePresses);
            Assert.Equal(LightsOutEventType.Generated, board.Events[0].Type);
        }

        [Fact]
        public void 相同种子产生相同谜题()
        {
            Assert.Equal(SnapshotOf(new LightsBoard(42)), SnapshotOf(new LightsBoard(42)));
            Assert.NotEqual(SnapshotOf(new LightsBoard(1)), SnapshotOf(new LightsBoard(2)));
        }

        [Fact]
        public void 点击中心翻转十字五格()
        {
            var board = new LightsBoard(seed: 42);
            bool[,] before = board.Lights;
            int center = LightsOutRules.BoardSize / 2;

            Assert.True(board.Press(center, center));
            Assert.Equal(5, board.Events[^1].FlippedCount);

            // 十字五格全部翻转，其余保持原状。
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    bool inCross = Math.Abs(row - center) + Math.Abs(column - center) <= 1;
                    Assert.Equal(inCross ? !before[row, column] : before[row, column], board.IsLit(row, column));
                }
            }
        }

        [Fact]
        public void 点击角落翻转三格()
        {
            var board = new LightsBoard(seed: 42);

            Assert.True(board.Press(0, 0));
            Assert.Equal(3, board.Events[^1].FlippedCount);
            Assert.Equal(1, board.Moves);
        }

        [Fact]
        public void 点击边格翻转四格()
        {
            var board = new LightsBoard(seed: 42);
            int middle = LightsOutRules.BoardSize / 2;

            Assert.True(board.Press(0, middle));
            Assert.Equal(4, board.Events[^1].FlippedCount);
        }

        [Fact]
        public void 同格两次按下回到原状()
        {
            var board = new LightsBoard(seed: 42);
            bool[,] before = board.Lights;

            Assert.True(board.Press(2, 2));
            Assert.True(board.Press(2, 2));

            // 翻转是异或：同格按两次等价没按。
            Assert.Equal(Flatten(before), Flatten(board.Lights));
            Assert.Equal(2, board.Moves);
        }

        [Fact]
        public void 多次按下叠加翻转()
        {
            var board = new LightsBoard(seed: 42);
            bool[,] before = board.Lights;

            // 相邻两格叠加：互相在对方十字内的格翻两次回原状，其余只翻一次。
            board.Press(1, 1);
            board.Press(1, 2);

            Assert.Equal(before[1, 1], board.IsLit(1, 1));
            Assert.Equal(before[1, 2], board.IsLit(1, 2));
            Assert.Equal(!before[0, 1], board.IsLit(0, 1));
            Assert.Equal(!before[2, 2], board.IsLit(2, 2));
            Assert.Equal(2, board.Moves);
        }

        [Fact]
        public void 谜题从全灭反向生成必然可解且不超过生成步数()
        {
            // 3x3 共 512 种按法，穷举找最小解：必然存在且不超过生成时的反向按下次数。
            var board = new LightsBoard(seed: 123, size: 3);
            var solution = BruteForceMinimalSolution(board);

            Assert.NotNull(solution);
            Assert.True(solution!.Count <= board.PuzzlePresses);
        }

        [Fact]
        public void 全部熄灭即胜利并记录事件()
        {
            var board = new LightsBoard(seed: 123, size: 3);
            var solution = BruteForceMinimalSolution(board);
            Assert.NotNull(solution);

            foreach (var (row, column) in solution!) Assert.True(board.Press(row, column));

            Assert.True(board.IsSolved);
            Assert.Equal(0, board.LightCount);
            Assert.Equal(LightsOutEventType.Won, board.Events[^1].Type);
        }

        [Fact]
        public void 胜利后拒绝继续按下()
        {
            var board = new LightsBoard(seed: 123, size: 3);
            var solution = BruteForceMinimalSolution(board);
            Assert.NotNull(solution);
            foreach (var (row, column) in solution!) board.Press(row, column);

            int moves = board.Moves;
            int eventCount = board.Events.Count;
            Assert.All(Enumerable.Range(0, 9), cell => Assert.False(board.Press(cell / 3, cell % 3)));

            Assert.Equal(moves, board.Moves);
            Assert.Equal(eventCount, board.Events.Count);
        }

        [Fact]
        public void 步数按有效按下统计()
        {
            var board = new LightsBoard(seed: 42);

            board.Press(0, 0);
            board.Press(4, 4);
            board.Press(2, 2);

            Assert.Equal(3, board.Moves);
            Assert.Equal(3, board.Events.Count(e => e.Type == LightsOutEventType.Pressed));
        }

        [Fact]
        public void 重置回谜题初始布局()
        {
            var board = new LightsBoard(seed: 42);
            bool[,] initial = board.Lights;

            board.Press(2, 2);
            board.Press(0, 0);
            Assert.Equal(2, board.Moves);

            board.Reset();

            Assert.Equal(Flatten(initial), Flatten(board.Lights));
            Assert.Equal(0, board.Moves);
            Assert.False(board.IsSolved);
            Assert.Equal(LightsOutEventType.Reset, board.Events[^1].Type);
        }

        [Fact]
        public void 胜利后重置可重新解题()
        {
            var board = new LightsBoard(seed: 123, size: 3);
            var solution = BruteForceMinimalSolution(board);
            Assert.NotNull(solution);
            foreach (var (row, column) in solution!) board.Press(row, column);
            Assert.True(board.IsSolved);

            board.Reset();

            Assert.False(board.IsSolved);
            Assert.True(board.Press(solution![0].Row, solution[0].Column));
        }

        [Fact]
        public void 换一题步数清零且谜题号递增()
        {
            var board = new LightsBoard(seed: 42);
            board.Press(2, 2);

            board.NewPuzzle(777);

            Assert.Equal(0, board.Moves);
            Assert.Equal(2, board.PuzzleNumber);
            Assert.Equal(777, board.Seed);
            Assert.False(board.IsSolved);
            Assert.Equal(LightsOutEventType.Regenerated, board.Events[^1].Type);
        }

        [Fact]
        public void 事件日志封顶一百二十八条()
        {
            var board = new LightsBoard(seed: 42);
            for (int i = 0; i < 200; i++) board.Press(2, 2);

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(LightsOutEventType.Pressed, board.Events[^1].Type);
            Assert.Equal(200, board.Events[^1].Move);
            Assert.Equal(200, board.Moves);
        }

        [Fact]
        public void 越界格位按下被拒绝()
        {
            var board = new LightsBoard(seed: 42);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Press(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Press(0, board.Size));
        }

        [Fact]
        public void 支持三到七的棋盘尺寸()
        {
            foreach (int size in new[] { 3, 4, 5, 6, 7 })
            {
                var board = new LightsBoard(seed: 9, size: size);
                Assert.Equal(size, board.Size);
                Assert.Equal(size * size, board.Lights.Length);
                Assert.InRange(board.PuzzlePresses, LightsOutRules.MinPuzzlePresses, LightsOutRules.MaxPuzzlePresses);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new LightsBoard(seed: 9, size: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LightsBoard(seed: 9, size: 8));
        }

        [Fact]
        public void 亮灯布局属性返回副本()
        {
            var board = new LightsBoard(seed: 42);
            bool[,] snapshot = board.Lights;
            bool litBefore = board.IsLit(2, 2);

            snapshot[2, 2] = !snapshot[2, 2];

            Assert.Equal(!litBefore, snapshot[2, 2]);
            Assert.Equal(litBefore, board.IsLit(2, 2));
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string Simulate(int seed)
            {
                var board = new LightsBoard(seed);
                for (int i = 0; i < 10; i++) board.Press(i % board.Size, (i * 3) % board.Size);
                board.Reset();
                board.NewPuzzle(seed + 1);
                for (int i = 0; i < 5; i++) board.Press(i % board.Size, (i * 2) % board.Size);
                return HashCode.Combine(board.Moves, board.LightCount, board.PuzzleNumber,
                    SnapshotOf(board), string.Join(';', board.Events.Select(e => e.ToString()))).ToString();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        private static string SnapshotOf(LightsBoard board) => string.Join(';', Flatten(board.Lights));

        private static IEnumerable<bool> Flatten(bool[,] grid)
        {
            for (int row = 0; row < grid.GetLength(0); row++)
            {
                for (int column = 0; column < grid.GetLength(1); column++)
                {
                    yield return grid[row, column];
                }
            }
        }

        /// <summary>穷举全部按法找最少按下的解；谜题由反向按下生成，解必然存在。</summary>
        private static List<(int Row, int Column)>? BruteForceMinimalSolution(LightsBoard board)
        {
            int size = board.Size;
            int cells = size * size;
            List<(int Row, int Column)>? best = null;

            for (int mask = 0; mask < 1 << cells; mask++)
            {
                var grid = board.Lights;
                var presses = new List<(int Row, int Column)>();
                for (int cell = 0; cell < cells; cell++)
                {
                    if ((mask & (1 << cell)) == 0) continue;

                    int row = cell / size;
                    int column = cell % size;
                    ToggleCross(grid, size, row, column);
                    presses.Add((row, column));
                }

                if (AllOff(grid) && (best == null || presses.Count < best.Count)) best = presses;
            }

            return best;
        }

        /// <summary>在亮灯副本上模拟一次按下：翻转自身与四邻，越界邻居跳过。</summary>
        private static void ToggleCross(bool[,] grid, int size, int row, int column)
        {
            Toggle(grid, size, row, column);
            Toggle(grid, size, row - 1, column);
            Toggle(grid, size, row + 1, column);
            Toggle(grid, size, row, column - 1);
            Toggle(grid, size, row, column + 1);
        }

        private static void Toggle(bool[,] grid, int size, int row, int column)
        {
            if (row < 0 || row >= size || column < 0 || column >= size) return;

            grid[row, column] = !grid[row, column];
        }

        private static bool AllOff(bool[,] grid)
        {
            foreach (bool light in grid)
            {
                if (light) return false;
            }

            return true;
        }
    }
}
