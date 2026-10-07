using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.FloodIt.Core;
using Xunit;

namespace Lyking.FloodIt.Core.Tests
{
    /// <summary>色彩淹没核心规则测试：确定性生成、初始淹没区、选色扩张、步数与胜负、重置换题与事件日志。</summary>
    public class FloodBoardTests
    {
        [Fact]
        public void 生成后棋盘就绪且颜色在六色范围内()
        {
            var board = new FloodBoard(seed: 42);

            Assert.Equal(FloodItRules.Rows, board.Rows);
            Assert.Equal(FloodItRules.Columns, board.Columns);
            Assert.Equal(FloodItRules.CellCount, board.CellCount);
            Assert.Equal(FloodItRules.MaxMoves, board.MaxMoves);
            Assert.Equal(0, board.Steps);
            Assert.Equal(FloodItRules.MaxMoves, board.RemainingMoves);
            Assert.True(board.FloodedCount >= 1);
            Assert.True(board.IsFlooded(0, 0));
            Assert.False(board.IsWon);
            Assert.False(board.IsLost);
            foreach (int color in board.Colors) Assert.InRange(color, 0, FloodItRules.ColorCount - 1);
            Assert.Equal(FloodItEventType.Generated, board.Events[0].Type);
        }

        [Fact]
        public void 固定种子逐格颜色与手算序列一致()
        {
            // new Random(42) 前 16 次 Next(6) 的手算序列，按行优先填充棋盘前两行。
            int[] expected = { 4, 0, 0, 3, 1, 1, 4, 3, 1, 4, 1, 1, 3, 1, 2, 1 };
            var board = new FloodBoard(seed: 42);

            for (int i = 0; i < expected.Length; i++)
            {
                int row = i / FloodItRules.Columns;
                int column = i % FloodItRules.Columns;
                Assert.Equal(expected[i], board.GetColor(row, column));
            }
        }

        [Fact]
        public void 相同种子产生相同棋盘()
        {
            Assert.Equal(SnapshotOf(new FloodBoard(42)), SnapshotOf(new FloodBoard(42)));
            Assert.NotEqual(SnapshotOf(new FloodBoard(1)), SnapshotOf(new FloodBoard(2)));
        }

        [Fact]
        public void 初始淹没区为左上角同色连通块()
        {
            var board = new FloodBoard(seed: 42);
            var expected = FloodMapOf(board.Colors);

            // 测试侧独立的深度优先连通计算与核心的 BFS 淹没区逐格一致。
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Assert.Equal(expected[row, column], board.IsFlooded(row, column));
                }
            }

