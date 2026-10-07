using System.Collections.Generic;
using Lyking.ConnectFour.Core;
using Xunit;

namespace Lyking.ConnectFour.Core.Tests
{
    /// <summary>四子连珠核心规则测试：重力落子、四连判定、平局、AI 决策与对局比分事件。</summary>
    public class ConnectFourCoreTests
    {
        /// <summary>42 步棋满且双方均无四连的落子列序列（红先手，奇偶手交替着色）。</summary>
        private static readonly int[] DrawSequence =
        {
            4, 1, 6, 2, 1, 0, 1, 4, 4, 2, 4, 1, 3, 6, 3, 3, 1, 0, 4, 6,
            6, 1, 2, 3, 2, 3, 2, 2, 6, 5, 5, 0, 5, 4, 6, 5, 5, 5, 0, 0, 3, 0,
        };

        [Fact]
        public void 空棋盘初始为红先手且棋盘尺寸正确()
        {
            var board = new ConnectFourBoard();

            Assert.Equal(ConnectFourRules.Columns, board.Cells.GetLength(0));
            Assert.Equal(ConnectFourRules.Rows, board.Cells.GetLength(1));
            Assert.All(EnumerateCells(board.Cells), cell => Assert.Equal(Cell.None, cell));
            Assert.Equal(Cell.Red, board.CurrentTurn);
            Assert.Equal(0, board.MoveCount);
            Assert.False(board.IsFinished);
            Assert.Equal(ConnectFourOutcome.None, board.Outcome);
            Assert.Null(board.WinningLine);
        }

        [Fact]
        public void 落子落至指定列最底部()
        {
            var board = new ConnectFourBoard();

            Assert.True(board.Drop(3));
            Assert.Equal(Cell.Red, board[3, 0]);
            Assert.Equal(1, ConnectFourRules.LowestEmptyRow(board.Cells, 3));
        }

        [Fact]
        public void 同列逐格向上堆叠()
        {
            var board = new ConnectFourBoard();
            // 红黄交替往中列连投三子，逐行堆叠且互不覆盖。
            Assert.True(board.Drop(3));
            Assert.True(board.Drop(3));
            Assert.True(board.Drop(3));

            Assert.Equal(Cell.Red, board[3, 0]);
            Assert.Equal(Cell.Yellow, board[3, 1]);
            Assert.Equal(Cell.Red, board[3, 2]);
            Assert.Equal(Cell.None, board[3, 3]);
            Assert.Equal(3, board.MoveCount);
        }

        [Fact]
        public void 满列拒绝落子且状态不变()
        {
            var board = new ConnectFourBoard();
            for (int i = 0; i < ConnectFourRules.Rows; i++) Assert.True(board.Drop(2));

            Assert.False(board.Drop(2));
            Assert.False(board.CanDrop(2));
            Assert.Equal(-1, ConnectFourRules.LowestEmptyRow(board.Cells, 2));
            Assert.Equal(ConnectFourRules.Rows, board.MoveCount);
            Assert.Equal(Cell.Yellow, board[2, 5]);
        }

        [Fact]
        public void 越界列拒绝落子()
        {
            var board = new ConnectFourBoard();

            Assert.False(board.Drop(-1));
            Assert.False(board.Drop(ConnectFourRules.Columns));
            Assert.Equal(0, board.MoveCount);
            Assert.Equal(Cell.Red, board.CurrentTurn);
        }

        [Fact]
        public void 成功落子后轮换行棋方()
        {
            var board = new ConnectFourBoard();

            Assert.True(board.Drop(0));
            Assert.Equal(Cell.Yellow, board.CurrentTurn);
            Assert.True(board.Drop(1));
            Assert.Equal(Cell.Red, board.CurrentTurn);
        }

        [Fact]
        public void 横向四连胜判定红方获胜()
        {
            var board = new ConnectFourBoard();
            // 红方在底行 0-3 列连成四子，黄子在 6 列堆叠三子不成线。
            PlaySequence(board, 0, 6, 1, 6, 2, 6, 3);

            Assert.True(board.IsFinished);
            Assert.Equal(ConnectFourOutcome.RedWins, board.Outcome);
            Assert.Equal(Cell.Red, ConnectFourRules.FindWinner(board.Cells));
            var line = board.WinningLine!;
            Assert.Equal(4, line.Count);
            Assert.Equal(0, line[0].Col);
            Assert.Equal(3, line[3].Col);
            Assert.Equal(0, line[0].Row);
            Assert.Equal(7, board.MoveCount);
        }

