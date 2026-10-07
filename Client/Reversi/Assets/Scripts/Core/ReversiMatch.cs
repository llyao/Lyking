using System.Collections.Generic;

namespace Lyking.Reversi.Core
{
    /// <summary>
    /// 黑白棋对局：包装核心棋盘，区分玩家（黑先手）与 AI（白），
    /// 维护跨局比分累计与封顶裁剪的事件日志（镜像本局棋盘事件并补注局数）；
    /// 重开只清空棋盘，比分保留。
    /// </summary>
    public sealed class ReversiMatch
    {
        private const int MaxEventLogs = 128;

        private readonly List<ReversiEventLog> _events = new List<ReversiEventLog>();

        public ReversiMatch() : this(new ReversiBoard()) { }

        /// <summary>以给定棋盘构造对局（复盘与测试用），局数从 1 起。</summary>
        public ReversiMatch(ReversiBoard board)
        {
            Board = board;
            Round = 1;
        }

        public ReversiBoard Board { get; private set; }

        /// <summary>当前局数（从 1 起），每次重开加一。</summary>
        public int Round { get; private set; }

        /// <summary>玩家（黑）累计胜局数。</summary>
        public int PlayerWins { get; private set; }

        /// <summary>AI（白）累计胜局数。</summary>
        public int AiWins { get; private set; }

        /// <summary>累计平局数。</summary>
        public int Draws { get; private set; }

        /// <summary>是否轮到玩家（黑）落子。</summary>
        public bool IsPlayerTurn => !Board.IsFinished && Board.CurrentTurn == Cell.Black;

        /// <summary>跨局累计的对局事件日志（封顶裁剪）。</summary>
        public IReadOnlyList<ReversiEventLog> Events => _events;

        /// <summary>玩家在指定格子落子；非玩家回合或非法落点被拒绝，返回是否成功。</summary>
        public bool PlayerMove(int row, int col)
        {
            int boardEventCount = Board.Events.Count;
            if (!IsPlayerTurn)
            {
                Log(new ReversiEventLog(Round, ReversiEventType.IllegalRejected, Cell.Black, Board.MoveCount, row, col, 0));
                return false;
            }

            if (!Board.Play(row, col))
            {
                MirrorBoardEvents(boardEventCount);
                return false;
            }

            MirrorBoardEvents(boardEventCount);
            SettleRound();
            return true;
        }

        /// <summary>AI 用加权 minimax 最优落点应答；非 AI 回合返回 false。</summary>
        public bool AiMove()
        {
            if (Board.IsFinished || Board.CurrentTurn != Cell.White) return false;

            int index = ReversiAi.BestMove(Board, Cell.White);
            if (index < 0) return false;

            int boardEventCount = Board.Events.Count;
            if (!Board.Play(index / ReversiRules.Size, index % ReversiRules.Size)) return false;

            MirrorBoardEvents(boardEventCount);
            SettleRound();
            return true;
        }

        /// <summary>重开一局：棋盘清空回到标准开局，局数加一，比分累计与事件日志保留。</summary>
        public void Restart()
        {
            Board = new ReversiBoard();
            Round++;
            Log(new ReversiEventLog(Round, ReversiEventType.Restarted, Cell.None, 0, -1, -1, 0));
        }

        /// <summary>把本局新增的棋盘事件镜像进对局日志并补注局数。</summary>
        private void MirrorBoardEvents(int previousCount)
        {
            var boardEvents = Board.Events;
            for (int i = previousCount; i < boardEvents.Count; i++) _events.Add(boardEvents[i].WithRound(Round));
            TrimEvents();
        }

        /// <summary>本局分出胜负或平局时更新比分并写入终局事件。</summary>
        private void SettleRound()
        {
            if (!Board.IsFinished) return;

            switch (Board.Outcome)
            {
                case ReversiOutcome.BlackWins:
                    PlayerWins++;
                    Log(new ReversiEventLog(Round, ReversiEventType.PlayerWon, Cell.Black, Board.MoveCount, -1, -1, 0));
                    break;
                case ReversiOutcome.WhiteWins:
                    AiWins++;
                    Log(new ReversiEventLog(Round, ReversiEventType.AiWon, Cell.White, Board.MoveCount, -1, -1, 0));
                    break;
                case ReversiOutcome.Draw:
                    Draws++;
                    Log(new ReversiEventLog(Round, ReversiEventType.Drawn, Cell.None, Board.MoveCount, -1, -1, 0));
                    break;
            }
        }

        private void Log(ReversiEventLog entry)
        {
            _events.Add(entry);
            TrimEvents();
        }

        private void TrimEvents()
        {
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
