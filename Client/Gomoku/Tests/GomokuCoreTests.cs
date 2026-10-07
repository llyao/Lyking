using System.Collections.Generic;
using Lyking.Gomoku.Core;
using Xunit;

namespace Lyking.Gomoku.Core.Tests
{
    /// <summary>五子棋核心规则测试：五连与长连判定、落子拒绝、AI 评分与决策确定性、对局比分事件。</summary>
    public class GomokuCoreTests
    {
        [Fact]
        public void 空棋盘初始为黑先手且棋盘尺寸正确()
        {
            var board = new GomokuBoard();

            Assert.Equal(GomokuRules.Size, board.Cells.GetLength(0));
            Assert.Equal(GomokuRules.Size, board.Cells.GetLength(1));
            Assert.All(EnumerateCells(board.Cells), cell => Assert.Equal(Cell.None, cell));
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.Equal(0, board.MoveCount);
            Assert.False(board.IsFinished);
            Assert.Equal(GomokuOutcome.None, board.Outcome);
            Assert.Null(board.LastMove);
            Assert.Null(board.FindWinningLine());
            Assert.False(board.IsFull());
        }

        [Fact]
        public void 成功落子后轮换行棋方并记录最后一手()
        {
            var board = new GomokuBoard();

            Assert.True(board.Play(7, 7));
            Assert.Equal(Cell.White, board.CurrentTurn);
            Assert.Equal(new CellPos(7, 7), board.LastMove);
            Assert.True(board.Play(7, 8));
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.Equal(new CellPos(7, 8), board.LastMove);
            Assert.Equal(2, board.MoveCount);
            Assert.False(board.IsFinished);
            Assert.Null(board.FindWinningLine());
        }

        [Fact]
        public void 已占格子拒绝落子且状态不变()
        {
            var board = new GomokuBoard();
            Assert.True(board.Play(7, 7));

            Assert.False(board.Play(7, 7));
            Assert.Equal(1, board.MoveCount);
            Assert.Equal(Cell.Black, board[7, 7]);
            Assert.Equal(Cell.White, board.CurrentTurn);
            Assert.False(board.IsEmptyAt(7, 7));
        }

        [Fact]
        public void 越界坐标拒绝落子()
        {
            var board = new GomokuBoard();

            Assert.False(board.Play(-1, 7));
            Assert.False(board.Play(7, -1));
            Assert.False(board.Play(GomokuRules.Size, 7));
            Assert.False(board.Play(7, GomokuRules.Size));
            Assert.Equal(0, board.MoveCount);
            Assert.Equal(Cell.Black, board.CurrentTurn);
        }

        [Fact]
        public void 终局后拒绝落子且棋盘保持终局形状()
        {
            var board = new GomokuBoard();
            // 黑子在第 7 行连成横五获胜。
            PlaySequence(board, (7, 4), (0, 0), (7, 5), (2, 0), (7, 6), (4, 0), (7, 7), (6, 0), (7, 8));
            Assert.True(board.IsFinished);
            var snapshot = (Cell[,])board.Cells.Clone();

            Assert.False(board.Play(0, 1));
            Assert.False(board.Play(-1, -1));
            Assert.Equal(9, board.MoveCount);
            Assert.Equal(Cell.Black, board.CurrentTurn);
            Assert.Equal(new CellPos(7, 8), board.LastMove);
            for (int row = 0; row < GomokuRules.Size; row++)
            {
                for (int col = 0; col < GomokuRules.Size; col++)
                {
                    Assert.Equal(snapshot[row, col], board[row, col]);
                }
            }
        }

        [Fact]
        public void 横向五连判定黑方获胜并返回连线坐标()
        {
            var board = new GomokuBoard();
            // 黑子在第 7 行 4-8 列连成横五，白子在第 0 列间隔落子不成线。
            PlaySequence(board, (7, 4), (0, 0), (7, 5), (2, 0), (7, 6), (4, 0), (7, 7), (6, 0), (7, 8));

            Assert.True(board.IsFinished);
            Assert.Equal(GomokuOutcome.BlackWins, board.Outcome);
            var line = board.FindWinningLine()!;
            Assert.Equal(5, line.Count);
            Assert.Equal(new CellPos(7, 4), line[0]);
            Assert.Equal(new CellPos(7, 8), line[4]);
            Assert.Equal(9, board.MoveCount);
            Assert.Equal(new CellPos(7, 8), board.LastMove);
        }

