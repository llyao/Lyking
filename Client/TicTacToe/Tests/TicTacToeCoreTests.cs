using System.Collections.Generic;
using Lyking.TicTacToe.Core;
using Xunit;

namespace Lyking.TicTacToe.Core.Tests
{
    /// <summary>井字棋核心规则测试：胜负判定、平局、落子拒绝、AI 完美性与对局比分事件。</summary>
    public class TicTacToeCoreTests
    {
        [Fact]
        public void 空棋盘初始为X先手且未结束()
        {
            var board = new TicTacToeBoard();

            Assert.Equal(TicTacToeRules.CellCount, board.Cells.Count);
            Assert.All(board.Cells, cell => Assert.Equal(Cell.None, cell));
            Assert.Equal(Cell.X, board.CurrentTurn);
            Assert.Equal(0, board.MoveCount);
            Assert.False(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.None, board.Outcome);
        }

        [Fact]
        public void 行三连判定X获胜并结束对局()
        {
            var board = new TicTacToeBoard();
            // X 取 0、1、2 成首行，O 取 3、4 填充。
            PlaySequence(board, 0, 3, 1, 4, 2);

            Assert.True(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.XWins, board.Outcome);
            Assert.Equal(Cell.X, TicTacToeRules.FindWinner(board.Cells));
            Assert.Equal(5, board.MoveCount);
        }

        [Fact]
        public void 列三连判定O获胜()
        {
            var board = new TicTacToeBoard();
            // O 取 1、4、7 成中列，X 取 0、2、8 填充且不成线。
            PlaySequence(board, 0, 1, 2, 4, 8, 7);

            Assert.True(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.OWins, board.Outcome);
            Assert.Equal(Cell.O, TicTacToeRules.FindWinner(board.Cells));
        }

        [Fact]
        public void 主对角线三连判定X获胜()
        {
            var board = new TicTacToeBoard();
            // X 取 0、4、8 成主对角线。
            PlaySequence(board, 0, 1, 4, 3, 8);

            Assert.True(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.XWins, board.Outcome);
        }

        [Fact]
        public void 副对角线三连判定O获胜()
        {
            var board = new TicTacToeBoard();
            // O 取 2、4、6 成副对角线，末手同时封堵 X 的 0-3-6 线。
            PlaySequence(board, 0, 2, 8, 4, 3, 6);

            Assert.True(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.OWins, board.Outcome);
        }

        [Fact]
        public void 棋满无胜判为平局()
        {
            var board = new TicTacToeBoard();
            // 双方交错填满且均无三连的经典平局局面。
            PlaySequence(board, 0, 4, 8, 2, 1, 3, 6, 7, 5);

            Assert.True(board.IsFinished);
            Assert.Equal(TicTacToeOutcome.Draw, board.Outcome);
            Assert.Equal(TicTacToeRules.CellCount, board.MoveCount);
        }

        [Fact]
        public void 已占格子拒绝落子且状态不变()
        {
            var board = new TicTacToeBoard();
            Assert.True(board.Play(0));

            Assert.False(board.Play(0));
            Assert.Equal(1, board.MoveCount);
            Assert.Equal(Cell.X, board.Cells[0]);
            Assert.Equal(Cell.O, board.CurrentTurn);
        }

        [Fact]
        public void 越界索引拒绝落子()
        {
            var board = new TicTacToeBoard();

            Assert.False(board.Play(-1));
            Assert.False(board.Play(TicTacToeRules.CellCount));
            Assert.Equal(0, board.MoveCount);
            Assert.Equal(Cell.X, board.CurrentTurn);
        }

        [Fact]
        public void 终局后拒绝落子且棋盘保持终局形状()
        {
            var board = new TicTacToeBoard();
            PlaySequence(board, 0, 3, 1, 4, 2);
            Assert.True(board.IsFinished);
            var snapshot = new Cell[TicTacToeRules.CellCount];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = board.Cells[i];

            Assert.False(board.Play(5));
            Assert.False(board.Play(-1));
            Assert.Equal(5, board.MoveCount);
            for (int i = 0; i < snapshot.Length; i++) Assert.Equal(snapshot[i], board.Cells[i]);
        }

        [Fact]
        public void 成功落子后轮换先手方()
        {
            var board = new TicTacToeBoard();

            Assert.True(board.Play(0));
            Assert.Equal(Cell.O, board.CurrentTurn);
            Assert.True(board.Play(4));
            Assert.Equal(Cell.X, board.CurrentTurn);
        }

        [Fact]
        public void 空棋盘最优落点为最小索引且完全确定()
        {
            var empty = new Cell[TicTacToeRules.CellCount];

            Assert.Equal(0, TicTacToeAi.BestMove(empty, Cell.X));
            Assert.Equal(0, TicTacToeAi.BestMove(empty, Cell.X));
            Assert.Equal(0, TicTacToeAi.BestMove(empty, Cell.O));
        }

        [Fact]
        public void AI能抓住必胜手()
        {
            // O 已占 0、4，落在 8 立即连成主对角线；X 无即时威胁可反制。
            var cells = new[] { Cell.O, Cell.None, Cell.X, Cell.None, Cell.O, Cell.None, Cell.X, Cell.None, Cell.None };

            Assert.Equal(8, TicTacToeAi.BestMove(cells, Cell.O));
        }

        [Fact]
        public void AI能挡住必败手()
        {
            // X 已占 0、1，O 不堵 2 则下回合必败，堵 2 是唯一不败着。
            var cells = new[] { Cell.X, Cell.X, Cell.None, Cell.None, Cell.O, Cell.None, Cell.None, Cell.None, Cell.None };

            Assert.Equal(2, TicTacToeAi.BestMove(cells, Cell.O));
        }

