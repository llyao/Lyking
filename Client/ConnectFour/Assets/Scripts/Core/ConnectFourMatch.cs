using System.Collections.Generic;

namespace Lyking.ConnectFour.Core
{
    /// <summary>
    /// 四子连珠对局：包装核心棋盘，区分玩家（红先手）与 AI（黄），
    /// 维护跨局比分累计与封顶裁剪的事件日志；重开只清空棋盘与手数，比分保留。
    /// 越界列、满列、终局或非玩家回合的落子被拒绝并记入事件日志。
    /// </summary>
    public sealed class ConnectFourMatch
    {
        private const int MaxEventLogs = 128;

        private readonly List<ConnectFourEventLog> _events = new List<ConnectFourEventLog>();

        public ConnectFourMatch()
        {
            Board = new ConnectFourBoard();
            Round = 1;
        }

        public ConnectFourBoard Board { get; private set; }

        /// <summary>当前局数（从 1 起），每次重开加一。</summary>
        public int Round { get; private set; }

        /// <summary>玩家（红）累计胜局数。</summary>
        public int PlayerWins { get; private set; }

        /// <summary>AI（黄）累计胜局数。</summary>
        public int AiWins { get; private set; }

        /// <summary>累计平局数。</summary>
        public int Draws { get; private set; }

        /// <summary>是否轮到玩家（红）落子。</summary>
        public bool IsPlayerTurn => !Board.IsFinished && Board.CurrentTurn == Cell.Red;

        public IReadOnlyList<ConnectFourEventLog> Events => _events;

        /// <summary>玩家往指定列落子；非玩家回合或非法落点被拒绝，返回是否成功。</summary>
        public bool PlayerDrop(int col)
        {
            if (!IsPlayerTurn || !Board.Drop(col))
            {
                Log(ConnectFourEventType.MoveRejected, col);
                return false;
            }

            Log(ConnectFourEventType.PlayerDropped, col);
            SettleRound();
            return true;
        }

        /// <summary>AI 用 minimax 最优落点应答；非 AI 回合返回 false。</summary>
        public bool AiDrop()
        {
            if (Board.IsFinished || Board.CurrentTurn != Cell.Yellow) return false;

            int col = ConnectFourAi.BestColumn(Board.Cells, Cell.Yellow);
            if (col < 0 || !Board.Drop(col)) return false;

            Log(ConnectFourEventType.AiDropped, col);
            SettleRound();
            return true;
        }

        /// <summary>重开一局：棋盘清空、局数加一，比分累计与事件日志保留。</summary>
        public void Restart()
        {
            Board = new ConnectFourBoard();
            Round++;
            Log(ConnectFourEventType.Restarted, -1);
        }

        /// <summary>本局分出胜负或平局时更新比分并写入终局事件。</summary>
        private void SettleRound()
        {
            if (!Board.IsFinished) return;

            switch (Board.Outcome)
            {
                case ConnectFourOutcome.RedWins:
                    PlayerWins++;
                    Log(ConnectFourEventType.PlayerWon, -1);
                    break;
                case ConnectFourOutcome.YellowWins:
                    AiWins++;
                    Log(ConnectFourEventType.AiWon, -1);
                    break;
                case ConnectFourOutcome.Draw:
                    Draws++;
                    Log(ConnectFourEventType.Drawn, -1);
                    break;
            }
        }

        private void Log(ConnectFourEventType type, int column)
        {
            _events.Add(new ConnectFourEventLog(Round, Board.MoveCount, type, column));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