        [Fact]
        public void 纵向五连判定白方获胜()
        {
            var board = new GomokuBoard();
            // 白子在第 11 列 3-7 行连成纵五，黑子在第 0 行分散落子不成线。
            PlaySequence(board, (0, 0), (3, 11), (0, 1), (4, 11), (0, 2), (5, 11), (0, 3), (6, 11), (5, 0), (7, 11));

            Assert.True(board.IsFinished);
            Assert.Equal(GomokuOutcome.WhiteWins, board.Outcome);
            var line = board.FindWinningLine()!;
            Assert.Equal(5, line.Count);
            Assert.Equal(new CellPos(3, 11), line[0]);
            Assert.Equal(new CellPos(7, 11), line[4]);
        }

        [Fact]
        public void 主对角线五连判定黑方获胜()
        {
            var board = new GomokuBoard();
            // 黑子沿 (5,5)-(9,9) 连成右下对角五连。
            PlaySequence(board, (5, 5), (0, 0), (6, 6), (2, 0), (7, 7), (4, 0), (8, 8), (6, 0), (9, 9));

            Assert.True(board.IsFinished);
            Assert.Equal(GomokuOutcome.BlackWins, board.Outcome);
            var line = board.FindWinningLine()!;
            Assert.Equal(new CellPos(5, 5), line[0]);
            Assert.Equal(new CellPos(9, 9), line[4]);
        }

        [Fact]
        public void 副对角线五连判定黑方获胜()
        {
            var board = new GomokuBoard();
            // 黑子沿 (5,9)-(9,5) 连成左下对角五连。
            PlaySequence(board, (5, 9), (0, 0), (6, 8), (2, 0), (7, 7), (4, 0), (8, 6), (6, 0), (9, 5));

            Assert.True(board.IsFinished);
            Assert.Equal(GomokuOutcome.BlackWins, board.Outcome);
            var line = board.FindWinningLine()!;
            Assert.Equal(new CellPos(5, 9), line[0]);
            Assert.Equal(new CellPos(9, 5), line[4]);
        }

        [Fact]
        public void 七连长连同样判定黑方获胜()
        {
            var board = new GomokuBoard();
            // 黑子 (7,3)-(7,5) 与 (7,7)-(7,9) 两段三连，补上 (7,6) 一次形成七连；
            // 本作简化规则约定长连（六连及以上）同样按胜处理。
            PlaySequence(board, (7, 3), (0, 0), (7, 4), (2, 0), (7, 5), (4, 0), (7, 7), (6, 0), (7, 8), (8, 0), (7, 9), (10, 0), (7, 6));

            Assert.True(board.IsFinished);
            Assert.Equal(GomokuOutcome.BlackWins, board.Outcome);
            var line = board.FindWinningLine()!;
            Assert.Equal(7, line.Count);
            Assert.Equal(new CellPos(7, 3), line[0]);
            Assert.Equal(new CellPos(7, 9), line[6]);
            Assert.Equal(13, board.MoveCount);
        }

        [Fact]
        public void 评分函数为纯函数且同局面同分()
        {
            var cells = BuildBoard(new[]
            {
                (7, 4, Cell.Black), (7, 5, Cell.Black), (7, 6, Cell.Black),
            });
            var snapshot = (Cell[,])cells.Clone();

            // 活四点评分恒定，且调用不修改棋盘。
            int first = GomokuAi.ScorePoint(cells, 7, 3, Cell.Black);
            int second = GomokuAi.ScorePoint(cells, 7, 3, Cell.Black);
            Assert.Equal(GomokuAi.OwnFourScore, first);
            Assert.Equal(first, second);
            for (int row = 0; row < GomokuRules.Size; row++)
            {
                for (int col = 0; col < GomokuRules.Size; col++)
                {
                    Assert.Equal(snapshot[row, col], cells[row, col]);
                }
            }

            // 空盘无棋型候选按基础分加中心偏好：天元分最高，角落分最低。
            var empty = new Cell[GomokuRules.Size, GomokuRules.Size];
            Assert.Equal(GomokuAi.BaseScore, GomokuAi.ScorePoint(empty, GomokuRules.Center, GomokuRules.Center, Cell.Black));
            Assert.True(GomokuAi.ScorePoint(empty, 7, 7, Cell.Black) > GomokuAi.ScorePoint(empty, 0, 0, Cell.Black));
        }