        [Fact]
        public void 纵向四连胜判定黄方获胜()
        {
            var board = new ConnectFourBoard();
            // 黄方在 5 列连投四子成纵向四连，红方分散落子不成线。
            PlaySequence(board, 0, 5, 2, 5, 4, 5, 6, 5);

            Assert.True(board.IsFinished);
            Assert.Equal(ConnectFourOutcome.YellowWins, board.Outcome);
            Assert.Equal(Cell.Yellow, ConnectFourRules.FindWinner(board.Cells));
            var line = board.WinningLine!;
            Assert.Equal(4, line.Count);
            Assert.Equal(5, line[0].Col);
            Assert.Equal(5, line[3].Col);
            Assert.Equal(0, line[0].Row);
            Assert.Equal(3, line[3].Row);
        }

        [Fact]
        public void 主对角线四连胜判定红方获胜()
        {
            var board = new ConnectFourBoard();
            // 红方借黄子垫底连成 (0,0)-(1,1)-(2,2)-(3,3) 的上斜四连。
            PlaySequence(board, 0, 1, 1, 3, 2, 2, 2, 6, 3, 6, 3, 6, 3);

            Assert.True(board.IsFinished);
            Assert.Equal(ConnectFourOutcome.RedWins, board.Outcome);
            var line = board.WinningLine!;
            Assert.Equal(4, line.Count);
            Assert.Equal(0, line[0].Col);
            Assert.Equal(0, line[0].Row);
            Assert.Equal(3, line[3].Col);
            Assert.Equal(3, line[3].Row);
        }

        [Fact]
        public void 副对角线四连及其获胜坐标()
        {
            // 手工构造 (0,3)-(1,2)-(2,1)-(3,0) 的下斜四连，验证扫描方向与坐标顺序。
            var cells = BuildBoard(new[]
            {
                (3, 0, Cell.Red),
                (2, 1, Cell.Red),
                (1, 2, Cell.Red),
                (0, 3, Cell.Red),
            });

            Assert.Equal(Cell.Red, ConnectFourRules.FindWinner(cells));
            var line = ConnectFourRules.FindWinningLine(cells)!;
            Assert.Equal(4, line.Count);
            Assert.Equal((0, 3), (line[0].Col, line[0].Row));
            Assert.Equal((3, 0), (line[3].Col, line[3].Row));
        }

        [Fact]
        public void 棋满无胜判为平局()
        {
            var board = new ConnectFourBoard();
            PlaySequence(board, DrawSequence);

            Assert.True(board.IsFinished);
            Assert.Equal(ConnectFourOutcome.Draw, board.Outcome);
            Assert.Equal(ConnectFourRules.CellCount, board.MoveCount);
            Assert.True(ConnectFourRules.IsFull(board.Cells));
            Assert.Null(board.WinningLine);
        }

        [Fact]
        public void 终局后拒绝落子且棋盘保持终局形状()
        {
            var board = new ConnectFourBoard();
            PlaySequence(board, 0, 6, 1, 6, 2, 6, 3);
            Assert.True(board.IsFinished);
            var snapshot = new List<Cell>(EnumerateCells(board.Cells));

            Assert.False(board.Drop(4));
            Assert.False(board.Drop(-1));
            Assert.False(board.CanDrop(4));
            Assert.Equal(7, board.MoveCount);
            Assert.Equal(snapshot, new List<Cell>(EnumerateCells(board.Cells)));
        }

        [Fact]
        public void 空棋盘AI决策完全确定()
        {
            var empty = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];

            int redColumn = ConnectFourAi.BestColumn(empty, Cell.Red);
            Assert.InRange(redColumn, 0, ConnectFourRules.Columns - 1);
            Assert.Equal(redColumn, ConnectFourAi.BestColumn(empty, Cell.Red));

