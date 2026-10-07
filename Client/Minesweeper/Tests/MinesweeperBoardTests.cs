#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Minesweeper.Core;
using Xunit;

namespace Lyking.Minesweeper.Core.Tests
{
    public sealed class MinesweeperBoardTests
    {
        private const int Seed = 42;
        private const int FirstClickX = 4;
        private const int FirstClickY = 4;

        private static MinesweeperBoard CreateBeginnerBoard(int seed = Seed)
        {
            return new MinesweeperBoard(BoardSpec.Beginner, new SystemRandomWrapper(seed));
        }

        /// <summary>创建与被测棋盘执行相同首击的探针棋盘，保证两者雷区布局一致。</summary>
        private static MinesweeperBoard CreateProbedBoard(int seed, int x, int y)
        {
            var probe = CreateBeginnerBoard(seed);
            Assert.Equal(RevealOutcome.Revealed, probe.Reveal(x, y));
            return probe;
        }

        private static List<(int X, int Y)> GetMinePositions(MinesweeperBoard board)
        {
            var mines = new List<(int X, int Y)>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.CellAt(x, y).HasMine)
                    {
                        mines.Add((x, y));
                    }
                }
            }

            return mines;
        }

        private static List<(int X, int Y)> GetSafePositions(MinesweeperBoard board)
        {
            var safe = new List<(int X, int Y)>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (!board.CellAt(x, y).HasMine)
                    {
                        safe.Add((x, y));
                    }
                }
            }

            return safe;
        }

        /// <summary>与核心一致的洪水区域期望值：零区加边界，独立于棋盘实现计算。</summary>
        private static HashSet<(int X, int Y)> ExpectedFloodRegion(MinesweeperBoard probe, int startX, int startY)
        {
            var region = new HashSet<(int X, int Y)> { (startX, startY) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));
            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                if (probe.CellAt(x, y).AdjacentMines != 0)
                {
                    continue;
                }

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        int nx = x + offsetX;
                        int ny = y + offsetY;
                        if (nx < 0 || ny < 0 || nx >= probe.Width || ny >= probe.Height)
                        {
                            continue;
                        }

                        if (region.Add((nx, ny)))
                        {
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }

            return region;
        }

        /// <summary>
        /// 枚举所有可能的首击，用固定种子找出洪水展开最大的首击坐标，
        /// 返回该首击的探针棋盘（与被测棋盘布局完全一致）。
        /// </summary>
        private static (int X, int Y, MinesweeperBoard Probe) FindBestFloodFirstClick(int seed)
        {
            int bestX = 0;
            int bestY = 0;
            MinesweeperBoard bestProbe = CreateBeginnerBoard(seed);
            int bestSize = 0;
            for (int y = 0; y < bestProbe.Height; y++)
            {
                for (int x = 0; x < bestProbe.Width; x++)
                {
                    var probe = CreateBeginnerBoard(seed);
                    Assert.Equal(RevealOutcome.Revealed, probe.Reveal(x, y));
                    if (probe.RevealedSafeCount > bestSize)
                    {
                        bestSize = probe.RevealedSafeCount;
                        bestX = x;
                        bestY = y;
                        bestProbe = probe;
                    }
                }
            }

            Assert.True(bestSize >= 5, "固定种子应存在足够大的首击洪水区用于测试洪水展开");
            return (bestX, bestY, bestProbe);
        }

        /// <summary>首击洪水区域之外的安全格，在完成首击的被测棋盘上仍是隐藏格。</summary>
        private static List<(int X, int Y)> GetHiddenSafePositions(MinesweeperBoard probe, int firstX, int firstY)
        {
            HashSet<(int X, int Y)> flood = ExpectedFloodRegion(probe, firstX, firstY);
            return GetSafePositions(probe).Where(cell => !flood.Contains(cell)).ToList();
        }

        [Fact]
        public void 难度预设规格正确()
        {
            Assert.Equal((9, 9, 10), (BoardSpec.Beginner.Width, BoardSpec.Beginner.Height, BoardSpec.Beginner.MineCount));
            Assert.Equal((16, 16, 40), (BoardSpec.Intermediate.Width, BoardSpec.Intermediate.Height, BoardSpec.Intermediate.MineCount));
            Assert.Equal((30, 16, 99), (BoardSpec.Expert.Width, BoardSpec.Expert.Height, BoardSpec.Expert.MineCount));
            Assert.Equal(71, BoardSpec.Beginner.SafeCellCount);
        }

        [Theory]
        [InlineData(1, 9, 10)]
        [InlineData(9, 1, 10)]
        [InlineData(0, 9, 10)]
        [InlineData(9, 0, 10)]
        [InlineData(9, 9, 0)]
        [InlineData(9, 9, 81)]
        [InlineData(9, 9, 82)]
        [InlineData(-1, 9, 10)]
        [InlineData(9, -1, 10)]
        public void 非法规格被拒绝(int width, int height, int mineCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoardSpec(width, height, mineCount));
        }

        [Fact]
        public void 最小合法规格可成局()
        {
            var spec = new BoardSpec(2, 2, 3);
            Assert.Equal(4, spec.CellCount);
            Assert.Equal(1, spec.SafeCellCount);
        }

        [Fact]
        public void 初始棋盘全部隐藏且未布雷()
        {
            var board = CreateBeginnerBoard();

            Assert.Equal(GameState.Ready, board.State);
            Assert.False(board.AreMinesPlaced);
            Assert.Equal(0, board.FlagCount);
            Assert.Equal(10, board.RemainingMineCount);
            Assert.Equal(0, board.RevealedSafeCount);

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    MinesweeperCell cell = board.CellAt(x, y);
                    Assert.Equal(CellState.Hidden, cell.State);
                    Assert.False(cell.HasMine);
                    Assert.Equal(0, cell.AdjacentMines);
                }
            }
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(9, 0)]
        [InlineData(0, 9)]
        [InlineData(9, 9)]
        public void 只读访问越界快速失败(int x, int y)
        {
            var board = CreateBeginnerBoard();
            Assert.Throws<ArgumentOutOfRangeException>(() => board.CellAt(x, y));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.IsWrongFlag(x, y));
        }

        [Fact]
        public void 首击布雷且首格安全()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            var board = CreateBeginnerBoard();

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));

            Assert.True(board.AreMinesPlaced);
            Assert.Equal(GameState.Playing, board.State);
            MinesweeperCell first = board.CellAt(FirstClickX, FirstClickY);
            Assert.Equal(CellState.Revealed, first.State);
            Assert.False(first.HasMine);
            Assert.Equal(10, GetMinePositions(board).Count);
            Assert.Equal(probe.RevealedSafeCount, board.RevealedSafeCount);
        }

        [Fact]
        public void 首击排除邻域()
        {
            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    MinesweeperCell neighbor = board.CellAt(FirstClickX + offsetX, FirstClickY + offsetY);
                    Assert.False(neighbor.HasMine, $"首击邻域格 ({FirstClickX + offsetX},{FirstClickY + offsetY}) 不应有雷");
                }
            }
        }

        [Fact]
        public void 固定种子雷区可复现()
        {
            var first = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            var second = CreateProbedBoard(Seed, FirstClickX, FirstClickY);

            Assert.Equal(GetMinePositions(first), GetMinePositions(second));
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    Assert.Equal(first.CellAt(x, y).AdjacentMines, second.CellAt(x, y).AdjacentMines);
                }
            }
        }

        [Fact]
        public void 邻雷计数与实际雷分布一致()
        {
            var board = CreateProbedBoard(Seed, FirstClickX, FirstClickY);

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    MinesweeperCell cell = board.CellAt(x, y);
                    if (cell.HasMine)
                    {
                        continue;
                    }

                    int expected = 0;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int nx = x + offsetX;
                            int ny = y + offsetY;
                            if (nx >= 0 && ny >= 0 && nx < board.Width && ny < board.Height && board.CellAt(nx, ny).HasMine)
                            {
                                expected++;
                            }
                        }
                    }

                    Assert.Equal(expected, cell.AdjacentMines);
                }
            }
        }

        [Fact]
        public void 洪水展开打开整片零区()
        {
            (int clickX, int clickY, MinesweeperBoard probe) = FindBestFloodFirstClick(Seed);

            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;
            board.Reveal(clickX, clickY);

            HashSet<(int X, int Y)> expected = ExpectedFloodRegion(probe, clickX, clickY);
            Assert.Equal(expected.Count, board.RevealedSafeCount);
            foreach ((int x, int y) in expected)
            {
                Assert.Equal(CellState.Revealed, board.CellAt(x, y).State);
            }

            MinesweeperEvent flood = Assert.Single(events, e => e.Type == MinesweeperEventType.FloodRevealed);
            Assert.Equal(expected.Count, flood.Count);
            Assert.Equal(clickX, flood.X);
            Assert.Equal(clickY, flood.Y);
        }

        [Fact]
        public void 数字格只打开自身()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            (int x, int y, int _) = GetHiddenSafePositions(probe, FirstClickX, FirstClickY)
                .Select(cell => (cell.X, cell.Y, probe.CellAt(cell.X, cell.Y).AdjacentMines))
                .First(cell => cell.AdjacentMines > 0);

            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(x, y));

            Assert.Equal(probe.RevealedSafeCount + 1, board.RevealedSafeCount);
            Assert.Equal(CellState.Revealed, board.CellAt(x, y).State);
            Assert.DoesNotContain(events, e => e.Type == MinesweeperEventType.FloodRevealed);
            Assert.Equal(MinesweeperEventType.Revealed, Assert.Single(events).Type);
        }

        [Fact]
        public void 插旗与拔旗往返()
        {
            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(FlagOutcome.Flagged, board.ToggleFlag(0, 0));
            Assert.Equal(CellState.Flagged, board.CellAt(0, 0).State);
            Assert.Equal(1, board.FlagCount);
            Assert.Equal(9, board.RemainingMineCount);

            Assert.Equal(FlagOutcome.Unflagged, board.ToggleFlag(0, 0));
            Assert.Equal(CellState.Hidden, board.CellAt(0, 0).State);
            Assert.Equal(0, board.FlagCount);
            Assert.Equal(10, board.RemainingMineCount);

            Assert.Equal(MinesweeperEventType.Flagged, events[0].Type);
            Assert.Equal(0, events[0].X);
            Assert.Equal(0, events[0].Y);
            Assert.Equal(MinesweeperEventType.Unflagged, events[1].Type);
        }

        [Fact]
        public void 已翻开格不能插旗()
        {
            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);

            Assert.Equal(FlagOutcome.Rejected, board.ToggleFlag(FirstClickX, FirstClickY));
            Assert.Equal(0, board.FlagCount);
        }

        [Fact]
        public void 旗格阻挡翻开且不触发布雷()
        {
            var board = CreateBeginnerBoard();

            Assert.Equal(FlagOutcome.Flagged, board.ToggleFlag(0, 0));
            Assert.Equal(RevealOutcome.BlockedByFlag, board.Reveal(0, 0));

            Assert.Equal(CellState.Flagged, board.CellAt(0, 0).State);
            Assert.False(board.AreMinesPlaced);
            Assert.Equal(GameState.Ready, board.State);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(9, 0)]
        [InlineData(0, 9)]
        public void 越界操作返回无效格且无事件(int x, int y)
        {
            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(RevealOutcome.InvalidCell, board.Reveal(x, y));
            Assert.Equal(FlagOutcome.InvalidCell, board.ToggleFlag(x, y));

            Assert.Equal(GameState.Ready, board.State);
            Assert.Empty(events);
        }

        [Fact]
        public void 插旗可在首击前进行()
        {
            var board = CreateBeginnerBoard();

            Assert.Equal(FlagOutcome.Flagged, board.ToggleFlag(0, 0));
            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));

            Assert.True(board.AreMinesPlaced);
            Assert.Equal(GameState.Playing, board.State);
            Assert.Equal(1, board.FlagCount);
            Assert.Equal(9, board.RemainingMineCount);
        }

        [Fact]
        public void 剩余雷数可为负()
        {
            var board = CreateBeginnerBoard();

            int flagged = 0;
            for (int y = 0; y < board.Height && flagged < 12; y++)
            {
                for (int x = 0; x < board.Width && flagged < 12; x++)
                {
                    Assert.Equal(FlagOutcome.Flagged, board.ToggleFlag(x, y));
                    flagged++;
                }
            }

            Assert.Equal(12, board.FlagCount);
            Assert.Equal(-2, board.RemainingMineCount);
        }

        [Fact]
        public void 翻开全部安全格判胜()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));
            foreach ((int x, int y) in GetSafePositions(probe))
            {
                if (board.State == GameState.Won)
                {
                    break;
                }

                if (board.CellAt(x, y).State == CellState.Hidden)
                {
                    Assert.Equal(RevealOutcome.Revealed, board.Reveal(x, y));
                }
            }

            Assert.Equal(GameState.Won, board.State);
            Assert.Equal(probe.Spec.SafeCellCount, board.RevealedSafeCount);
            Assert.Contains(events, e => e.Type == MinesweeperEventType.Won);
            foreach ((int x, int y) in GetMinePositions(board))
            {
                Assert.Equal(CellState.Hidden, board.CellAt(x, y).State);
            }
        }

        [Fact]
        public void 踩雷判负并翻开所有雷()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            (int mineX, int mineY) = GetMinePositions(probe).First();
            Assert.True(probe.CellAt(mineX, mineY).HasMine);

            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));
            Assert.Equal(RevealOutcome.HitMine, board.Reveal(mineX, mineY));

            Assert.Equal(GameState.Lost, board.State);
            foreach ((int x, int y) in GetMinePositions(board))
            {
                Assert.Equal(CellState.Revealed, board.CellAt(x, y).State);
            }

            MinesweeperEvent hit = Assert.Single(events, e => e.Type == MinesweeperEventType.MineHit);
            Assert.Equal(mineX, hit.X);
            Assert.Equal(mineY, hit.Y);
            Assert.Contains(events, e => e.Type == MinesweeperEventType.Lost);
        }

        [Fact]
        public void 失败后错误旗可识别()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            (int safeX, int safeY) = GetHiddenSafePositions(probe, FirstClickX, FirstClickY).First();
            (int mineX, int mineY) = GetMinePositions(probe).First();

            var board = CreateBeginnerBoard();
            Assert.Equal(FlagOutcome.Flagged, board.ToggleFlag(safeX, safeY));
            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));
            Assert.Equal(RevealOutcome.HitMine, board.Reveal(mineX, mineY));

            Assert.True(board.IsWrongFlag(safeX, safeY), "旗住的格子在失败后若无雷应标记为错误旗");
            Assert.False(board.IsWrongFlag(FirstClickX, FirstClickY));
        }

        [Fact]
        public void 失败后操作被拒绝()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            (int mineX, int mineY) = GetMinePositions(probe).First();
            (int safeX, int safeY) = GetHiddenSafePositions(probe, FirstClickX, FirstClickY).Last();

            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);
            Assert.Equal(RevealOutcome.HitMine, board.Reveal(mineX, mineY));

            Assert.Equal(RevealOutcome.GameOver, board.Reveal(safeX, safeY));
            Assert.Equal(FlagOutcome.GameOver, board.ToggleFlag(safeX, safeY));
            Assert.Equal(GameState.Lost, board.State);
            Assert.Equal(CellState.Hidden, board.CellAt(safeX, safeY).State);
        }

        [Fact]
        public void 胜利后操作被拒绝()
        {
            var probe = CreateProbedBoard(Seed, FirstClickX, FirstClickY);
            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);
            foreach ((int x, int y) in GetSafePositions(probe))
            {
                if (board.State == GameState.Won)
                {
                    break;
                }

                if (board.CellAt(x, y).State == CellState.Hidden)
                {
                    board.Reveal(x, y);
                }
            }

            Assert.Equal(GameState.Won, board.State);
            Assert.Equal(RevealOutcome.GameOver, board.Reveal(0, 0));
            Assert.Equal(FlagOutcome.GameOver, board.ToggleFlag(0, 0));
            Assert.Equal(0, board.FlagCount);
        }

        [Fact]
        public void 观察者异常不破坏对局()
        {
            var board = CreateBeginnerBoard();
            var received = new List<MinesweeperEvent>();
            board.EventPublished += _ => throw new InvalidOperationException("观察者故障");
            board.EventPublished += received.Add;

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(FirstClickX, FirstClickY));

            Assert.True(board.AreMinesPlaced);
            Assert.Equal(GameState.Playing, board.State);
            Assert.Contains(received, e => e.Type == MinesweeperEventType.Revealed);
        }

        [Fact]
        public void 事件序列为翻开加洪水展开()
        {
            (int clickX, int clickY, MinesweeperBoard probe) = FindBestFloodFirstClick(Seed);

            var board = CreateBeginnerBoard();
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;
            board.Reveal(clickX, clickY);

            int expectedRegion = ExpectedFloodRegion(probe, clickX, clickY).Count;
            Assert.Equal(2, events.Count);
            Assert.Equal(MinesweeperEventType.Revealed, events[0].Type);
            Assert.Equal(clickX, events[0].X);
            Assert.Equal(clickY, events[0].Y);
            Assert.Equal(MinesweeperEventType.FloodRevealed, events[1].Type);
            Assert.Equal(expectedRegion, events[1].Count);
        }

        [Fact]
        public void 重复翻开已开格返回已翻开且无事件()
        {
            var board = CreateBeginnerBoard();
            board.Reveal(FirstClickX, FirstClickY);
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.Equal(RevealOutcome.AlreadyRevealed, board.Reveal(FirstClickX, FirstClickY));

            Assert.Empty(events);
            Assert.Equal(GameState.Playing, board.State);
            Assert.Equal(CellState.Revealed, board.CellAt(FirstClickX, FirstClickY).State);
        }

        [Fact]
        public void 二二小棋盘收缩排除集保证首格安全()
        {
            var board = new MinesweeperBoard(new BoardSpec(2, 2, 3), new SystemRandomWrapper(Seed));

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(0, 0));

            Assert.False(board.CellAt(0, 0).HasMine);
            Assert.Equal(3, board.CellAt(0, 0).AdjacentMines);
            Assert.Equal(3, GetMinePositions(board).Count);
        }

        [Fact]
        public void 三三棋盘唯一安全格首击直接判胜()
        {
            var board = new MinesweeperBoard(new BoardSpec(3, 3, 8), new SystemRandomWrapper(Seed));

            Assert.Equal(RevealOutcome.Revealed, board.Reveal(1, 1));

            Assert.False(board.CellAt(1, 1).HasMine);
            Assert.Equal(8, board.CellAt(1, 1).AdjacentMines);
            Assert.Equal(GameState.Won, board.State);
            Assert.Equal(1, board.RevealedSafeCount);
        }

        [Fact]
        public void 固定种子随机序列稳定()
        {
            var first = new SystemRandomWrapper(Seed);
            var second = new SystemRandomWrapper(Seed);

            int[] firstValues = Enumerable.Range(0, 16).Select(_ => first.Next(0, 100)).ToArray();
            int[] secondValues = Enumerable.Range(0, 16).Select(_ => second.Next(0, 100)).ToArray();

            Assert.Equal(firstValues, secondValues);
        }
    }
}