        [Fact]
        public void 空棋盘AI决策落天元且完全确定()
        {
            var empty = new Cell[GomokuRules.Size, GomokuRules.Size];

            Assert.Equal(new CellPos(GomokuRules.Center, GomokuRules.Center), GomokuAi.BestMove(empty, Cell.Black));
            Assert.Equal(new CellPos(GomokuRules.Center, GomokuRules.Center), GomokuAi.BestMove(empty, Cell.Black));
            Assert.Equal(new CellPos(GomokuRules.Center, GomokuRules.Center), GomokuAi.BestMove(empty, Cell.White));
        }

        [Fact]
        public void 多个同分候选时取行列字典序最小()
        {
            // 黑子已有 (7,4)-(7,6) 活三，(7,3) 与 (7,7) 都是同分活四点，取行先列后的最小者 (7,3)。
            var cells = BuildBoard(new[]
            {
                (7, 4, Cell.Black), (7, 5, Cell.Black), (7, 6, Cell.Black),
            });

            Assert.Equal(new CellPos(7, 3), GomokuAi.BestMove(cells, Cell.Black));
            Assert.Equal(new CellPos(7, 3), GomokuAi.BestMove(cells, Cell.Black));
        }

        [Fact]
        public void AI能抓住一步成五的制胜手()
        {
            // 黑子 (7,4)-(7,7) 冲四，右端被白子封堵，落 (7,3) 立即成五。
            var cells = BuildBoard(new[]
            {
                (7, 4, Cell.Black), (7, 5, Cell.Black), (7, 6, Cell.Black), (7, 7, Cell.Black), (7, 8, Cell.White),
            });

            Assert.Equal(new CellPos(7, 3), GomokuAi.BestMove(cells, Cell.Black));
        }

        [Fact]
        public void AI能挡对手一步成五的冲四()
        {
            // 白子 (7,4)-(7,7) 冲四，仅 (7,3) 可成五，黑方必须立即堵截。
            var cells = BuildBoard(new[]
            {
                (7, 4, Cell.White), (7, 5, Cell.White), (7, 6, Cell.White), (7, 7, Cell.White), (7, 8, Cell.Black),
            });

            Assert.Equal(new CellPos(7, 3), GomokuAi.BestMove(cells, Cell.Black));
        }

        [Fact]
        public void AI落子必在空格且合法()
        {
            var empty = new Cell[GomokuRules.Size, GomokuRules.Size];
            var liveThree = BuildBoard(new[]
            {
                (7, 4, Cell.Black), (7, 5, Cell.Black), (7, 6, Cell.Black),
            });
            var rushFour = BuildBoard(new[]
            {
                (7, 4, Cell.White), (7, 5, Cell.White), (7, 6, Cell.White), (7, 7, Cell.White), (7, 8, Cell.Black),
            });
            var scattered = BuildBoard(new[]
            {
                (0, 0, Cell.Black), (2, 0, Cell.White), (5, 5, Cell.Black), (9, 9, Cell.White), (7, 7, Cell.Black),
            });

            AssertTakeEmpty(empty);
            AssertTakeEmpty(liveThree);
            AssertTakeEmpty(rushFour);
            AssertTakeEmpty(scattered);
        }

        [Fact]
        public void AI应对消极玩家获胜且比分跨局累计()
        {
            var match = new GomokuMatch();
            执黑消极落子让白方取胜(match);

            Assert.Equal(GomokuOutcome.WhiteWins, match.Board.Outcome);
            Assert.Equal(1, match.AiWins);
            Assert.Equal(0, match.PlayerWins);
            Assert.Equal(5, CountEvents(match, GomokuEventType.PlayerMoved));
            Assert.Equal(5, CountEvents(match, GomokuEventType.AiMoved));
            Assert.Equal(GomokuEventType.AiWon, match.Events[match.Events.Count - 1].Type);

            // 重开后同样应对，比分跨局累计到 2。
            match.Restart();
            执黑消极落子让白方取胜(match);

            Assert.Equal(2, match.Round);
            Assert.Equal(2, match.AiWins);
            Assert.Equal(0, match.PlayerWins);
        }

