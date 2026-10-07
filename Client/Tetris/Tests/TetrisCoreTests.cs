using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Tetris.Core;
using Xunit;

namespace Lyking.Tetris.Core.Tests
{
    /// <summary>俄罗斯方块核心规则测试：方块表、移动旋转踢墙、软硬降、消行计分、升级、结束与确定性。</summary>
    public class TetrisBoardTests
    {
        private static TetrisRules SmallRules(int width = 6, int height = 10, int linesPerLevel = 10) =>
            new TetrisRules(width, height, linesPerLevel, baseStepSeconds: 0.8f, stepShrinkFactor: 0.85f,
                minStepSeconds: 0.1f, clearScores: new[] { 100, 300, 500, 800 });

        private static List<GridPos> Sorted(IEnumerable<GridPos> cells) =>
            cells.OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ToList();

        [Fact]
        public void 构造后初始状态合法()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42);

            Assert.Equal(10, board.Width);
            Assert.Equal(20, board.Height);
            Assert.Equal(0, board.Score);
            Assert.Equal(1, board.Level);
            Assert.Equal(0, board.LinesCleared);
            Assert.Equal(0, board.TickIndex);
            Assert.False(board.IsFinished);
            Assert.Equal(TetrisEventType.Spawned, board.Events[0].Type);
            Assert.Equal(4, board.PieceCells.Count);
            foreach (var cell in board.PieceCells)
            {
                Assert.InRange(cell.X, 0, board.Width - 1);
                Assert.InRange(cell.Y, 0, board.Height - 1);
                Assert.Null(board.CellAt(cell.X, cell.Y));
            }
        }

        [Fact]
        public void 方块表为七种方块各定义四个顺时针旋转态()
        {
            foreach (TetrominoKind kind in Enum.GetValues(typeof(TetrominoKind)))
            {
                int box = kind == TetrominoKind.I ? 4 : kind == TetrominoKind.O ? 2 : 3;
                Assert.Equal(box, TetrominoTable.BoxWidth(kind));
                Assert.Equal(box, TetrominoTable.BoxHeight(kind));

                for (int rotation = 0; rotation < TetrominoTable.RotationCount; rotation++)
                {
                    var cells = TetrominoTable.Cells(kind, rotation);
                    Assert.Equal(TetrominoTable.CellsPerPiece, cells.Count);

                    var unique = new HashSet<GridPos>();
                    foreach (var cell in cells)
                    {
                        Assert.InRange(cell.X, 0, box - 1);
                        Assert.InRange(cell.Y, 0, box - 1);
                        Assert.True(unique.Add(cell), $"{kind} 旋转态 {rotation} 内出现重复格子。");
                    }

                    // 相邻旋转态必须互为绕包围盒中心的顺时针旋转：(x,y) -> (y, 边长-1-x)。
                    var rotated = cells.Select(cell => new GridPos(cell.Y, box - 1 - cell.X));
                    var next = TetrominoTable.Cells(kind, (rotation + 1) % TetrominoTable.RotationCount);
                    Assert.Equal(Sorted(next), Sorted(rotated));
                }
            }

            // O 四个旋转态完全相同；T 出场为平底朝上凸。
            var oSpawn = Sorted(TetrominoTable.Cells(TetrominoKind.O, 0));
            for (int rotation = 1; rotation < TetrominoTable.RotationCount; rotation++)
            {
                Assert.Equal(oSpawn, Sorted(TetrominoTable.Cells(TetrominoKind.O, rotation)));
            }

            Assert.Equal(
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(1, 2) }.OrderBy(c => c.X).ThenBy(c => c.Y).ToList(),
                Sorted(TetrominoTable.Cells(TetrominoKind.T, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => TetrominoTable.Cells(TetrominoKind.T, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => TetrominoTable.Cells((TetrominoKind)7, 0));
        }

        [Fact]
        public void 左右移动在井壁处被拒绝()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.O });

            // O 出场在 10 宽井的第 4、5 列，向左可移 4 步贴壁。
            for (int i = 0; i < 4; i++) Assert.True(board.MoveLeft(), $"第 {i + 1} 次左移应成功。");
            Assert.Equal(0, board.PieceOrigin.X);
            Assert.False(board.MoveLeft());

            for (int i = 0; i < 8; i++) Assert.True(board.MoveRight(), $"第 {i + 1} 次右移应成功。");
            Assert.Equal(8, board.PieceOrigin.X);
            Assert.False(board.MoveRight());
            Assert.True(board.PieceCells.All(cell => cell.X >= 0 && cell.X < board.Width));
        }

        [Fact]
        public void 旋转在开阔处原位生效并循环()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.T });
            var spawnCells = Sorted(board.PieceCells);

            Assert.True(board.Rotate());
            Assert.Equal(1, board.Rotation);
            // T 顺时针 90 度：立柱在第 4 列，凸起转向右。
            Assert.Equal(
                new[] { new GridPos(4, 17), new GridPos(4, 18), new GridPos(4, 19), new GridPos(5, 18) }.OrderBy(c => c.X).ThenBy(c => c.Y).ToList(),
                Sorted(board.PieceCells));

            for (int i = 0; i < 3; i++) Assert.True(board.Rotate());
            Assert.Equal(0, board.Rotation);
            Assert.Equal(spawnCells, Sorted(board.PieceCells));
        }

        [Fact]
        public void 靠右壁旋转按踢墙顺序向左让位()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.I });

            // 竖 I 推到右壁：旋转态 1 的立柱在第 origin+2 列，最远可到第 9 列。
            for (int i = 0; i < 3; i++) Assert.True(board.MoveRight());
            Assert.Equal(6, board.PieceOrigin.X);
            Assert.False(board.MoveRight());

            Assert.True(board.Rotate());
            Assert.Equal(1, board.Rotation);
            Assert.True(board.MoveRight());
            Assert.Equal(7, board.PieceOrigin.X);
            Assert.False(board.MoveRight());

            // 原位旋转会越出右壁，向左踢 1 格后成功。
            Assert.True(board.Rotate());
            Assert.Equal(2, board.Rotation);
            Assert.Equal(6, board.PieceOrigin.X);
            Assert.True(board.PieceCells.All(cell => cell.X >= 0 && cell.X < board.Width));
        }

        [Fact]
        public void 无处可转时旋转失败并保持朝向()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.I });

            // 横 I 降到贴地前一格（包围盒原点 y=-1）：竖立旋转态会越出井底，四个候选全部失败。
            for (int i = 0; i < 17; i++) Assert.True(board.SoftDrop(), $"第 {i + 1} 次软降应成功。");
            Assert.Equal(-1, board.PieceOrigin.Y);

            var cellsBefore = Sorted(board.PieceCells);
            Assert.False(board.Rotate());
            Assert.Equal(0, board.Rotation);
            Assert.Equal(-1, board.PieceOrigin.Y);
            Assert.Equal(cellsBefore, Sorted(board.PieceCells));
        }

        [Fact]
        public void 软降逐格下移并在触底时锁定()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.T });

            Assert.Equal(17, board.PieceOrigin.Y);
            Assert.True(board.SoftDrop());
            Assert.Equal(16, board.PieceOrigin.Y);

            int drops = 1;
            while (board.SoftDrop()) drops++;
            Assert.Equal(18, drops);

            // T 平底贴地：底排 3 格与凸起 1 格写入井格，随后补入新方块。
            Assert.True(board.CellAt(3, 0) == TetrominoKind.T);
            Assert.True(board.CellAt(4, 0) == TetrominoKind.T);
            Assert.True(board.CellAt(5, 0) == TetrominoKind.T);
            Assert.True(board.CellAt(4, 1) == TetrominoKind.T);
            Assert.Contains(board.Events, e => e.Type == TetrisEventType.Locked);
            Assert.Equal(4, board.PieceCells.Count);
            Assert.False(board.IsFinished);
        }

        [Fact]
        public void 硬降直接落底锁定()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.O });

            Assert.True(board.HardDrop());
            Assert.True(board.CellAt(4, 0) == TetrominoKind.O);
            Assert.True(board.CellAt(5, 0) == TetrominoKind.O);
            Assert.True(board.CellAt(4, 1) == TetrominoKind.O);
            Assert.True(board.CellAt(5, 1) == TetrominoKind.O);
            Assert.Contains(board.Events, e => e.Type == TetrisEventType.Locked);
            // 硬降不属于重力步，不推进步序号。
            Assert.Equal(0, board.TickIndex);
            Assert.False(board.IsFinished);
        }

        [Fact]
        public void 填满整行后消行计分且上方堆叠下移()
        {
            var board = new TetrisBoard(SmallRules(), seed: 42, pieceSequence: new[] { TetrominoKind.L, TetrominoKind.J });

            board.MoveLeft();
            board.HardDrop(); // L 落底：底排 0-2 列，角块留在 (2,1)。
            board.MoveRight();
            board.MoveRight();
            board.HardDrop(); // J 落底：底排 3-5 列，角块留在 (3,1)，底排凑满 6 列，消 1 行。

            Assert.Equal(100, board.Score);
            Assert.Equal(1, board.LinesCleared);
            Assert.Equal(1, board.Level);
            Assert.Contains(board.Events, e => e.Type == TetrisEventType.LineCleared && e.Lines == 1);
            // 上一行的 L 角块与 J 角块整体下移一行。
            Assert.True(board.CellAt(2, 0) == TetrominoKind.L);
            Assert.True(board.CellAt(3, 0) == TetrominoKind.J);
            Assert.Null(board.CellAt(0, 0));
            Assert.Null(board.CellAt(5, 0));
            Assert.Null(board.CellAt(2, 1));
        }

        [Fact]
        public void 一次消两行按计分表得分()
        {
            var board = new TetrisBoard(SmallRules(), seed: 42, pieceSequence: new[] { TetrominoKind.O, TetrominoKind.O, TetrominoKind.O });

            board.HardDrop();
            board.MoveLeft();
            board.MoveLeft();
            board.HardDrop();
            board.MoveRight();
            board.MoveRight();
            board.HardDrop();

            // 三只 O 分列左、中、右，一次锁定的同时清掉相邻两行。
            Assert.Equal(300, board.Score);
            Assert.Equal(2, board.LinesCleared);
            Assert.Contains(board.Events, e => e.Type == TetrisEventType.LineCleared && e.Lines == 2);
            Assert.Null(board.CellAt(2, 0));
            Assert.Null(board.CellAt(5, 1));
            Assert.Equal(0, board.CountFilledCells());
        }

        [Fact]
        public void 消行使等级提升并缩短步进间隔()
        {
            var board = new TetrisBoard(SmallRules(linesPerLevel: 1), seed: 42,
                pieceSequence: new[] { TetrominoKind.O, TetrominoKind.O, TetrominoKind.O });

            board.HardDrop();
            board.MoveLeft();
            board.MoveLeft();
            board.HardDrop();
            board.MoveRight();
            board.MoveRight();
            board.HardDrop();

            // 计分使用消行时的等级 1，消 2 行后连升 2 级到 3 级。
            Assert.Equal(300, board.Score);
            Assert.Equal(2, board.LinesCleared);
            Assert.Equal(3, board.Level);
            Assert.Contains(board.Events, e => e.Type == TetrisEventType.LevelUp);
            Assert.Equal(0.578f, board.StepSeconds, 4);
        }

        [Fact]
        public void 新方块出场重叠即游戏结束且操作被拒绝()
        {
            var board = new TetrisBoard(SmallRules(), seed: 42,
                pieceSequence: new[] { TetrominoKind.O, TetrominoKind.O, TetrominoKind.O, TetrominoKind.O, TetrominoKind.O });

            for (int i = 0; i < 4; i++) Assert.True(board.HardDrop(), $"第 {i + 1} 只 O 应正常落底。");
            // 第 5 只 O 填满中列顶部，锁定后新方块出场即重叠。
            Assert.False(board.HardDrop());

            Assert.True(board.IsGameOver);
            Assert.Equal(TetrisEventType.GameOver, board.Events[board.Events.Count - 1].Type);
            Assert.True(board.CellAt(2, 9) == TetrominoKind.O);

            int score = board.Score;
            int ticks = board.TickIndex;
            Assert.False(board.MoveLeft());
            Assert.False(board.MoveRight());
            Assert.False(board.Rotate());
            Assert.False(board.SoftDrop());
            Assert.False(board.HardDrop());
            Assert.False(board.Tick());
            Assert.Equal(score, board.Score);
            Assert.Equal(ticks, board.TickIndex);
        }

        [Fact]
        public void 方块序列按注入顺序循环供预览()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 42, pieceSequence: new[] { TetrominoKind.O, TetrominoKind.T });

            Assert.Equal(TetrominoKind.O, board.CurrentKind);
            Assert.Equal(TetrominoKind.T, board.NextKind);

            Assert.True(board.HardDrop());
            Assert.Equal(TetrominoKind.T, board.CurrentKind);
            Assert.Equal(TetrominoKind.O, board.NextKind);

            Assert.Throws<ArgumentException>(() => new TetrisBoard(pieceSequence: Array.Empty<TetrominoKind>()));
        }

        [Fact]
        public void 规则纯函数提供消行计分与步进间隔()
        {
            var rules = TetrisRules.Default;
            Assert.Equal(100, rules.ScoreForLines(1, 1));
            Assert.Equal(300, rules.ScoreForLines(2, 1));
            Assert.Equal(500, rules.ScoreForLines(3, 1));
            Assert.Equal(800, rules.ScoreForLines(4, 1));
            Assert.Equal(900, rules.ScoreForLines(2, 3));

            Assert.Equal(0.8f, rules.StepSecondsFor(1), 4);
            Assert.Equal(0.68f, rules.StepSecondsFor(2), 4);
            Assert.Equal(0.185f, rules.StepSecondsFor(10), 3);
            Assert.Equal(0.1f, rules.StepSecondsFor(100), 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => rules.ScoreForLines(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.ScoreForLines(5, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.ScoreForLines(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.StepSecondsFor(0));
        }

        [Fact]
        public void 相同种子与相同操作序列模拟完全一致()
        {
            static long Simulate(int seed)
            {
                var board = new TetrisBoard(TetrisRules.Default, seed);
                var moves = new Random(seed + 1);
                for (int i = 0; i < 300 && !board.IsFinished; i++)
                {
                    switch (moves.Next(6))
                    {
                        case 0: board.MoveLeft(); break;
                        case 1: board.MoveRight(); break;
                        case 2: board.Rotate(); break;
                        case 3: board.SoftDrop(); break;
                        case 4: board.HardDrop(); break;
                        default: board.Tick(); break;
                    }
                }

                var hash = new HashCode();
                hash.Add(board.Score);
                hash.Add(board.Level);
                hash.Add(board.LinesCleared);
                hash.Add(board.TickIndex);
                hash.Add(board.IsGameOver);
                for (int y = 0; y < board.Height; y++)
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        hash.Add(board.CellAt(x, y).GetHashCode());
                    }
                }

                return hash.ToHashCode();
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }
    }

    /// <summary>测试辅助扩展：统计井内已锁定的格子数。</summary>
    internal static class TetrisBoardTestExtensions
    {
        public static int CountFilledCells(this TetrisBoard board)
        {
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.CellAt(x, y) is not null) count++;
                }
            }

            return count;
        }
    }
}
