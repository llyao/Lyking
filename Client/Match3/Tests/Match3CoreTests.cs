using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Match3.Core;
using Xunit;

namespace Lyking.Match3.Core.Tests
{
    /// <summary>三消核心规则测试：确定性生成、三连检测、计分与连锁、重力填充、无效交换回退、死局重排与对局结束。</summary>
    public class Match3BoardTests
    {
        [Fact]
        public void 构造后棋盘就绪且初始无现成三连()
        {
            var board = new Match3Board(seed: 42);

            Assert.Equal(Match3Rules.CellCount, board.CellCount);
            Assert.Equal(0, board.Moves);
            Assert.Equal(0, board.Score);
            Assert.Equal(Match3Rules.TotalMoves, board.MovesLeft);
            Assert.Equal(0, board.ChainLevel);
            Assert.False(board.IsFinished);
            Assert.True(board.HasValidMove);
            Assert.False(HasAnyRun(board.Gems));
            Assert.Equal(Match3EventType.Dealt, board.Events[0].Type);
        }

        [Fact]
        public void 相同种子产生相同棋盘()
        {
            Assert.Equal(SnapshotOf(new Match3Board(42)), SnapshotOf(new Match3Board(42)));
            Assert.NotEqual(SnapshotOf(new Match3Board(1)), SnapshotOf(new Match3Board(2)));
        }

        [Fact]
        public void 固定种子生成序列与手算随机序列一致()
        {
            // 手算 new Random(42).Next(6) 逐格生成（含回填重取）的行优先前 12 格。
            var board = new Match3Board(seed: 42);
            var gems = board.Gems;
            int[] expected = { 4, 0, 0, 3, 1, 1, 4, 3, 1, 4, 1, 1 };
            Assert.Equal(expected, Enumerable.Range(0, expected.Length).Select(i => gems[i / Match3Rules.Columns, i % Match3Rules.Columns]));
        }

        [Fact]
        public void 横向三连消除计六十分()
        {
            // 行 0 = [2,5,2,3,...] 且 (1,1)=2：交换 (0,1) 与 (1,1) 补全横向三连。
            var initial = BackgroundBoard();
            initial[0, 0] = 2;
            initial[0, 2] = 2;
            initial[1, 1] = 2;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(1, 9));
            Assert.Equal(1, board.Moves);
            Assert.Equal(60, board.Score);
            Assert.Equal(1, board.ChainLevel);

            var matched = board.Events.Single(e => e.Type == Match3EventType.Matched);
            Assert.Equal(1, matched.GroupCount);
            Assert.Equal(3, matched.CellCount);
            Assert.Equal(60, matched.Score);
            Assert.Equal(1, matched.ChainIndex);
        }