            Assert.Equal(CountOf(expected), board.FloodedCount);
        }

        [Fact]
        public void 选色后淹没区按同色连通扩张()
        {
            var board = new FloodBoard(seed: 42);
            int before = board.FloodedCount;
            int pick = SmallestBorderColor(board);
            Assert.InRange(pick, 0, FloodItRules.ColorCount - 1);

            Assert.True(board.Choose(pick));

            Assert.Equal(1, board.Steps);
            Assert.True(board.FloodedCount > before);

            // 改色后的淹没区等于测试侧按新布局重算的左上角连通块。
            var expected = FloodMapOf(board.Colors);
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Assert.Equal(expected[row, column], board.IsFlooded(row, column));
                }
            }

            Assert.Equal(CountOf(expected), board.FloodedCount);
            Assert.Equal(FloodItEventType.Chosen, board.Events[^1].Type);
            Assert.Equal(pick, board.Events[^1].Color);
            Assert.Equal(board.FloodedCount - before, board.Events[^1].ExpandedCount);
        }

        [Fact]
        public void 步数与剩余步数随有效选色递减()
        {
            var board = new FloodBoard(seed: 42);

            for (int step = 1; step <= 3; step++)
            {
                Assert.True(board.Choose(SmallestValidColor(board)));
                Assert.Equal(step, board.Steps);
                Assert.Equal(FloodItRules.MaxMoves - step, board.RemainingMoves);
            }

            Assert.False(board.IsWon);
            Assert.False(board.IsLost);
            Assert.Equal(3, board.Events.Count(e => e.Type == FloodItEventType.Chosen));
        }

        [Fact]
        public void 同色选择被拒绝且不计步()
        {
            var board = new FloodBoard(seed: 42);
            int current = board.GetColor(0, 0);
            int floodedCount = board.FloodedCount;

            Assert.False(board.Choose(current));

            Assert.Equal(0, board.Steps);
            Assert.Equal(floodedCount, board.FloodedCount);
            Assert.Equal(FloodItEventType.Rejected, board.Events[^1].Type);
            Assert.Equal(current, board.Events[^1].Color);
        }

        [Fact]
        public void 非法颜色编号抛出越界异常()
        {
            var board = new FloodBoard(seed: 42);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Choose(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Choose(FloodItRules.ColorCount));
            Assert.Equal(0, board.Steps);
            Assert.False(board.IsWon);
            Assert.False(board.IsLost);
        }

        [Fact]
        public void 步数用尽未完成即失败()
        {
            var board = new FloodBoard(seed: 42);

            for (int i = 0; i < FloodItRules.MaxMoves; i++)
            {
                Assert.True(board.Choose(SmallestValidColor(board)));
            }

            Assert.Equal(FloodItRules.MaxMoves, board.Steps);
            Assert.Equal(0, board.RemainingMoves);
            Assert.False(board.IsWon);
            Assert.True(board.IsLost);
            Assert.Equal(FloodItEventType.Lost, board.Events[^1].Type);

            Assert.False(board.Choose(SmallestValidColor(board)));
            Assert.Equal(FloodItRules.MaxMoves, board.Steps);
        }

        [Fact]
        public void 全盘同色即胜利()
        {
            var board = new FloodBoard(seed: 7);

            // 贪心策略：每次选扩张最多的颜色（平局取最小色号），该种子可在 25 步内全盘同色。
            for (int guard = 0; guard < FloodItRules.MaxMoves && !board.IsWon; guard++)
            {
                Assert.True(board.Choose(GreedyColor(board)));
            }

            Assert.True(board.IsWon);
            Assert.False(board.IsLost);
            Assert.Equal(board.CellCount, board.FloodedCount);
            var colors = board.Colors;
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Assert.Equal(colors[0, 0], colors[row, column]);
                }
            }

            Assert.Equal(FloodItEventType.Won, board.Events[^1].Type);
            Assert.False(board.Choose(SmallestValidColor(board)));
        }

        [Fact]
        public void 重置回初始棋盘()
        {
            var board = new FloodBoard(seed: 42);
            string initial = SnapshotOf(board);
            int initialFlooded = board.FloodedCount;
            Assert.True(board.Choose(SmallestValidColor(board)));
            Assert.True(board.Choose(SmallestValidColor(board)));
            Assert.Equal(2, board.Steps);

            board.Reset();

            Assert.Equal(initial, SnapshotOf(board));
            Assert.Equal(0, board.Steps);
            Assert.Equal(initialFlooded, board.FloodedCount);
            Assert.False(board.IsWon);
            Assert.False(board.IsLost);
            Assert.Equal(FloodItEventType.Reset, board.Events[^1].Type);
            Assert.True(board.Choose(SmallestValidColor(board)));
            Assert.Equal(1, board.Steps);
        }

        [Fact]
        public void 换题重建新棋盘并清零步数()
        {
            var board = new FloodBoard(seed: 42);
            Assert.True(board.Choose(SmallestValidColor(board)));

            board.NewPuzzle(777);

            Assert.Equal(777, board.Seed);
            Assert.Equal(0, board.Steps);
            Assert.False(board.IsWon);
            Assert.False(board.IsLost);
            Assert.Equal(SnapshotOf(new FloodBoard(seed: 777)), SnapshotOf(board));
            Assert.Equal(FloodItEventType.Generated, board.Events[^1].Type);
        }

        [Fact]
        public void 棋盘尺寸支持四到三十且越界被拒()
        {
            foreach (int size in new[] { FloodItRules.MinBoardSize, FloodItRules.Size, FloodItRules.MaxBoardSize })
            {
                var board = new FloodBoard(seed: 9, size: size);
                Assert.Equal(size, board.Size);
                Assert.Equal(size * size, board.CellCount);
                foreach (int color in board.Colors) Assert.InRange(color, 0, FloodItRules.ColorCount - 1);
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FloodBoard(seed: 9, size: FloodItRules.MinBoardSize - 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FloodBoard(seed: 9, size: FloodItRules.MaxBoardSize + 1));
        }

        [Fact]
        public void 颜色属性返回副本()
        {
            var board = new FloodBoard(seed: 42);
            var snapshot = board.Colors;
            int original = snapshot[5, 5];

            snapshot[5, 5] = (original + 1) % FloodItRules.ColorCount;

            Assert.Equal(original, board.GetColor(5, 5));
        }

        [Fact]
        public void 事件日志封顶一百二十八条()
        {
            var board = new FloodBoard(seed: 42);
            int current = board.GetColor(0, 0);

            for (int i = 0; i < 150; i++) Assert.False(board.Choose(current));

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(FloodItEventType.Rejected, board.Events[^1].Type);
            Assert.Equal(0, board.Steps);
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string Simulate(int seed)
            {
                var board = new FloodBoard(seed);
                for (int i = 0; i < 10; i++) board.Choose(SmallestValidColor(board));
                board.Reset();
                board.NewPuzzle(seed + 1);
                for (int i = 0; i < 5; i++) board.Choose(SmallestValidColor(board));
                return HashCode.Combine(board.Steps, board.Seed, board.FloodedCount,
                    SnapshotOf(board), string.Join(';', board.Events.Select(e => e.ToString()))).ToString();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        private static string SnapshotOf(FloodBoard board)
        {
            var grid = board.Colors;
            var values = new List<string>();
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    values.Add(grid[row, column].ToString());
                }
            }

            return string.Join(';', values);
        }

        /// <summary>测试侧独立的淹没区计算：从左上角沿同色格子深度优先，与核心的 BFS 互为交叉验证。</summary>
        private static bool[,] FloodMapOf(int[,] grid)
        {
            int rows = grid.GetLength(0);
            int columns = grid.GetLength(1);
            var flooded = new bool[rows, columns];
            var stack = new Stack<(int Row, int Column)>();
            int color = grid[0, 0];
            flooded[0, 0] = true;
            stack.Push((0, 0));
            while (stack.Count > 0)
            {
                var (row, column) = stack.Pop();
                TryPush(grid, flooded, stack, color, row - 1, column);
                TryPush(grid, flooded, stack, color, row + 1, column);
                TryPush(grid, flooded, stack, color, row, column - 1);
                TryPush(grid, flooded, stack, color, row, column + 1);
            }

            return flooded;
        }

        private static void TryPush(int[,] grid, bool[,] flooded, Stack<(int Row, int Column)> stack, int color, int row, int column)
        {
            int rows = grid.GetLength(0);
            int columns = grid.GetLength(1);
            if (row < 0 || row >= rows || column < 0 || column >= columns) return;
            if (flooded[row, column] || grid[row, column] != color) return;

            flooded[row, column] = true;
            stack.Push((row, column));
        }

        private static int CountOf(bool[,] flooded)
        {
            int count = 0;
            foreach (bool value in flooded)
            {
                if (value) count++;
            }

            return count;
        }

        /// <summary>最小的合法选色：异于当前淹没色的最小颜色编号，必然被核心接受。</summary>
        private static int SmallestValidColor(FloodBoard board) => board.GetColor(0, 0) == 0 ? 1 : 0;

        /// <summary>与淹没区相邻且异于当前淹没色的最小颜色编号，选它必然扩张。</summary>
        private static int SmallestBorderColor(FloodBoard board)
        {
            int current = board.GetColor(0, 0);
            for (int color = 0; color < board.ColorCount; color++)
            {
                if (color == current) continue;
                for (int row = 0; row < board.Rows; row++)
                {
                    for (int column = 0; column < board.Columns; column++)
                    {
                        if (board.GetColor(row, column) != color || !BordersFloodedRegion(board, row, column)) continue;
                        return color;
                    }
                }
            }

            throw new InvalidOperationException("未全盘同色时淹没区外必有相邻异色格。");
        }

        private static bool BordersFloodedRegion(FloodBoard board, int row, int column) =>
            (row > 0 && board.IsFlooded(row - 1, column))
            || (row < board.Rows - 1 && board.IsFlooded(row + 1, column))
            || (column > 0 && board.IsFlooded(row, column - 1))
            || (column < board.Columns - 1 && board.IsFlooded(row, column + 1));

        /// <summary>贪心选色：模拟每个候选色的扩张格数，取扩张最多者，平局取最小色号。</summary>
        private static int GreedyColor(FloodBoard board)
        {
            int current = board.GetColor(0, 0);
            var colors = board.Colors;
            var flooded = FloodMapOf(colors);
            int best = -1;
            int bestCount = -1;
            for (int color = 0; color < board.ColorCount; color++)
            {
                if (color == current) continue;

                var recolored = (int[,])colors.Clone();
                for (int row = 0; row < board.Rows; row++)
                {
                    for (int column = 0; column < board.Columns; column++)
                    {
                        if (flooded[row, column]) recolored[row, column] = color;
                    }
                }

                int count = CountOf(FloodMapOf(recolored));
                if (count <= bestCount) continue;

                bestCount = count;
                best = color;
            }

            return best;
        }
    }
}
