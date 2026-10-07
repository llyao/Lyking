using System;
using System.Collections.Generic;
using Lyking.Reversi.Core;
using Xunit;

namespace Lyking.Reversi.Core.Tests
{
    /// <summary>黑白棋核心规则测试：开局与合法步、翻转计算、过手与终局、AI 确定性与对局比分事件。</summary>
    public class ReversiCoreTests
    {
        private const int Size = ReversiRules.Size;

        [Fact]
        public void 规则常量与权重表符合设计()
        {
            Assert.Equal(8, ReversiRules.Size);
            Assert.Equal(64, ReversiRules.CellCount);
            Assert.Equal(3, ReversiRules.SearchDepth);
            Assert.Equal(8, ReversiRules.Directions.Count);
            Assert.Equal(4, ReversiRules.Corners.Count);
            Assert.Equal(100, ReversiRules.WeightAt(0, 0));
            Assert.Equal(100, ReversiRules.WeightAt(7, 7));
            Assert.Equal(10, ReversiRules.WeightAt(0, 3));
            Assert.Equal(10, ReversiRules.WeightAt(7, 5));
            Assert.Equal(1, ReversiRules.WeightAt(3, 3));
            Assert.Equal("d3", ReversiRules.ToCoordinate(2, 3));
            Assert.Equal("a1", ReversiRules.ToCoordinate(0, 0));
        }

        [Fact]
        public void 初始局面为标准四子且黑先手未结束()
        {
            var board = new ReversiBoard();

            Assert.Equal(Cell.White, board[3, 3]);   // d4
            Assert.Equal(Cell.White, board[4, 4]);   // e5
            Assert.Equal(Cell.Black, board[3, 4]);   // e4
            Assert.Equal(Cell.Black, board[4, 3]);   // d5
            Assert.Equal((2, 2), board.Counts);
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.Equal(0, board.MoveCount);
            Assert.False(board.IsFinished);
            Assert.Equal(ReversiOutcome.None, board.Outcome);
            Assert.Null(board.LastMove);
            Assert.Empty(board.Events);
        }

        [Fact]
        public void 初始合法步恰为d3c4f5e6四步且各翻一子()
        {
            var board = new ReversiBoard();
            var moves = board.LegalMoves(Cell.Black);

            Assert.Equal(4, moves.Count);
            var coordinates = new List<string>();
            foreach (var move in moves) coordinates.Add(ReversiRules.ToCoordinate(move.Row, move.Col));
            coordinates.Sort(StringComparer.Ordinal);
            Assert.Equal(new[] { "c4", "d3", "e6", "f5" }, coordinates);
            Assert.All(moves, move => Assert.Single(move.Flipped));
            // d3 翻 d4，e6 翻 e5。
            var d3 = FindMove(moves, 2, 3);
            var e6 = FindMove(moves, 5, 4);
            Assert.NotNull(d3);
            Assert.NotNull(e6);
            Assert.Equal(new[] { F(3, 3) }, Sorted(d3!.Value.Flipped));
            Assert.Equal(new[] { F(4, 4) }, Sorted(e6!.Value.Flipped));
            // 黑白对称：白方初始同样恰有 c5、d6、e3、f4 四步。
            var whiteMoves = board.LegalMoves(Cell.White);
            Assert.Equal(4, whiteMoves.Count);
            var whiteCoordinates = new List<string>();
            foreach (var move in whiteMoves) whiteCoordinates.Add(ReversiRules.ToCoordinate(move.Row, move.Col));
            whiteCoordinates.Sort(StringComparer.Ordinal);
            Assert.Equal(new[] { "c5", "d6", "e3", "f4" }, whiteCoordinates);
        }

        [Fact]
        public void 合法步按八方向计算翻转集合()
        {
            // 白子位于 d4、c5、d5，黑子在其外侧封口；c4 落子沿东、南、右下三方向同时夹击。
            var cells = Paint(
                (3, 3, Cell.White), (4, 2, Cell.White), (4, 3, Cell.White), (4, 5, Cell.White),
                (3, 4, Cell.Black), (5, 2, Cell.Black), (5, 4, Cell.Black), (4, 4, Cell.Black), (3, 5, Cell.Black));
            var board = new ReversiBoard(cells, Cell.Black);

            var c4 = FindMove(board.LegalMoves(Cell.Black), 3, 2);
            Assert.NotNull(c4);
            Assert.Equal(new[] { F(3, 3), F(4, 2), F(4, 3) }, Sorted(c4.Value.Flipped));
            // 不与任何方向构成夹击的空格不是合法步。
            Assert.Null(FindMove(board.LegalMoves(Cell.Black), 0, 0));
        }

