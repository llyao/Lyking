using System.Collections.Generic;

namespace Lyking.Gomoku.Core
{
    /// <summary>
    /// 五子棋对局：包装核心棋盘，区分玩家（黑先手）与 AI（白），
    /// 维护跨局比分累计与封顶裁剪的事件日志；重开只清空棋盘与手数，比分保留。
    /// 越界、已占、终局或非玩家回合的落子被拒绝并记入事件日志。
    /// </summary>
    public sealed class GomokuMatch
    {
        private const int MaxEventLogs = 128;

        private readonly List<GomokuEventLog> _events = new List<GomokuEventLog>();

        public GomokuMatch()
        {
            Board = new GomokuBoard();
            Round = 1;
        }

        public GomokuBoard Board { get; private set; }

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

        public IReadOnlyList<GomokuEventLog> Events => _events;

        /// <summary>玩家在指定空格落子；非玩家回合或非法落点被拒绝并记入事件日志，返回是否成功。</summary>
        public bool PlayerMove(int row, int col)
        {
            if (!IsPlayerTurn || !Board.Play(row, col))
            {
                Log(GomokuEventType.MoveRejected, row, col);
                return false;
            }

            Log(GomokuEventType.PlayerMoved, row, col);
            SettleRound();
            return true;
        }

        /// <summary>AI 用启发式评分最优落点应答；非 AI 回合返回 false。</summary>
        public bool AiMove()
        {
            if (Board.IsFinished || Board.CurrentTurn != Cell.White) return false;

            var move = GomokuAi.BestMove(Board.Cells, Cell.White);
            if (move.Row < 0 || !Board.Play(move.Row, move.Col)) return false;

            Log(GomokuEventType.AiMoved, move.Row, move.Col);
            SettleRound();
            return true;
        }

        /// <summary>重开一局：棋盘清空、局数加一，比分累计与事件日志保留。</summary>
        public void Restart()
        {
            Board = new GomokuBoard();
            Round++;
            Log(GomokuEventType.Restarted, -1, -1);
        }

        /// <summary>本局分出胜负或平局时更新比分并写入终局事件。</summary>
        private void SettleRound()
        {
            if (!Board.IsFinished) return;

            switch (Board.Outcome)
            {
                case GomokuOutcome.BlackWins:
                    PlayerWins++;
                    Log(GomokuEventType.PlayerWon, -1, -1);
                    break;
                case GomokuOutcome.WhiteWins:
                    AiWins++;
                    Log(GomokuEventType.AiWon, -1, -1);
                    break;
                case GomokuOutcome.Draw:
                    Draws++;
                    Log(GomokuEventType.Drawn, -1, -1);
                    break;
            }
        }

        private void Log(GomokuEventType type, int row, int col)
        {
            _events.Add(new GomokuEventLog(Round, Board.MoveCount, type, row, col));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
