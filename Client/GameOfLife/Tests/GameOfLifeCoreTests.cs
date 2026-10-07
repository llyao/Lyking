using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameOfLife.Core;
using Xunit;

namespace Lyking.GameOfLife.Core.Tests
{
    /// <summary>生命游戏核心规则测试：B3/S23 完备性、环形边界、经典图案、随机撒子、事件日志与确定性。</summary>
    public class LifeBoardTests
    {
        [Fact]
        public void 构造后棋盘为空且默认尺寸取自规则()
        {
            var board = new LifeBoard();

            Assert.Equal(LifeRules.DefaultWidth, board.Width);
            Assert.Equal(LifeRules.DefaultHeight, board.Height);
            Assert.Equal(0, board.Generation);
            Assert.Equal(0, board.AliveCount);
            Assert.False(board.IsPaused);
            Assert.Empty(board.Events);
            Assert.DoesNotContain(true, board.Cells.Cast<bool>());
        }

        [Fact]
        public void 支持自定义尺寸并拒绝越界尺寸()
        {
            var board = new LifeBoard(10, 8);
            Assert.Equal(10, board.Width);
            Assert.Equal(8, board.Height);

            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeBoard(LifeRules.MinSize - 1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeBoard(48, LifeRules.MaxSize + 1));
        }

        [Fact]
        public void 零个或一个邻居的细胞死亡()
        {
            var board = new LifeBoard();
            board.ToggleCell(20, 15);           // 孤立细胞
            board.ToggleCell(30, 15);           // 互为唯一邻居的一对
            board.ToggleCell(31, 15);

            board.Step();

            Assert.Equal(0, board.AliveCount);
            Assert.Equal(1, board.Generation);
        }

        [Fact]
        public void 两个或三个邻居的细胞存活()
        {
            // 方块：每格恰有 3 个邻居，稳定不动。
            var block = new LifeBoard();
            foreach (var (x, y) in LifePatterns.Block) block.ToggleCell(x + 20, y + 15);

            block.Step();

            Assert.Equal(4, block.AliveCount);
            Assert.True(block.IsAlive(20, 15));
            Assert.True(block.IsAlive(21, 15));
            Assert.True(block.IsAlive(20, 16));
            Assert.True(block.IsAlive(21, 16));

            // L 形：三格各有 2 个邻居全部存活，缺口格恰有 3 个邻居复活成方块。
            var elbow = new LifeBoard();
            elbow.ToggleCell(20, 15);
            elbow.ToggleCell(21, 15);
            elbow.ToggleCell(20, 16);

            elbow.Step();

            Assert.Equal(4, elbow.AliveCount);
            Assert.True(elbow.IsAlive(20, 15));
            Assert.True(elbow.IsAlive(21, 15));
            Assert.True(elbow.IsAlive(20, 16));
            Assert.True(elbow.IsAlive(21, 16));
        }

        [Fact]
        public void 四个及以上邻居的细胞死亡()
        {
            var board = new LifeBoard();
            // 十字形：中心恰有 4 个邻居。
            board.ToggleCell(20, 15);
            board.ToggleCell(19, 15);
            board.ToggleCell(21, 15);
            board.ToggleCell(20, 14);
            board.ToggleCell(20, 16);

            board.Step();

            // 中心因过密死亡，四个臂（3 邻居）与四个对角（3 邻居复活）共存。
            Assert.False(board.IsAlive(20, 15));
            Assert.Equal(8, board.AliveCount);

            // 八邻居围满：中心同样死亡。
            var crowded = new LifeBoard();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    crowded.ToggleCell(30 + dx, 15 + dy);
                }
            }

            crowded.Step();