        [Fact]
        public void 落子沿单方向翻转并轮换行棋方()
        {
            var board = new ReversiBoard();

            Assert.True(board.Play(2, 3));   // d3 翻 d4

            Assert.Equal(Cell.Black, board[2, 3]);
            Assert.Equal(Cell.Black, board[3, 3]);
            Assert.Equal((4, 1), board.Counts);
            Assert.Equal(Cell.White, board.CurrentTurn);
            Assert.Equal(1, board.MoveCount);
            Assert.Equal((2, 3), board.LastMove);
            Assert.Equal(ReversiEventType.PlayerMoved, board.Events[0].Type);
            Assert.Equal(1, board.Events[0].Flipped);
        }

        [Fact]
        public void 落子沿多方向同时翻转对方棋子()
        {
            var cells = Paint(
                (3, 3, Cell.White), (4, 2, Cell.White), (4, 3, Cell.White), (4, 5, Cell.White),
                (3, 4, Cell.Black), (5, 2, Cell.Black), (5, 4, Cell.Black), (4, 4, Cell.Black), (3, 5, Cell.Black));
            var board = new ReversiBoard(cells, Cell.Black);

            Assert.True(board.Play(3, 2));   // c4 同时翻转 d4、c5、d5

            Assert.Equal((9, 1), board.Counts);
            Assert.Equal(Cell.White, board.CurrentTurn);
            Assert.Equal((3, 2), board.LastMove);
            Assert.Equal(ReversiEventType.PlayerMoved, board.Events[0].Type);
            Assert.Equal(3, board.Events[0].Flipped);
        }

        [Fact]
        public void 无翻转落点被拒绝且盘面不变()
        {
            var board = new ReversiBoard();

            Assert.False(board.Play(0, 0));   // a1 不夹击任何白子

            Assert.Equal((2, 2), board.Counts);
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.Equal(0, board.MoveCount);
            Assert.Null(board.LastMove);
            Assert.Equal(ReversiEventType.IllegalRejected, board.Events[board.Events.Count - 1].Type);
        }

        [Fact]
        public void 已占格子拒绝落子()
        {
            var board = new ReversiBoard();

            Assert.False(board.Play(3, 3));   // d4 已有白子
            Assert.False(board.Play(4, 3));   // d5 已有黑子

            Assert.Equal((2, 2), board.Counts);
            Assert.Equal(0, board.MoveCount);
            Assert.Equal(Cell.Black, board.CurrentTurn);
        }

        [Fact]
        public void 终局后拒绝落子且盘面保持不变()
        {
            var board = new ReversiBoard();
            PlayOutToCompletion(board);
            Assert.True(board.IsFinished);
            var snapshot = BoardSnapshot(board);
            int moveCount = board.MoveCount;

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++) Assert.False(board.Play(row, col));
            }

