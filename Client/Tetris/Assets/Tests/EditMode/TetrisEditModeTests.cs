using System.Linq;
using Lyking.Tetris.Core;
using NUnit.Framework;

namespace Lyking.Tetris.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class TetrisEditModeTests
    {
        [Test]
        public void 棋局在引擎外可创建并保持初始合法()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 7);

            Assert.AreEqual(10, board.Width);
            Assert.AreEqual(20, board.Height);
            Assert.AreEqual(0, board.Score);
            Assert.AreEqual(1, board.Level);
            Assert.IsFalse(board.IsFinished);
            Assert.AreEqual(TetrisEventType.Spawned, board.Events[0].Type);
            Assert.AreEqual(4, board.PieceCells.Count);
            foreach (var cell in board.PieceCells)
            {
                Assert.GreaterOrEqual(cell.X, 0);
                Assert.Less(cell.X, board.Width);
                Assert.GreaterOrEqual(cell.Y, 0);
                Assert.Less(cell.Y, board.Height);
            }
        }

        [Test]
        public void 左右移动与旋转受井壁约束()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 7, pieceSequence: new[] { TetrominoKind.O });

            for (int i = 0; i < 4; i++) Assert.IsTrue(board.MoveLeft());
            Assert.IsFalse(board.MoveLeft());
            Assert.AreEqual(0, board.PieceOrigin.X);

            Assert.IsTrue(board.Rotate());
            Assert.AreEqual(1, board.Rotation);
            // O 旋转后形状不变，仍在井内。
            Assert.IsTrue(board.PieceCells.All(cell => cell.X >= 0 && cell.X < board.Width));
        }

        [Test]
        public void 硬降落底锁定并写入事件()
        {
            var board = new TetrisBoard(TetrisRules.Default, seed: 7, pieceSequence: new[] { TetrominoKind.O });

            Assert.IsTrue(board.HardDrop());
            Assert.AreEqual(TetrominoKind.O, board.CellAt(4, 0));
            Assert.AreEqual(TetrominoKind.O, board.CellAt(5, 1));
            Assert.IsTrue(board.Events.Any(e => e.Type == TetrisEventType.Locked));
            Assert.IsFalse(board.IsFinished);
            Assert.AreEqual(0, board.TickIndex);
        }

        [Test]
        public void 填满整行即消行计分并写入事件()
        {
            var rules = new TetrisRules(6, 10, 10, baseStepSeconds: 0.8f, stepShrinkFactor: 0.85f,
                minStepSeconds: 0.1f, clearScores: new[] { 100, 300, 500, 800 });
            var board = new TetrisBoard(rules, seed: 7, pieceSequence: new[] { TetrominoKind.O, TetrominoKind.O, TetrominoKind.O });

            board.HardDrop();
            board.MoveLeft();
            board.MoveLeft();
            board.HardDrop();
            board.MoveRight();
            board.MoveRight();
            board.HardDrop();

            Assert.AreEqual(300, board.Score);
            Assert.AreEqual(2, board.LinesCleared);
            Assert.IsTrue(board.Events.Any(e => e.Type == TetrisEventType.LineCleared));
            Assert.IsNull(board.CellAt(2, 0));
        }
    }
}
