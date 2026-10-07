using System;
using System.Collections.Generic;

namespace Lyking.Reversi.Core
{
    /// <summary>
    /// 黑白棋核心棋盘：确定性离散回合制状态机，无 Unity 依赖、无随机数。
    /// 黑先手，双方轮流在能翻转至少一枚对方棋子的空格落子；
    /// 一方无合法步时自动过手由对方继续，双方均无步或棋盘下满即按盘面子数终局。
    /// 非法落点（越界、已占、不翻转任何子或对局已结束）被直接拒绝并记录事件。
    /// </summary>
    public sealed class ReversiBoard
    {
        private const int MaxEventLogs = 128;

        private readonly Cell[,] _cells = new Cell[ReversiRules.Size, ReversiRules.Size];
        private readonly List<ReversiEventLog> _events = new List<ReversiEventLog>();

        public ReversiBoard()
        {
            ReversiRules.PlaceInitialStones(_cells);
            CurrentTurn = Cell.Black;
        }

        /// <summary>以给定盘面构造棋盘（复盘与测试用）：复制盘面并校正行棋方与终局状态，不写事件。</summary>
        public ReversiBoard(Cell[,] cells, Cell turn)
        {
            if (cells.GetLength(0) != ReversiRules.Size || cells.GetLength(1) != ReversiRules.Size)
            {
                throw new ArgumentException("盘面尺寸必须为 8x8。", nameof(cells));
            }

            if (turn == Cell.None) throw new ArgumentException("行棋方不能为空。", nameof(turn));

            for (int row = 0; row < ReversiRules.Size; row++)
            {
                for (int col = 0; col < ReversiRules.Size; col++)
                {
                    _cells[row, col] = cells[row, col];
                    if (cells[row, col] != Cell.None) MoveCount++;
                }
            }

            CurrentTurn = turn;
            if (ReversiRules.HasAnyMove(_cells, turn)) return;

            // 当前方无步：对方可行棋则过手，否则双方均无步直接终局。
            var opponent = ReversiRules.OpponentOf(turn);
            if (ReversiRules.HasAnyMove(_cells, opponent)) CurrentTurn = opponent;
            else SettleOutcome();
        }

        /// <summary>棋盘格占用方，索引 [行, 列]。</summary>
        public Cell this[int row, int col] => _cells[row, col];

        /// <summary>当前该落子的一方。</summary>
        public Cell CurrentTurn { get; private set; }

        /// <summary>已落子手数（过手不计）。</summary>
        public int MoveCount { get; private set; }

        public bool IsFinished { get; private set; }

        public ReversiOutcome Outcome { get; private set; } = ReversiOutcome.None;

        /// <summary>最后一步落点；本局尚未落子时为 null。</summary>
        public (int Row, int Col)? LastMove { get; private set; }

        /// <summary>黑白双方盘面子数。</summary>
        public (int Black, int White) Counts
        {
            get
            {
                int black = 0;
                int white = 0;
                for (int row = 0; row < ReversiRules.Size; row++)
                {
                    for (int col = 0; col < ReversiRules.Size; col++)
                    {
                        if (_cells[row, col] == Cell.Black) black++;
                        else if (_cells[row, col] == Cell.White) white++;
                    }
                }

                return (black, white);
            }
        }

        /// <summary>本局事件日志（落子带翻转数、过手、终局与非法拒绝，封顶裁剪）。</summary>
        public IReadOnlyList<ReversiEventLog> Events => _events;

        /// <summary><paramref name="side"/> 一方的全部合法步（行优先顺序，含各自翻转集合）。</summary>
        public IReadOnlyList<ReversiMove> LegalMoves(Cell side)
        {
            return ReversiRules.CollectMoves(_cells, side);
        }

        /// <summary>当前行棋方在指定格子落子。非法落点被拒绝并记录事件，返回是否成功。</summary>
        public bool Play(int row, int col)
        {
            if (IsFinished || row < 0 || row >= ReversiRules.Size || col < 0 || col >= ReversiRules.Size
                || _cells[row, col] != Cell.None)
            {
                return Reject(row, col);
            }

            var flipped = ReversiRules.FlipsFor(_cells, row, col, CurrentTurn);
            if (flipped.Count == 0) return Reject(row, col);

            var mover = CurrentTurn;
            _cells[row, col] = mover;
            for (int i = 0; i < flipped.Count; i++)
            {
                int index = flipped[i];
                _cells[index / ReversiRules.Size, index % ReversiRules.Size] = mover;
            }

            MoveCount++;
            LastMove = (row, col);
            Log(mover == Cell.Black ? ReversiEventType.PlayerMoved : ReversiEventType.AiMoved, mover, row, col, flipped.Count);
            AdvanceTurn();
            return true;
        }

        /// <summary>拒绝非法落点：记录事件并保持盘面与行棋方不变。</summary>
        private bool Reject(int row, int col)
        {
            Log(ReversiEventType.IllegalRejected, CurrentTurn, row, col, 0);
            return false;
        }

        /// <summary>换手：对方有步则轮换；对方无步则记录过手由己方继续；双方均无步或棋满即终局。</summary>
        private void AdvanceTurn()
        {
            var opponent = ReversiRules.OpponentOf(CurrentTurn);
            if (ReversiRules.HasAnyMove(_cells, opponent))
            {
                CurrentTurn = opponent;
                return;
            }

            // 对方无合法步：仍有空格时记录过手事件；双方均无步或棋盘下满即终局。
            var counts = Counts;
            if (counts.Black + counts.White < ReversiRules.CellCount)
            {
                Log(ReversiEventType.Passed, opponent, -1, -1, 0);
            }

            if (ReversiRules.HasAnyMove(_cells, CurrentTurn)) return;

            SettleOutcome();
            Log(ReversiEventType.Finished, Cell.None, -1, -1, 0);
        }

        /// <summary>按盘面子数裁定胜负或平局。</summary>
        private void SettleOutcome()
        {
            var counts = Counts;
            Outcome = counts.Black > counts.White ? ReversiOutcome.BlackWins
                : counts.White > counts.Black ? ReversiOutcome.WhiteWins : ReversiOutcome.Draw;
            IsFinished = true;
        }

        private void Log(ReversiEventType type, Cell side, int row, int col, int flipped)
        {
            _events.Add(new ReversiEventLog(0, type, side, MoveCount, row, col, flipped));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