        [Fact]
        public void 穷举玩家全部策略AI均不败()
        {
            // AI 执 O：X 的 9 个首步及全部后续组合逐一穷举，O 每步取 minimax 最优。
            var cells = new Cell[TicTacToeRules.CellCount];

            Assert.False(PlayerCanForceWin(cells, Cell.X, 0), "完美 AI 不应被任何玩家策略击败。");
        }

        [Fact]
        public void 双方完美对弈九个首步均为平局()
        {
            // 玩家任取一个首步后双方都用 BestMove 收官：AI 至少不败、玩家最优至少逼平，结果必为平局。
            for (int first = 0; first < TicTacToeRules.CellCount; first++)
            {
                var cells = new Cell[TicTacToeRules.CellCount];
                cells[first] = Cell.X;

                Assert.Equal(TicTacToeOutcome.Draw, PlayOutWithBestMoves(cells, Cell.O));
            }
        }

        [Fact]
        public void AI获胜一局后比分累计且事件完整()
        {
            var match = new TicTacToeMatch();
            // 玩家连出败着（0、1、3），AI 应对后于第三手抓住制胜点获胜。
            Assert.True(match.PlayerMove(0));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(1));
            Assert.True(match.AiMove());
            Assert.True(match.PlayerMove(3));
            Assert.True(match.AiMove());

            Assert.Equal(TicTacToeOutcome.OWins, match.Board.Outcome);
            Assert.Equal(1, match.AiWins);
            Assert.Equal(0, match.PlayerWins);
            Assert.Equal(0, match.Draws);
            Assert.Equal(TicTacToeEventType.AiWon, match.Events[match.Events.Count - 1].Type);
            Assert.Equal(3, CountEvents(match, TicTacToeEventType.PlayerMoved));
            Assert.Equal(3, CountEvents(match, TicTacToeEventType.AiMoved));
        }

        [Fact]
        public void 完美对弈成平局后重开保留比分并清空棋盘()
        {
            var match = new TicTacToeMatch();
            Assert.True(match.PlayerMove(0));
            PlayPerfectRounds(match);
            Assert.Equal(TicTacToeOutcome.Draw, match.Board.Outcome);
            Assert.Equal(1, match.Draws);

            match.Restart();

            Assert.Equal(2, match.Round);
            Assert.Equal(0, match.Board.MoveCount);
            Assert.Equal(Cell.X, match.Board.CurrentTurn);
            Assert.False(match.Board.IsFinished);
            Assert.Equal(1, match.Draws);
            Assert.Equal(TicTacToeEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 对局事件日志超限时封顶裁剪()
        {
            var match = new TicTacToeMatch();
            for (int round = 0; round < 30; round++)
            {
                if (round > 0) match.Restart();
                Assert.True(match.PlayerMove(0));
                PlayPerfectRounds(match);
            }

            Assert.True(match.Events.Count <= 128, "事件日志必须按上限封顶。");
            // 裁剪后保留的是最近事件，首条不应再属于第 1 局。
            Assert.NotEqual(1, match.Events[0].Round);
        }

        /// <summary>按给定索引顺序落子，任何一步失败即测试失败。</summary>
        private static void PlaySequence(TicTacToeBoard board, params int[] indexes)
        {
            foreach (var index in indexes) Assert.True(board.Play(index), $"落子 {index} 应当成功。");
        }

        /// <summary>以双方最优着法把当前局面收官（玩家首步已落）。</summary>
        private static void PlayPerfectRounds(TicTacToeMatch match)
        {
            while (!match.Board.IsFinished)
            {
                if (match.IsPlayerTurn)
                    Assert.True(match.PlayerMove(TicTacToeAi.BestMove(match.Board.Cells, Cell.X)), "完美玩家落子应当成功。");
                else
                    Assert.True(match.AiMove(), "AI 应答应当成功。");
            }
        }

        /// <summary>穷举玩家方所有落点（AI 方固定用 BestMove），返回玩家是否存在取胜路线。</summary>
        private static bool PlayerCanForceWin(Cell[] cells, Cell turn, int stones)
        {
            var winner = TicTacToeRules.FindWinner(cells);
            if (winner == Cell.X) return true;
            if (winner == Cell.O || stones == TicTacToeRules.CellCount) return false;

            if (turn == Cell.O)
            {
                int move = TicTacToeAi.BestMove(cells, Cell.O);
                if (move < 0) return false;

                cells[move] = Cell.O;
                bool wins = PlayerCanForceWin(cells, Cell.X, stones + 1);
                cells[move] = Cell.None;
                return wins;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != Cell.None) continue;

                cells[i] = Cell.X;
                bool wins = PlayerCanForceWin(cells, Cell.O, stones + 1);
                cells[i] = Cell.None;
                if (wins) return true;
            }

            return false;
        }

        /// <summary>双方都用 BestMove 收官并返回终局结果。</summary>
        private static TicTacToeOutcome PlayOutWithBestMoves(Cell[] cells, Cell turn)
        {
            var winner = TicTacToeRules.FindWinner(cells);
            if (winner == Cell.X) return TicTacToeOutcome.XWins;
            if (winner == Cell.O) return TicTacToeOutcome.OWins;
            if (TicTacToeRules.IsFull(cells)) return TicTacToeOutcome.Draw;

            int move = TicTacToeAi.BestMove(cells, turn);
            Assert.True(move >= 0, "未终局局面必然存在可行落点。");

            cells[move] = turn;
            var outcome = PlayOutWithBestMoves(cells, turn == Cell.X ? Cell.O : Cell.X);
            cells[move] = Cell.None;
            return outcome;
        }

        private static int CountEvents(TicTacToeMatch match, TicTacToeEventType type)
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