        [Fact]
        public void 玩家连成五连后终局比分与事件正确()
        {
            var match = new GomokuMatch();
            玩家连成纵向五连获胜(match);

            // 15x15 全满构造代价过高，平局路径以终局后比分正确覆盖。
            Assert.Equal(1, match.PlayerWins);
            Assert.Equal(0, match.AiWins);
            Assert.Equal(0, match.Draws);
            Assert.Equal(5, CountEvents(match, GomokuEventType.PlayerMoved));
            Assert.Equal(0, CountEvents(match, GomokuEventType.AiMoved));
            Assert.Equal(GomokuEventType.PlayerWon, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 重开一局清空棋盘且比分保留()
        {
            var match = new GomokuMatch();
            玩家连成纵向五连获胜(match);
            Assert.Equal(1, match.PlayerWins);

            match.Restart();

            Assert.Equal(2, match.Round);
            Assert.Equal(0, match.Board.MoveCount);
            Assert.Null(match.Board.LastMove);
            Assert.Equal(Cell.Black, match.Board.CurrentTurn);
            Assert.False(match.Board.IsFinished);
            Assert.Equal(GomokuOutcome.None, match.Board.Outcome);
            Assert.Equal(1, match.PlayerWins);
            Assert.Equal(GomokuEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 对局事件日志超限时封顶裁剪()
        {
            var match = new GomokuMatch();
            for (int round = 0; round < 50; round++)
            {
                if (round > 0) match.Restart();
                Assert.True(match.PlayerMove(0, 0));
                Assert.True(match.AiMove());
            }

            Assert.True(match.Events.Count <= 128, "事件日志必须按上限封顶。");
            // 裁剪后保留的是最近事件，首条不应再属于第 1 局。
            Assert.NotEqual(1, match.Events[0].Round);
        }

        /// <summary>按给定坐标顺序黑白轮流落子，任何一步失败即测试失败。</summary>
        private static void PlaySequence(GomokuBoard board, params (int Row, int Col)[] moves)
        {
            foreach (var (row, col) in moves) Assert.True(board.Play(row, col), $"落子 ({row}, {col}) 应当成功。");
        }

        /// <summary>按 [行,列] 坐标批量放置棋子，构造指定局面。</summary>
        private static Cell[,] BuildBoard((int Row, int Col, Cell Cell)[] stones)
        {
            var cells = new Cell[GomokuRules.Size, GomokuRules.Size];
            foreach (var stone in stones) cells[stone.Row, stone.Col] = stone.Cell;
            return cells;
        }

        /// <summary>断言 AI 在给定局面下的决策落在棋盘内的空格上。</summary>
        private static void AssertTakeEmpty(Cell[,] cells)
        {
            var move = GomokuAi.BestMove(cells, Cell.Black);
            Assert.InRange(move.Row, 0, GomokuRules.Size - 1);
            Assert.InRange(move.Col, 0, GomokuRules.Size - 1);
            Assert.Equal(Cell.None, cells[move.Row, move.Col]);
        }

        /// <summary>黑方固定在第 0 列间隔落子不设防，白方按启发式评分应答直至获胜。</summary>
        private static void 执黑消极落子让白方取胜(GomokuMatch match)
        {
            Assert.True(match.PlayerMove(0, 0));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(2, 0));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(4, 0));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(6, 0));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(8, 0));
            Assert.True(match.AiMove());
            Assert.True(match.Board.IsFinished, "白方应在第 5 手应答后连成五连获胜。");
        }

        /// <summary>白方用核心棋盘直接应答（0 行分散落子），玩家在第 7 列连成纵向五连。</summary>
        private static void 玩家连成纵向五连获胜(GomokuMatch match)
        {
            Assert.True(match.PlayerMove(3, 7));
            Assert.True(match.Board.Play(0, 0));
            Assert.True(match.PlayerMove(4, 7));
            Assert.True(match.Board.Play(0, 1));
            Assert.True(match.PlayerMove(5, 7));
            Assert.True(match.Board.Play(0, 2));
            Assert.True(match.PlayerMove(6, 7));
            Assert.True(match.Board.Play(0, 3));
            Assert.True(match.PlayerMove(7, 7));
            Assert.Equal(GomokuOutcome.BlackWins, match.Board.Outcome);
        }

        private static int CountEvents(GomokuMatch match, GomokuEventType type)
        {
            int count = 0;
            foreach (var entry in match.Events)
            {
                if (entry.Type == type) count++;
            }

            return count;
        }

        private static IEnumerable<Cell> EnumerateCells(Cell[,] cells)
        {
            foreach (var cell in cells) yield return cell;
        }
    }
}