            Assert.Equal(moveCount, board.MoveCount);
            Assert.Equal(snapshot, BoardSnapshot(board));
            Assert.Empty(board.LegalMoves(Cell.Black));
            Assert.Empty(board.LegalMoves(Cell.White));
        }

        [Fact]
        public void 对方无步时自动过手并由己方继续()
        {
            // 黑落 h3 翻 h2 后，白方仅剩 d4：其周邻除右上对角线（e3/f2/g1 全黑封死）
            // 外全部为空，白方任何落点都无法构成夹击，只能过手由黑方继续。
            var cells = Paint(
                (3, 3, Cell.White), (1, 7, Cell.White),
                (2, 4, Cell.Black), (1, 5, Cell.Black), (0, 6, Cell.Black), (0, 7, Cell.Black));
            var board = new ReversiBoard(cells, Cell.Black);

            Assert.True(board.Play(2, 7));   // h3 翻 h2

            Assert.Equal((6, 1), board.Counts);
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.False(board.IsFinished);
            Assert.Equal(ReversiEventType.Passed, board.Events[board.Events.Count - 1].Type);
            Assert.Equal(Cell.White, board.Events[board.Events.Count - 1].Side);

            Assert.True(board.Play(4, 2));   // c5 翻 d4，白子翻尽后终局

            Assert.Equal((8, 0), board.Counts);
            Assert.True(board.IsFinished);
            Assert.Equal(ReversiOutcome.BlackWins, board.Outcome);
            Assert.Equal(ReversiEventType.Finished, board.Events[board.Events.Count - 1].Type);
            Assert.Equal(ReversiEventType.Passed, board.Events[board.Events.Count - 2].Type);
        }

        [Fact]
        public void 双方均无步时自动终局并按子数定胜负()
        {
            // 白子对 e4、d5 被黑方完全围死且偏离两条主对角线，
            // 仅剩 a1、h8 两个谁也无法夹击的空格。
            var cells = Paint((3, 4, Cell.White), (4, 3, Cell.White));
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    bool isWhiteCore = (row == 3 && col == 4) || (row == 4 && col == 3);
                    bool isEmpty = (row == 0 && col == 0) || (row == 7 && col == 7);
                    if (!isWhiteCore && !isEmpty) cells[row, col] = Cell.Black;
                }
            }

            var board = new ReversiBoard(cells, Cell.Black);

            Assert.True(board.IsFinished);
            Assert.Equal((60, 2), board.Counts);
            Assert.Equal(ReversiOutcome.BlackWins, board.Outcome);
            Assert.Empty(board.LegalMoves(Cell.Black));
            Assert.Empty(board.LegalMoves(Cell.White));
            Assert.False(board.Play(0, 0));
        }

        [Fact]
        public void 棋盘下满时终局且平局()
        {
            var cells = Blank();
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++) cells[row, col] = (row + col) % 2 == 0 ? Cell.Black : Cell.White;
            }

            var board = new ReversiBoard(cells, Cell.Black);

            Assert.True(board.IsFinished);
            Assert.Equal((32, 32), board.Counts);
            Assert.Equal(ReversiOutcome.Draw, board.Outcome);
            Assert.Empty(board.LegalMoves(Cell.Black));
            Assert.False(board.Play(0, 0));
        }

        [Fact]
        public void AI整局推进完全确定()
        {
            var firstBoard = new ReversiBoard();
            var firstMoves = PlayOutToCompletion(firstBoard);
            var secondBoard = new ReversiBoard();
            var secondMoves = PlayOutToCompletion(secondBoard);

            Assert.True(firstBoard.IsFinished);
            Assert.Equal(firstMoves, secondMoves);
            Assert.Equal(BoardSnapshot(firstBoard), BoardSnapshot(secondBoard));
            Assert.Equal(firstBoard.Outcome, secondBoard.Outcome);
        }

        [Fact]
        public void AI同一局面重复决策结果一致()
        {
            var board = new ReversiBoard();
            for (int ply = 0; ply < 4; ply++)
            {
                int index = ReversiAi.BestMove(board, board.CurrentTurn);
                Assert.True(board.Play(index / Size, index % Size));
            }

            var turn = board.CurrentTurn;
            int decision = ReversiAi.BestMove(BoardCells(board), turn);

            Assert.True(decision >= 0);
            Assert.Equal(decision, ReversiAi.BestMove(BoardCells(board), turn));
            Assert.Equal(decision, ReversiAi.BestMove(board, turn));
        }

        [Fact]
        public void AI并列最优取行列字典序最小()
        {
            // 标准开局四步完全对称、评分相等，应取行优先扫描最先遇到的 d3。
            var cells = BoardCells(new ReversiBoard());

            Assert.Equal(F(2, 3), ReversiAi.BestMove(cells, Cell.Black));
        }

        [Fact]
        public void AI会优先抢占角点()
        {
            // 黑方经 a1 落子可沿东、右下两方向夹击抢占角点，其余着法收益远低。
            var cells = Paint(
                (0, 1, Cell.White), (1, 1, Cell.White), (4, 3, Cell.White), (4, 4, Cell.White),
                (0, 2, Cell.Black), (2, 2, Cell.Black), (4, 2, Cell.Black), (5, 5, Cell.Black));

            Assert.Equal(F(0, 0), ReversiAi.BestMove(cells, Cell.Black));
        }

        [Fact]
        public void AI落子必为合法步()
        {
            // 摆盘抽查：构造盘面上的决策必须能被翻转规则复核为合法步。
            var crafted = Paint(
                (3, 3, Cell.White), (4, 2, Cell.White), (4, 3, Cell.White), (4, 5, Cell.White),
                (3, 4, Cell.Black), (5, 2, Cell.Black), (5, 4, Cell.Black), (4, 4, Cell.Black), (3, 5, Cell.Black));
            AssertChoiceIsLegal(crafted, Cell.Black);

            // 整局推进：每一步决策都必须命中当时的合法步集合。
            var board = new ReversiBoard();
            int plies = 0;
            while (!board.IsFinished)
            {
                var turn = board.CurrentTurn;
                int index = ReversiAi.BestMove(board, turn);
                Assert.True(index >= 0, "未终局局面必然存在合法步。");
                AssertChoiceIsLegal(BoardCells(board), turn, index);
                Assert.True(board.Play(index / Size, index % Size));
                plies++;
                Assert.True(plies <= ReversiRules.CellCount, "对局必须在有限手内结束。");
            }
        }

        [Fact]
        public void 对局完整进行后比分跨局累计()
        {
            var match = new ReversiMatch();
            var first = PlayFullMatch(match);

            Assert.True(match.Board.IsFinished);
            Assert.Equal(first == ReversiOutcome.BlackWins ? 1 : 0, match.PlayerWins);
            Assert.Equal(first == ReversiOutcome.WhiteWins ? 1 : 0, match.AiWins);
            Assert.Equal(first == ReversiOutcome.Draw ? 1 : 0, match.Draws);
            Assert.Equal(1, CountEvents(match, SettlementType(first)));

            match.Restart();
            var second = PlayFullMatch(match);

            // 同一驱动完全确定：第二局必然同一结果，比分累计到 2。
            Assert.Equal(first, second);
            Assert.Equal(first == ReversiOutcome.BlackWins ? 2 : 0, match.PlayerWins);
            Assert.Equal(first == ReversiOutcome.WhiteWins ? 2 : 0, match.AiWins);
            Assert.Equal(first == ReversiOutcome.Draw ? 2 : 0, match.Draws);
            Assert.Equal(2, CountEvents(match, SettlementType(first)));
        }

        [Fact]
        public void 玩家取胜后重开清盘且比分保留()
        {
            // 白子仅 d4、c5 两枚：黑落 c4 一手翻尽白子，白方过手后黑方也无步，直接终局。
            var cells = Paint((3, 3, Cell.White), (4, 2, Cell.White), (3, 4, Cell.Black), (5, 2, Cell.Black));
            var match = new ReversiMatch(new ReversiBoard(cells, Cell.Black));

            Assert.True(match.PlayerMove(3, 2));   // c4

            Assert.True(match.Board.IsFinished);
            Assert.Equal(ReversiOutcome.BlackWins, match.Board.Outcome);
            Assert.Equal((5, 0), match.Board.Counts);
            Assert.Equal(1, match.PlayerWins);
            Assert.Equal(ReversiEventType.PlayerWon, match.Events[match.Events.Count - 1].Type);
            Assert.Equal(1, match.Round);

            match.Restart();

            Assert.Equal(2, match.Round);
            Assert.Equal((2, 2), match.Board.Counts);
            Assert.Equal(Cell.Black, match.Board.CurrentTurn);
            Assert.Equal(0, match.Board.MoveCount);
            Assert.False(match.Board.IsFinished);
            Assert.Equal(1, match.PlayerWins);
            Assert.Equal(0, match.AiWins);
            Assert.Equal(ReversiEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 对局事件日志超限时封顶裁剪()
        {
            var match = new ReversiMatch();
            for (int round = 0; round < 30; round++)
            {
                if (round > 0) match.Restart();
                for (int ply = 0; ply < 6 && !match.Board.IsFinished; ply++)
                {
                    var side = match.Board.CurrentTurn;
                    int index = ReversiAi.BestMove(match.Board, side);
                    Assert.True(side == Cell.Black
                        ? match.PlayerMove(index / Size, index % Size)
                        : match.AiMove());
                }
            }

            Assert.Equal(128, match.Events.Count);
            // 裁剪后保留最近事件，首条不再属于第 1 局。
            Assert.NotEqual(1, match.Events[0].Round);
            Assert.Equal(30, match.Events[match.Events.Count - 1].Round);
        }

        /// <summary>行优先展平索引。</summary>
        private static int F(int row, int col) => row * Size + col;

        /// <summary>构造空白 8x8 盘面。</summary>
        private static Cell[,] Blank() => new Cell[Size, Size];

        /// <summary>在空白盘面上批量摆子。</summary>
        private static Cell[,] Paint(params (int Row, int Col, Cell Side)[] placements)
        {
            var cells = Blank();
            foreach (var (row, col, side) in placements) cells[row, col] = side;
            return cells;
        }

        /// <summary>复制棋盘当前盘面。</summary>
        private static Cell[,] BoardCells(ReversiBoard board)
        {
            var cells = new Cell[Size, Size];
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++) cells[row, col] = board[row, col];
            }

            return cells;
        }

        /// <summary>把棋盘展开为行优先平铺列表，用于快照比较。</summary>
        private static List<Cell> BoardSnapshot(ReversiBoard board)
        {
            var snapshot = new List<Cell>();
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++) snapshot.Add(board[row, col]);
            }

            return snapshot;
        }

        /// <summary>在合法步列表中查找指定落点；不存在时返回 null。</summary>
        private static ReversiMove? FindMove(IReadOnlyList<ReversiMove> moves, int row, int col)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].Row == row && moves[i].Col == col) return moves[i];
            }

            return null;
        }

        /// <summary>翻转集合按索引升序排列，便于断言。</summary>
        private static int[] Sorted(IReadOnlyList<int> indexes)
        {
            var copy = new int[indexes.Count];
            for (int i = 0; i < indexes.Count; i++) copy[i] = indexes[i];
            Array.Sort(copy);
            return copy;
        }

        /// <summary>断言 AI 在给定盘面为 <paramref name="turn"/> 选出的落点确为合法步。</summary>
        private static void AssertChoiceIsLegal(Cell[,] cells, Cell turn, int? expected = null)
        {
            int index = expected ?? ReversiAi.BestMove(cells, turn);
            Assert.True(index >= 0, "该局面存在合法步，AI 决策不应为空。");

            int row = index / Size;
            int col = index % Size;
            Assert.True(ReversiRules.FlipsFor(cells, row, col, turn).Count > 0, $"AI 决策 ({row},{col}) 必须是合法步。");
        }

        /// <summary>双方都用最优着法把棋盘收官，返回全部落点序列。</summary>
        private static List<int> PlayOutToCompletion(ReversiBoard board)
        {
            var moves = new List<int>();
            while (!board.IsFinished)
            {
                int index = ReversiAi.BestMove(board, board.CurrentTurn);
                Assert.True(index >= 0, "未终局局面必然存在合法步。");
                Assert.True(board.Play(index / Size, index % Size), "AI 落子应当成功。");
                moves.Add(index);
                Assert.True(moves.Count <= ReversiRules.CellCount, "对局必须在有限手内结束。");
            }

            return moves;
        }

        /// <summary>对局内玩家（黑）与 AI（白）都用最优着法收官，返回终局结果。</summary>
        private static ReversiOutcome PlayFullMatch(ReversiMatch match)
        {
            int plies = 0;
            while (!match.Board.IsFinished)
            {
                if (match.IsPlayerTurn)
                {
                    int index = ReversiAi.BestMove(match.Board, Cell.Black);
                    Assert.True(match.PlayerMove(index / Size, index % Size), "玩家最优落子应当成功。");
                }
                else
                {
                    Assert.True(match.AiMove(), "AI 应答应当成功。");
                }

                plies++;
                Assert.True(plies <= ReversiRules.CellCount, "对局必须在有限手内结束。");
            }

            return match.Board.Outcome;
        }

        /// <summary>终局结果对应的比分事件类型。</summary>
        private static ReversiEventType SettlementType(ReversiOutcome outcome)
        {
            return outcome switch
            {
                ReversiOutcome.BlackWins => ReversiEventType.PlayerWon,
                ReversiOutcome.WhiteWins => ReversiEventType.AiWon,
                _ => ReversiEventType.Drawn,
            };
        }

        private static int CountEvents(ReversiMatch match, ReversiEventType type)
        {
            int count = 0;
            foreach (var entry in match.Events)
            {
                if (entry.Type == type) count++;
            }

            return count;
        }
    }
}