            int yellowColumn = ConnectFourAi.BestColumn(empty, Cell.Yellow);
            Assert.InRange(yellowColumn, 0, ConnectFourRules.Columns - 1);
            Assert.Equal(yellowColumn, ConnectFourAi.BestColumn(empty, Cell.Yellow));
        }

        [Fact]
        public void 多个即胜手时AI取最小列索引()
        {
            // 黄方在 0 列与 6 列各差一子成纵向四连，两手即胜评分相同，应取最小列 0。
            var cells = BuildBoard(new[]
            {
                (0, 0, Cell.Yellow), (0, 1, Cell.Yellow), (0, 2, Cell.Yellow),
                (6, 0, Cell.Yellow), (6, 1, Cell.Yellow), (6, 2, Cell.Yellow),
                (3, 0, Cell.Red), (3, 1, Cell.Red), (3, 2, Cell.Red),
            });

            Assert.Equal(0, ConnectFourAi.BestColumn(cells, Cell.Yellow));
        }

        [Fact]
        public void AI能抓住即胜手()
        {
            // 黄方在 0 列已堆三子，落 0 列立即成四；红方 2x2 方块无即时威胁。
            var cells = BuildBoard(new[]
            {
                (0, 0, Cell.Yellow), (0, 1, Cell.Yellow), (0, 2, Cell.Yellow),
                (3, 0, Cell.Red), (3, 1, Cell.Red), (4, 0, Cell.Red), (4, 1, Cell.Red),
            });

            Assert.Equal(0, ConnectFourAi.BestColumn(cells, Cell.Yellow));
            Assert.Equal(0, ConnectFourAi.BestColumn(cells, Cell.Yellow));
        }

        [Fact]
        public void AI能挡住对手即胜手()
        {
            // 红方在 3 列已堆三子，黄方不堵 3 列则下回合必败。
            var cells = BuildBoard(new[]
            {
                (3, 0, Cell.Red), (3, 1, Cell.Red), (3, 2, Cell.Red),
                (0, 0, Cell.Yellow), (1, 0, Cell.Yellow),
            });

            Assert.Equal(3, ConnectFourAi.BestColumn(cells, Cell.Yellow));
        }

        [Fact]
        public void AI避开会送对手一步成四的列()
        {
            // 红方在行 1 的 2-4 列潜伏三连：黄方落 1 列或 5 列会垫出对手的即胜点，应避开。
            var cells = BuildBoard(new[]
            {
                (2, 0, Cell.Yellow), (3, 0, Cell.Yellow), (4, 0, Cell.Red),
                (2, 1, Cell.Red), (3, 1, Cell.Red), (4, 1, Cell.Red),
            });

            int column = ConnectFourAi.BestColumn(cells, Cell.Yellow);
            Assert.InRange(column, 0, ConnectFourRules.Columns - 1);
            Assert.NotEqual(1, column);
            Assert.NotEqual(5, column);
            Assert.Equal(column, ConnectFourAi.BestColumn(cells, Cell.Yellow));
        }

        [Fact]
        public void AI对终局或满盘局面返回不可落子()
        {
            var wonBoard = new ConnectFourBoard();
            PlaySequence(wonBoard, 0, 6, 1, 6, 2, 6, 3);
            var fullBoard = new ConnectFourBoard();
            PlaySequence(fullBoard, DrawSequence);
            var empty = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];

