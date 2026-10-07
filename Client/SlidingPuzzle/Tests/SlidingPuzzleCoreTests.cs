using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.SlidingPuzzle.Core;
using Xunit;

namespace Lyking.SlidingPuzzle.Core.Tests
{
    /// <summary>数字华容道核心规则测试：已解状态、打乱可解性、四向滑动、多格滑入、撤销、尺寸切换与胜利。</summary>
    public class SlidingBoardTests
    {
        private static readonly SlideDir[] AllDirs = { SlideDir.Up, SlideDir.Down, SlideDir.Left, SlideDir.Right };

        [Fact]
        public void 初始棋局即已解状态且拒绝滑动()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);

            Assert.True(board.IsSolved);
            Assert.Equal(PuzzleRules.MaxSize, board.Size);
            Assert.Equal(PuzzleRules.MaxSize * PuzzleRules.MaxSize, board.TileCount);
            Assert.Equal(0, board.Steps);
            Assert.Equal(SlidingPuzzleEventType.Reset, board.Events[0].Type);

            var tiles = board.Tiles;
            for (int i = 0; i < tiles.Length - 1; i++) Assert.Equal(i + 1, tiles[i]);
            Assert.Equal(0, tiles[^1]);

            Assert.All(AllDirs, dir => Assert.False(board.Move(dir)));
            Assert.Equal(0, board.Steps);
            Assert.True(new SlidingBoard(PuzzleRules.MinSize).IsSolved);
        }

        [Fact]
        public void 相同种子打乱布局一致且不同种子布局不同()
        {
            var first = new SlidingBoard(PuzzleRules.MaxSize);
            var path = first.Shuffle(42);

            Assert.Equal(PuzzleRules.ShuffleSteps4x4, path.Count);
            Assert.Equal(0, first.Steps);
            Assert.False(first.IsSolved);
            Assert.Equal(SlidingPuzzleEventType.Shuffled, first.Events[^1].Type);

            var same = new SlidingBoard(PuzzleRules.MaxSize);
            same.Shuffle(42);
            Assert.Equal(first.Tiles, same.Tiles);

            var other = new SlidingBoard(PuzzleRules.MaxSize);
            other.Shuffle(43);
            Assert.NotEqual(first.Tiles, other.Tiles);
        }

        [Fact]
        public void 打乱后必可解_奇偶性校验()
        {
            foreach (var size in new[] { PuzzleRules.MinSize, PuzzleRules.MaxSize })
            {
                var board = new SlidingBoard(size);
                board.Shuffle(42);
                Assert.True(IsSolvableByParity(board), $"{size}x{size} 打乱布局应满足可解奇偶性。");
            }
        }

        [Fact]
        public void 打乱路径逆序回放恰好还原且步数等于打乱步数()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            var path = board.Shuffle(42);

            ReplayInverse(board, path);

            Assert.True(board.IsSolved);
            Assert.Equal(path.Count, board.Steps);
            Assert.Equal(SlidingPuzzleEventType.Won, board.Events[^1].Type);

            var small = new SlidingBoard(PuzzleRules.MinSize);
            var smallPath = small.Shuffle(7);
            ReplayInverse(small, smallPath);
            Assert.True(small.IsSolved);
            Assert.Equal(PuzzleRules.ShuffleSteps3x3, smallPath.Count);
        }

        [Fact]
        public void 合法滑动四向且空位按相反方向移动()
        {
            var board = new SlidingBoard(PuzzleRules.MinSize);
            board.Shuffle(42);

            foreach (var dir in AllDirs)
            {
                MoveBlankTo(board, 1, 1);
                int before = BlankIndexOf(board);
                int expectedRow = before / board.Size;
                int expectedColumn = before % board.Size;
                switch (dir)
                {
                    case SlideDir.Up: expectedRow++; break;
                    case SlideDir.Down: expectedRow--; break;
                    case SlideDir.Left: expectedColumn++; break;
                    case SlideDir.Right: expectedColumn--; break;
                }

                int steps = board.Steps;
                Assert.True(board.Move(dir), $"{dir} 在空位居中时应合法。");
                int after = BlankIndexOf(board);
                Assert.Equal(expectedRow, after / board.Size);
                Assert.Equal(expectedColumn, after % board.Size);
                Assert.Equal(steps + 1, board.Steps);
                Assert.Equal(SlidingPuzzleEventType.Slid, board.Events[^1].Type);
            }
        }

        [Fact]
        public void 非法滑动被拒绝且棋局不变()
        {
            var board = new SlidingBoard(PuzzleRules.MinSize);
            board.Shuffle(42);
            MoveBlankTo(board, 0, 0);

            var tilesBefore = board.Tiles;
            int stepsBefore = board.Steps;

            // 空位在左上角：上方与左侧无滑块，Down/Right 即"把界外滑块滑入空位"。
            Assert.False(board.Move(SlideDir.Down));
            Assert.False(board.Move(SlideDir.Right));
            Assert.Equal(tilesBefore, board.Tiles);
            Assert.Equal(stepsBefore, board.Steps);
            Assert.Equal(SlidingPuzzleEventType.SlideRejected, board.Events[^1].Type);

            // 与空位不共线的滑块与空位本身都拒绝一次滑入。
            Assert.Equal(0, board.SlideTile(1 * board.Size + 1));
            Assert.Equal(0, board.SlideTile(BlankIndexOf(board)));
            Assert.Equal(tilesBefore, board.Tiles);
            Assert.Equal(stepsBefore, board.Steps);
        }

        [Fact]
        public void 滑动后步数递增且撤销还原()
        {
            var board = new SlidingBoard(PuzzleRules.MinSize);
            board.Shuffle(42);

            var tilesBefore = board.Tiles;
            Assert.True(board.Move(FirstLegalMove(board)));
            Assert.Equal(1, board.Steps);

            Assert.True(board.Undo());
            Assert.Equal(0, board.Steps);
            Assert.Equal(tilesBefore, board.Tiles);
            Assert.Equal(SlidingPuzzleEventType.Undone, board.Events[^1].Type);
            Assert.False(board.Undo());
        }

        [Fact]
        public void 已归位检测随滑动刷新()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            Assert.All(Enumerable.Range(0, board.TileCount).ToArray(), i => Assert.True(board.IsTileHome(i)));

            var path = board.Shuffle(42);
            Assert.True(Enumerable.Range(0, board.TileCount).Any(i => !board.IsTileHome(i)), "打乱后至少有一枚滑块未归位。");

            ReplayInverse(board, path);
            Assert.All(Enumerable.Range(0, board.TileCount).ToArray(), i => Assert.True(board.IsTileHome(i)));

            Assert.True(board.Undo());
            Assert.True(Enumerable.Range(0, board.TileCount).Any(i => !board.IsTileHome(i)), "撤销后应有滑块离开归位格。");
        }

        [Fact]
        public void 胜利后拒绝继续滑动()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            var path = board.Shuffle(42);
            ReplayInverse(board, path);

            Assert.True(board.IsSolved);
            int steps = board.Steps;
            Assert.All(AllDirs, dir => Assert.False(board.Move(dir)));
            Assert.Equal(0, board.SlideTile(0));
            Assert.Equal(steps, board.Steps);
            Assert.Equal(SlidingPuzzleEventType.SlideRejected, board.Events[^1].Type);
        }

        [Fact]
        public void 尺寸切换重置且非法尺寸抛出异常()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            board.Shuffle(42);
            Assert.False(board.IsSolved);

            board.Reset(PuzzleRules.MinSize);
            Assert.Equal(PuzzleRules.MinSize, board.Size);
            Assert.Equal(PuzzleRules.MinSize * PuzzleRules.MinSize, board.TileCount);
            Assert.True(board.IsSolved);
            Assert.Equal(0, board.Steps);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 }, board.Tiles);
            Assert.False(board.Undo());

            board.Reset(PuzzleRules.MaxSize);
            Assert.Equal(PuzzleRules.MaxSize * PuzzleRules.MaxSize, board.TileCount);
            Assert.True(board.IsSolved);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reset(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Reset(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingBoard(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingBoard(5));
        }

        [Fact]
        public void 与空位同行同列的滑块一次滑入多格()
        {
            var board = new SlidingBoard(PuzzleRules.MaxSize);
            board.Shuffle(42);
            MoveBlankTo(board, 1, 1);

            // 同行：点击 (1,3)，(1,2) 与 (1,3) 依次左移，空位落到 (1,3)。
            int rowTile = 1 * board.Size + 3;
            var tilesBefore = board.Tiles;
            int steps = board.Steps;
            Assert.Equal(2, board.SlideTile(rowTile));
            Assert.Equal(steps + 2, board.Steps);
            var tilesAfter = board.Tiles;
            Assert.Equal(tilesBefore[1 * board.Size + 2], tilesAfter[1 * board.Size + 1]);
            Assert.Equal(tilesBefore[rowTile], tilesAfter[1 * board.Size + 2]);
            Assert.Equal(0, tilesAfter[rowTile]);

            // 同列：空位移到 (2,2) 后点击 (0,2)，(1,2) 与 (0,2) 依次下移。
            MoveBlankTo(board, 2, 2);
            int columnTile = 0 * board.Size + 2;
            tilesBefore = board.Tiles;
            steps = board.Steps;
            Assert.Equal(2, board.SlideTile(columnTile));
            Assert.Equal(steps + 2, board.Steps);
            tilesAfter = board.Tiles;
            Assert.Equal(tilesBefore[columnTile], tilesAfter[1 * board.Size + 2]);
            Assert.Equal(0, tilesAfter[columnTile]);
        }

        [Fact]
        public void 事件日志封顶并保留最新记录()
        {
            var board = new SlidingBoard(PuzzleRules.MinSize);
            board.Shuffle(7);
            MoveBlankTo(board, 0, 0);

            for (int i = 0; i < 200; i++) Assert.False(board.Move(SlideDir.Down));

            Assert.Equal(128, board.Events.Count);
            Assert.Equal(SlidingPuzzleEventType.SlideRejected, board.Events[^1].Type);
        }

        [Fact]
        public void 同种子完整对局逐步一致()
        {
            static string Simulate(int seed)
            {
                var board = new SlidingBoard(PuzzleRules.MaxSize);
                var path = board.Shuffle(seed);
                ReplayInverse(board, path);
                return HashCode.Combine(
                    string.Join(';', board.Tiles),
                    board.Steps,
                    string.Join(';', path),
                    board.Events.Count,
                    board.Events[^1].Type).ToString();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        private static int BlankIndexOf(SlidingBoard board)
        {
            var tiles = board.Tiles;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == 0) return i;
            }

            throw new InvalidOperationException("棋局必有空位。");
        }

        /// <summary>把空位逐步推到目标格：Move(Up) 使空位下移一行，Move(Left) 使空位右移一列，依此类推。</summary>
        private static void MoveBlankTo(SlidingBoard board, int row, int column)
        {
            int guard = 0;
            while (BlankIndexOf(board) != row * board.Size + column)
            {
                int blank = BlankIndexOf(board);
                int blankRow = blank / board.Size;
                int blankColumn = blank % board.Size;
                if (blankRow < row) Assert.True(board.Move(SlideDir.Up));
                else if (blankRow > row) Assert.True(board.Move(SlideDir.Down));
                else if (blankColumn < column) Assert.True(board.Move(SlideDir.Left));
                else if (blankColumn > column) Assert.True(board.Move(SlideDir.Right));

                if (++guard > board.TileCount) throw new InvalidOperationException("空位移动未收敛。");
            }
        }

        /// <summary>逆序执行打乱路径的相反方向：由构造保证恰好回到已解状态。</summary>
        private static void ReplayInverse(SlidingBoard board, IReadOnlyList<SlideDir> path)
        {
            for (int i = path.Count - 1; i >= 0; i--)
            {
                Assert.True(board.Move(path[i].Opposite()));
            }
        }

        /// <summary>取一个空位旁实际存在的滑动方向；打乱后空位至少有一个合法方向。</summary>
        private static SlideDir FirstLegalMove(SlidingBoard board)
        {
            int blank = BlankIndexOf(board);
            int row = blank / board.Size;
            int column = blank % board.Size;
            if (column < board.Size - 1) return SlideDir.Left;
            if (column > 0) return SlideDir.Right;
            if (row < board.Size - 1) return SlideDir.Up;
            return SlideDir.Down;
        }

        /// <summary>按滑块拼图可解性判据校验：奇数边长要求逆序对为偶；偶数边长要求逆序对数 + 空位所在行（自底向上从 1 计）为奇。</summary>
        private static bool IsSolvableByParity(SlidingBoard board)
        {
            var tiles = board.Tiles;
            int inversions = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == 0) continue;
                for (int j = i + 1; j < tiles.Length; j++)
                {
                    if (tiles[j] != 0 && tiles[j] < tiles[i]) inversions++;
                }
            }

            if (board.Size % 2 == 1) return inversions % 2 == 0;

            int blank = BlankIndexOf(board);
            int blankRowFromBottom = board.Size - blank / board.Size;
            return (inversions + blankRowFromBottom) % 2 == 1;
        }
    }
}