            Assert.False(crowded.IsAlive(30, 15));
        }

        [Fact]
        public void 死细胞恰有三个邻居时复活()
        {
            var board = new LifeBoard();
            board.ToggleCell(20, 15);
            board.ToggleCell(21, 15);
            board.ToggleCell(22, 15);

            board.Step();

            // 横向三格上下两格恰有 3 个邻居复活；只有 2 个邻居的死格不复活。
            Assert.Equal(3, board.AliveCount);
            Assert.True(board.IsAlive(21, 14));
            Assert.True(board.IsAlive(21, 15));
            Assert.True(board.IsAlive(21, 16));
            Assert.False(board.IsAlive(20, 14));
            Assert.False(board.IsAlive(23, 15));
            Assert.False(board.IsAlive(20, 15));
        }

        [Fact]
        public void 边界环绕按环形拓扑计数()
        {
            // 横向闪烁器跨左边界摆放：(7,4) 与 (0,4) 经环绕相邻，等效连续三格。
            var board = new LifeBoard(8, 8);
            board.ToggleCell(7, 4);
            board.ToggleCell(0, 4);
            board.ToggleCell(1, 4);

            board.Step();

            Assert.Equal(3, board.AliveCount);
            Assert.True(board.IsAlive(0, 3));
            Assert.True(board.IsAlive(0, 4));
            Assert.True(board.IsAlive(0, 5));
            Assert.False(board.IsAlive(7, 4));

            board.Step();

            Assert.Equal(3, board.AliveCount);
            Assert.True(board.IsAlive(7, 4));
            Assert.True(board.IsAlive(0, 4));
            Assert.True(board.IsAlive(1, 4));
        }

        [Fact]
        public void 闪烁器周期两态振荡()
        {
            var board = new LifeBoard();
            foreach (var (x, y) in LifePatterns.Blinker) board.ToggleCell(x + 19, y + 15);

            board.Step();

            Assert.Equal(3, board.AliveCount);
            Assert.True(board.IsAlive(19, 16));
            Assert.True(board.IsAlive(20, 16));
            Assert.True(board.IsAlive(21, 16));
            Assert.False(board.IsAlive(20, 15));

            board.Step();

            Assert.Equal(2, board.Generation);
            Assert.Equal(3, board.AliveCount);
            Assert.True(board.IsAlive(20, 15));
            Assert.True(board.IsAlive(20, 16));
            Assert.True(board.IsAlive(20, 17));
        }

        [Fact]
        public void 方块静物多代不变()
        {
            var board = new LifeBoard();
            board.LoadPattern(LifePatterns.Block, "方块");

            for (int i = 0; i < 5; i++) board.Step();

            Assert.Equal(4, board.AliveCount);
            Assert.Equal(5, board.Generation);
            Assert.True(board.IsAlive(23, 15));
            Assert.True(board.IsAlive(24, 15));
            Assert.True(board.IsAlive(23, 16));
            Assert.True(board.IsAlive(24, 16));
        }

        [Fact]
        public void 滑翔机四步沿对角线平移一格()
        {
            var board = new LifeBoard();
            foreach (var (x, y) in LifePatterns.Glider) board.ToggleCell(x, y);

            for (int i = 0; i < 4; i++) board.Step();

            // 每 4 代沿对角线平移 (1,1)；图案贴在左上角附近，不触发环绕。
            Assert.Equal(5, board.AliveCount);
            Assert.True(board.IsAlive(2, 1));
            Assert.True(board.IsAlive(3, 2));
            Assert.True(board.IsAlive(1, 3));
            Assert.True(board.IsAlive(2, 3));
            Assert.True(board.IsAlive(3, 3));
            Assert.False(board.IsAlive(1, 0));
        }

        [Fact]
        public void 滑翔机枪三十代后活细胞数递增()
        {
            var board = new LifeBoard();
            board.LoadPattern(LifePatterns.GosperGliderGun, "滑翔机枪");

            Assert.Equal(36, board.AliveCount);
            Assert.Equal(0, board.Generation);
            Assert.Equal(LifeEventType.PatternLoaded, board.Events[^1].Type);
            Assert.Equal("滑翔机枪", board.Events[^1].PatternName);

            for (int i = 0; i < 30; i++) board.Step();

            // 第 30 代第一枚滑翔机已发射，活细胞数超过初始 36。
            Assert.True(board.AliveCount > 36);
        }

        [Fact]
        public void 代数按推进次数计数()
        {
            var board = new LifeBoard();
            for (int i = 0; i < 7; i++) board.Step();

            Assert.Equal(7, board.Generation);

            board.Clear();

            Assert.Equal(0, board.Generation);
        }

        [Fact]
        public void 清空使活细胞代数与年龄归零()
        {
            var board = new LifeBoard();
            board.Randomize(3, 0.5);
            for (int i = 0; i < 10; i++) board.Step();
            Assert.True(board.AliveCount > 0);

            board.Clear();

            Assert.Equal(0, board.AliveCount);
            Assert.Equal(0, board.Generation);
            Assert.Equal(0, board.Age.Cast<int>().Max());
            Assert.DoesNotContain(true, board.Cells.Cast<bool>());
            Assert.Equal(LifeEventType.Cleared, board.Events[^1].Type);
        }

        [Fact]
        public void 随机撒子同种子布局完全一致()
        {
            var first = new LifeBoard();
            var second = new LifeBoard();
            first.Randomize(99, LifeRules.DefaultDensity);
            second.Randomize(99, LifeRules.DefaultDensity);

            Assert.Equal(SnapshotOf(first), SnapshotOf(second));
            Assert.Equal(first.AliveCount, second.AliveCount);

            var third = new LifeBoard();
            third.Randomize(100, LifeRules.DefaultDensity);
            Assert.NotEqual(SnapshotOf(first), SnapshotOf(third));

            // 重复撒子覆盖旧布局且代数归零。
            first.Randomize(99, LifeRules.DefaultDensity);
            Assert.Equal(0, first.Generation);
            Assert.Equal(SnapshotOf(first), SnapshotOf(second));
        }

        [Fact]
        public void 随机撒子密度落在合理区间且支持边界值()
        {
            var board = new LifeBoard();
            board.Randomize(5, 0.3);
            int total = board.Width * board.Height;

            Assert.InRange(board.AliveCount, (int)(total * 0.2), (int)(total * 0.4));
            Assert.Equal(LifeEventType.Randomized, board.Events[^1].Type);
            Assert.Equal(board.AliveCount, board.Events[^1].CellCount);

            board.Randomize(7, 0.0);
            Assert.Equal(0, board.AliveCount);

            board.Randomize(7, 1.0);
            Assert.Equal(total, board.AliveCount);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Randomize(7, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Randomize(7, 1.1));
        }

        [Fact]
        public void 载入图案居中放置并清空旧局面()
        {
            var board = new LifeBoard();
            board.Randomize(1, 0.9);
            Assert.True(board.AliveCount > 4);

            board.LoadPattern(LifePatterns.Block, "方块");

            // 包围盒 2x2 在 48x32 上居中：偏移 (23,15)。
            Assert.Equal(4, board.AliveCount);
            Assert.Equal(0, board.Generation);
            Assert.True(board.IsAlive(23, 15));
            Assert.True(board.IsAlive(24, 15));
            Assert.True(board.IsAlive(23, 16));
            Assert.True(board.IsAlive(24, 16));
            Assert.Equal("方块", board.Events[^1].PatternName);
            Assert.Equal(4, board.Events[^1].CellCount);

            Assert.Throws<ArgumentException>(() => board.LoadPattern(Array.Empty<(int X, int Y)>()));
        }

        [Fact]
        public void 翻转单格切换活死并维护计数与年龄()
        {
            var board = new LifeBoard();
            Assert.False(board.IsAlive(5, 5));

            board.ToggleCell(5, 5);

            Assert.True(board.IsAlive(5, 5));
            Assert.Equal(1, board.AliveCount);
            Assert.Equal(1, board.AgeAt(5, 5));

            board.ToggleCell(5, 5);

            Assert.False(board.IsAlive(5, 5));
            Assert.Equal(0, board.AliveCount);
            Assert.Equal(0, board.AgeAt(5, 5));
        }

        [Fact]
        public void 年龄随存活递增且死亡归零()
        {
            var board = new LifeBoard();
            foreach (var (x, y) in LifePatterns.Block) board.ToggleCell(x + 20, y + 15);

            for (int i = 0; i < 3; i++) board.Step();

            Assert.Equal(4, board.AgeAt(20, 15));
            Assert.Equal(4, board.AgeAt(21, 16));

            board.ToggleCell(21, 16);

            Assert.Equal(0, board.AgeAt(21, 16));
            Assert.Equal(3, board.AliveCount);

            // 新生细胞年龄记 1，存活细胞在原年龄上加 1。
            var blinker = new LifeBoard();
            blinker.ToggleCell(20, 15);
            blinker.ToggleCell(21, 15);
            blinker.ToggleCell(22, 15);

            blinker.Step();

            Assert.Equal(1, blinker.AgeAt(21, 14));
            Assert.Equal(2, blinker.AgeAt(21, 15));
        }

        [Fact]
        public void 暂停与恢复切换状态并记录事件()
        {
            var board = new LifeBoard();
            board.Randomize(11, LifeRules.DefaultDensity);
            board.Step();
            int eventCount = board.Events.Count;

            board.SetPaused(true);
            Assert.True(board.IsPaused);
            board.SetPaused(true);              // 重复暂停不重复记录
            Assert.Equal(eventCount + 1, board.Events.Count);
            Assert.Equal(LifeEventType.Paused, board.Events[^1].Type);

            board.SetPaused(false);
            Assert.False(board.IsPaused);
            Assert.Equal(LifeEventType.Resumed, board.Events[^1].Type);
        }

        [Fact]
        public void 事件日志封顶一百二十八条()
        {
            var board = new LifeBoard();
            for (int i = 0; i < 200; i++) board.LoadPattern(LifePatterns.Block);

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(LifeEventType.PatternLoaded, board.Events[^1].Type);
            Assert.Equal(4, board.Events[^1].CellCount);
        }

        [Fact]
        public void 布局与年龄属性返回副本且越界访问被拒绝()
        {
            var board = new LifeBoard();
            board.ToggleCell(0, 0);
            bool[,] cells = board.Cells;
            int[,] ages = board.Age;

            cells[0, 0] = false;
            ages[0, 0] = 99;

            Assert.True(board.IsAlive(0, 0));
            Assert.Equal(1, board.AgeAt(0, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() => board.IsAlive(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.IsAlive(0, board.Height));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.ToggleCell(board.Width, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.AgeAt(0, -1));
        }

        [Fact]
        public void 逐代推进与连续推进结果一致()
        {
            var stepped = new LifeBoard();
            var batched = new LifeBoard();
            stepped.Randomize(7, LifeRules.DefaultDensity);
            batched.Randomize(7, LifeRules.DefaultDensity);

            for (int i = 0; i < 20; i++)
            {
                stepped.Step();
                Assert.InRange(stepped.AliveCount, 0, stepped.Width * stepped.Height);
            }

            for (int i = 0; i < 20; i++) batched.Step();

            Assert.Equal(SnapshotOf(batched), SnapshotOf(stepped));
            Assert.Equal(batched.AliveCount, stepped.AliveCount);
            Assert.Equal(20, stepped.Generation);
        }

        [Fact]
        public void 同种子同初始两次模拟五十代完全一致()
        {
            static string Simulate(int seed)
            {
                var board = new LifeBoard();
                board.Randomize(seed, LifeRules.DefaultDensity);
                for (int i = 0; i < 50; i++) board.Step();
                return $"{SnapshotOf(board)}|{board.AliveCount}|{board.Generation}";
            }

            Assert.Equal(Simulate(42), Simulate(42));
            Assert.NotEqual(Simulate(42), Simulate(43));
        }

        private static string SnapshotOf(LifeBoard board) =>
            string.Join(';', Flatten(board.Cells).Select(live => live ? '1' : '0'));

        private static IEnumerable<bool> Flatten(bool[,] grid)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    yield return grid[x, y];
                }
            }
        }
    }
}