        [Fact]
        public void 纵向三连消除计六十分()
        {
            // 列 0 = [2,5,2,3,...] 且 (1,1)=2：交换 (1,0) 与 (1,1) 补全纵向三连。
            var initial = BackgroundBoard();
            initial[0, 0] = 2;
            initial[2, 0] = 2;
            initial[1, 1] = 2;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(8, 9));
            Assert.Equal(1, board.Moves);
            Assert.Equal(60, board.Score);
            Assert.Equal(1, board.ChainLevel);
            Assert.Equal(3, board.Events.Single(e => e.Type == Match3EventType.Matched).CellCount);
        }

        [Fact]
        public void 四连消除计九十分()
        {
            // 行 0 = [2,5,2,2,2,4,...] 且 (1,1)=2：交换后形成横向四连。
            var initial = BackgroundBoard();
            initial[0, 0] = 2;
            initial[0, 2] = 2;
            initial[0, 3] = 2;
            initial[1, 1] = 2;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(1, 9));
            Assert.Equal(90, board.Score);
            Assert.Equal(4, board.Events.Single(e => e.Type == Match3EventType.Matched).CellCount);
        }

        [Fact]
        public void 五连消除计一百二十分()
        {
            // 行 0 = [2,5,2,2,2,2,5,...] 且 (1,1)=2：交换后形成横向五连。
            var initial = BackgroundBoard();
            initial[0, 0] = 2;
            initial[0, 2] = 2;
            initial[0, 3] = 2;
            initial[0, 4] = 2;
            initial[0, 5] = 5;
            initial[1, 1] = 2;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(1, 9));
            Assert.Equal(120, board.Score);
            Assert.Equal(5, board.Events.Single(e => e.Type == Match3EventType.Matched).CellCount);
        }

        [Fact]
        public void 横纵交叠按两组计分取和()
        {
            // 交换 (1,1) 与 (1,2) 同时补全行 1 横三连与列 1 纵三连，交叠于 (1,1)。
            var initial = BackgroundBoard();
            initial[0, 1] = 2;
            initial[0, 2] = 5;
            initial[1, 0] = 2;
            initial[1, 1] = 2;
            initial[1, 2] = 2;
            initial[2, 1] = 2;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(9, 10));
            Assert.Equal(1, board.Moves);
            Assert.Equal(120, board.Score);

            // 两组各 60 分取和；消除格数去重后 5 格（(1,1) 只计一次）。
            var matched = board.Events.Single(e => e.Type == Match3EventType.Matched);
            Assert.Equal(2, matched.GroupCount);
            Assert.Equal(5, matched.CellCount);
            Assert.Equal(120, matched.Score);
        }

        [Fact]
        public void 交换无消除回退不计步()
        {
            // 背景棋盘相邻格交换不会产生三连：应回退原状、不计步、记无效交换事件。
            var initial = BackgroundBoard();
            var board = new Match3Board(initial, seed: 7);

            Assert.False(board.Swap(0, 1));
            Assert.Equal(0, board.Moves);
            Assert.Equal(0, board.Score);
            Assert.Equal(SnapshotOf(initial), SnapshotOf(board.Gems));
            Assert.Equal(Match3EventType.SwapRejected, board.Events[^1].Type);
            Assert.Equal(0, board.Events[^1].Move);
        }

        [Fact]
        public void 不相邻交换越界与非法注入被拒绝()
        {
            var board = new Match3Board(seed: 42);

            Assert.False(board.Swap(0, 2));
            Assert.False(board.Swap(0, 9));
            Assert.Equal(0, board.Events.Count(e => e.Type == Match3EventType.Swapped));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Swap(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Swap(0, Match3Rules.CellCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.GetGem(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.GetGem(0, Match3Rules.Columns));

            Assert.Throws<ArgumentNullException>(() => new Match3Board(null!));
            Assert.Throws<ArgumentException>(() => new Match3Board(new int[4, 4]));
            var outOfRange = BackgroundBoard();
            outOfRange[0, 0] = Match3Rules.GemKindCount;
            Assert.Throws<ArgumentException>(() => new Match3Board(outOfRange));
        }

        [Fact]
        public void 消除后重力下移且顶部填充确定()
        {
            // 列 5 行 2-4 的纵向三连消除后：原行 0 的 1 下移到行 3、原行 1 的 2 下移到行 4，顶部 3 格填充。
            var initial = BackgroundBoard();
            initial[0, 5] = 1;
            initial[1, 5] = 2;
            initial[2, 5] = 3;
            initial[3, 5] = 3;
            initial[4, 5] = 5;
            initial[4, 6] = 3;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(37, 38));
            Assert.Equal(60, board.Score);

            var gems = board.Gems;
            int[] expectedColumn = { 3, 5, 2, 1, 2, 2, 5, 2 };
            Assert.Equal(expectedColumn, Enumerable.Range(0, Match3Rules.Rows).Select(row => gems[row, 5]));
            Assert.Equal(1, gems[3, 5]);
            Assert.Equal(2, gems[4, 5]);
            Assert.All(Enumerable.Range(0, 3), row => Assert.InRange(gems[row, 5], 0, Match3Rules.GemKindCount - 1));

            // 同种子重复同一交换，填充结果完全一致。
            var replayInitial = BackgroundBoard();
            replayInitial[0, 5] = 1;
            replayInitial[1, 5] = 2;
            replayInitial[2, 5] = 3;
            replayInitial[3, 5] = 3;
            replayInitial[4, 5] = 5;
            replayInitial[4, 6] = 3;
            var replay = new Match3Board(replayInitial, seed: 7);
            replay.Swap(37, 38);
            Assert.Equal(SnapshotOf(gems), SnapshotOf(replay.Gems));
        }

        [Fact]
        public void 连锁自动续算且倍率累加()
        {
            // 列 2 的纵向三连消除后，上方 0 下移与 (5,1)、(5,3) 拼成行 5 横向三连，第二波按 ×2 计分。
            var initial = BackgroundBoard();
            initial[2, 2] = 0;
            initial[3, 2] = 1;
            initial[4, 2] = 1;
            initial[5, 2] = 0;
            initial[5, 1] = 0;
            initial[5, 3] = 1;
            var board = new Match3Board(initial, seed: 7);

            Assert.True(board.Swap(42, 43));
            Assert.Equal(1, board.Moves);
            Assert.Equal(2, board.ChainLevel);
            Assert.Equal(180, board.Score);

            var matched = board.Events.Where(e => e.Type == Match3EventType.Matched).ToList();
            Assert.Equal(2, matched.Count);
            Assert.Equal(1, matched[0].ChainIndex);
            Assert.Equal(60, matched[0].Score);
            Assert.Equal(2, matched[1].ChainIndex);
            Assert.Equal(120, matched[1].Score);
        }

        [Fact]
        public void 死局棋盘无可行交换()
        {
            // (2r+3c)%6 的棋盘任何相邻交换都不会产生三连。
            var dead = new int[Match3Rules.Rows, Match3Rules.Columns];
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    dead[row, column] = (2 * row + 3 * column) % Match3Rules.GemKindCount;
                }
            }

            var board = new Match3Board(dead, seed: 1);
            Assert.False(board.HasValidMove);
        }

        [Fact]
        public void 重排后无三连且有可行交换()
        {
            var dead = new int[Match3Rules.Rows, Match3Rules.Columns];
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    dead[row, column] = (2 * row + 3 * column) % Match3Rules.GemKindCount;
                }
            }

            var board = new Match3Board(dead, seed: 1);
            int[] kindsBefore = board.Gems.Cast<int>().OrderBy(kind => kind).ToArray();

            Assert.True(board.Shuffle());
            Assert.True(board.HasValidMove);
            Assert.False(HasAnyRun(board.Gems));
            Assert.Equal(Match3EventType.Shuffled, board.Events[^1].Type);
            // 重排只换位置，宝石种类的多重集不变。
            Assert.Equal(kindsBefore, board.Gems.Cast<int>().OrderBy(kind => kind).ToArray());
        }

        [Fact]
        public void 步数用尽对局结束并拒绝交换()
        {
            var board = new Match3Board(seed: 11);
            for (int guard = 0; guard < 100 && !board.IsFinished; guard++)
            {
                if (!PlayAnyValidSwap(board)) Assert.True(board.Shuffle(), "死局自救重排应成功。");
            }

            Assert.True(board.IsFinished);
            Assert.Equal(Match3Rules.TotalMoves, board.Moves);
            Assert.Equal(0, board.MovesLeft);
            Assert.True(board.Score > 0);
            Assert.Equal(Match3EventType.Finished, board.Events[^1].Type);

            // 结束后任何交换都被静默拒绝，不产生新事件。
            int eventCount = board.Events.Count;
            Assert.All(Enumerable.Range(0, Match3Rules.CellCount - 1), i => Assert.False(board.Swap(i, i + 1)));
            Assert.Equal(eventCount, board.Events.Count);
            Assert.False(board.Shuffle());
        }

        [Fact]
        public void 事件日志封顶一百二十八条()
        {
            // 背景棋盘的每次交换都无效，恰好产生一条事件，循环 200 次验证封顶。
            var board = new Match3Board(BackgroundBoard(), seed: 7);
            for (int i = 0; i < 200; i++) board.Swap(0, 1);

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(Match3EventType.SwapRejected, board.Events[^1].Type);
            Assert.Equal(0, board.Moves);
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string Simulate(int seed)
            {
                var board = new Match3Board(seed);
                for (int turn = 0; turn < 12 && !board.IsFinished; turn++)
                {
                    if (!PlayAnyValidSwap(board) && !board.Shuffle()) break;
                }

                return HashCode.Combine(board.Score, board.Moves, board.ChainLevel, SnapshotOf(board.Gems),
                    string.Join(';', board.Events.Select(e => e.ToString()))).ToString();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        [Fact]
        public void 注入棋盘与属性副本均不受外部修改影响()
        {
            var initial = BackgroundBoard();
            var board = new Match3Board(initial, seed: 1);
            initial[0, 0] = (initial[0, 0] + 1) % Match3Rules.GemKindCount;
            Assert.Equal(0, board.GetGem(0, 0));

            var snapshot = board.Gems;
            snapshot[3, 3] = (snapshot[3, 3] + 1) % Match3Rules.GemKindCount;
            Assert.Equal(0, board.GetGem(3, 3));
        }

        /// <summary>生成无三连的背景棋盘：bg(r,c) = (3r+c) % 6，横向差 1、纵向差 3。</summary>
        private static int[,] BackgroundBoard()
        {
            var gems = new int[Match3Rules.Rows, Match3Rules.Columns];
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    gems[row, column] = (3 * row + column) % Match3Rules.GemKindCount;
                }
            }

            return gems;
        }

        private static string SnapshotOf(int[,] gems) =>
            string.Join(';', gems.Cast<int>());

        private static string SnapshotOf(Match3Board board) => SnapshotOf(board.Gems);

        /// <summary>独立扫描棋盘快照，判断是否存在横向或纵向 ≥3 连续段。</summary>
        private static bool HasAnyRun(int[,] gems)
        {
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column + 2 < Match3Rules.Columns; column++)
                {
                    if (gems[row, column] == gems[row, column + 1] && gems[row, column] == gems[row, column + 2]) return true;
                }
            }

            for (int column = 0; column < Match3Rules.Columns; column++)
            {
                for (int row = 0; row + 2 < Match3Rules.Rows; row++)
                {
                    if (gems[row, column] == gems[row + 1, column] && gems[row, column] == gems[row + 2, column]) return true;
                }
            }

            return false;
        }

        /// <summary>按行优先顺序穷举相邻交换，执行第一个有效交换并返回是否成功。</summary>
        private static bool PlayAnyValidSwap(Match3Board board)
        {
            for (int index = 0; index < board.CellCount; index++)
            {
                if (index % Match3Rules.Columns < Match3Rules.Columns - 1 && board.Swap(index, index + 1)) return true;
                if (index / Match3Rules.Columns < Match3Rules.Rows - 1 && board.Swap(index, index + Match3Rules.Columns)) return true;
            }

            return false;
        }
    }
}