            Assert.Equal(-1, ConnectFourAi.BestColumn(wonBoard.Cells, Cell.Yellow));
            Assert.Equal(-1, ConnectFourAi.BestColumn(fullBoard.Cells, Cell.Red));
            Assert.Equal(-1, ConnectFourAi.BestColumn(empty, Cell.None));
        }

        [Fact]
        public void AI获胜一局后比分累计且事件完整()
        {
            var match = new ConnectFourMatch();
            玩家执败着让AI获胜(match);

            Assert.Equal(ConnectFourOutcome.YellowWins, match.Board.Outcome);
            Assert.Equal(1, match.AiWins);
            Assert.Equal(0, match.PlayerWins);
            Assert.Equal(0, match.Draws);
            Assert.Equal(ConnectFourEventType.AiWon, match.Events[match.Events.Count - 1].Type);
            Assert.Equal(4, CountEvents(match, ConnectFourEventType.PlayerDropped));
            Assert.Equal(1, CountEvents(match, ConnectFourEventType.AiDropped));
        }

        [Fact]
        public void 玩家获胜一局后跨局比分累计()
        {
            var match = new ConnectFourMatch();
            // 第一局：AI 胜（构造见 玩家执败着让AI获胜）。
            玩家执败着让AI获胜(match);
            Assert.Equal(1, match.AiWins);

            match.Restart();
            // 第二局：黄子被驱动去 6 列堆叠，玩家在 0 列连成纵向四连获胜。
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));

            Assert.Equal(ConnectFourOutcome.RedWins, match.Board.Outcome);
            Assert.Equal(1, match.PlayerWins);
            Assert.Equal(1, match.AiWins);
            Assert.Equal(0, match.Draws);
            Assert.Equal(2, match.Round);
            Assert.Equal(ConnectFourEventType.PlayerWon, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 重开清盘且比分保留()
        {
            var match = new ConnectFourMatch();
            玩家执败着让AI获胜(match);
            Assert.Equal(1, match.AiWins);

            match.Restart();

            Assert.Equal(2, match.Round);
            Assert.Equal(0, match.Board.MoveCount);
            Assert.Equal(Cell.Red, match.Board.CurrentTurn);
            Assert.False(match.Board.IsFinished);
            Assert.Equal(ConnectFourOutcome.None, match.Board.Outcome);
            Assert.Equal(1, match.AiWins);
            Assert.Equal(ConnectFourEventType.Restarted, match.Events[match.Events.Count - 1].Type);
        }

        [Fact]
        public void 对局事件日志超限时封顶裁剪()
        {
            var match = new ConnectFourMatch();
            玩家执败着让AI获胜(match);
            Assert.True(match.Board.IsFinished);

            // 终局后的落子请求全部被拒并计入日志，触发封顶裁剪。
            for (int i = 0; i < 300; i++) Assert.False(match.PlayerDrop(0));

            Assert.Equal(128, match.Events.Count);
            Assert.All(match.Events, entry => Assert.Equal(ConnectFourEventType.MoveRejected, entry.Type));
        }

        /// <summary>按给定列顺序落子，任何一步失败即测试失败。</summary>
        private static void PlaySequence(ConnectFourBoard board, params int[] columns)
        {
            foreach (var column in columns) Assert.True(board.Drop(column), $"往列 {column} 落子应当成功。");
        }

        /// <summary>
        /// 玩家在底行连出败着（0、1、2、4 列），黄子由棋盘直接驱动在 3 列堆叠三子，
        /// 即胜点无人封锁，AI 于第 8 手抓住即胜手获胜。
        /// </summary>
        private static void 玩家执败着让AI获胜(ConnectFourMatch match)
        {
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(3));
            Assert.True(match.PlayerDrop(1));
            Assert.True(match.Board.Drop(3));
            Assert.True(match.PlayerDrop(2));
            Assert.True(match.Board.Drop(3));
            Assert.True(match.PlayerDrop(4));
            Assert.True(match.AiDrop());
            Assert.Equal(ConnectFourOutcome.YellowWins, match.Board.Outcome);
        }

        /// <summary>黄子被直接驱动去 6 列堆叠三子，玩家在 0 列连成纵向四子获胜。</summary>
        private static void 玩家连成纵向四子获胜(ConnectFourMatch match)
        {
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));
            Assert.True(match.Board.Drop(6));
            Assert.True(match.PlayerDrop(0));
            Assert.Equal(ConnectFourOutcome.RedWins, match.Board.Outcome);
        }

        /// <summary>按 [列,行] 坐标批量放置棋子，构造指定局面。</summary>
        private static Cell[,] BuildBoard((int Col, int Row, Cell Cell)[] stones)
        {
            var cells = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];
            foreach (var stone in stones) cells[stone.Col, stone.Row] = stone.Cell;
            return cells;
        }

        private static int CountEvents(ConnectFourMatch match, ConnectFourEventType type)
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
            for (int col = 0; col < cells.GetLength(0); col++)
            {
                for (int row = 0; row < cells.GetLength(1); row++)
                {
                    yield return cells[col, row];
                }
            }
        }
    }
}
