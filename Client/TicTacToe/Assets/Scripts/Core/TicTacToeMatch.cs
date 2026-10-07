using System.Collections.Generic;

namespace Lyking.TicTacToe.Core
{
    /// <summary>
    /// 井字棋对局：包装核心棋盘，区分玩家（X 先手）与 AI（O），
    /// 维护跨局比分累计与封顶裁剪的事件日志；重开只清空棋盘与手数，比分保留。
    /// </summary>
    public sealed class TicTacToeMatch
    {
        private const int MaxEventLogs = 128;

        private readonly List<TicTacToeEventLog> _events = new List<TicTacToeEventLog>();

        public TicTacToeMatch()
        {
            Board = new TicTacToeBoard();
            Round = 1;
        }

        public TicTacToeBoard Board { get; private set; }

        /// <summary>当前局数（从 1 起），每次重开加一。</summary>
        public int Round { get; private set; }

        /// <summary>玩家（X）累计胜局数。</summary>
        public int PlayerWins { get; private set; }

        /// <summary>AI（O）累计胜局数。</summary>
        public int AiWins { get; private set; }

        /// <summary>累计平局数。</summary>
        public int Draws { get; private set; }

        /// <summary>是否轮到玩家（X）落子。</summary>
        public bool IsPlayerTurn => !Board.IsFinished && Board.CurrentTurn == Cell.X;

        public IReadOnlyList<TicTacToeEventLog> Events => _events;

        /// <summary>玩家在指定空格落子；非玩家回合或非法落点被拒绝，返回是否成功。</summary>
        public bool PlayerMove(int index)
        {
            if (!IsPlayerTurn) return false;
            if (!Board.Play(index)) return false;

            Log(TicTacToeEventType.PlayerMoved, index);
            SettleRound();
            return true;
        }

        /// <summary>AI 用 minimax 最优落点应答；非 AI 回合返回 false。</summary>
        public bool AiMove()
        {
            if (Board.IsFinished || Board.CurrentTurn != Cell.O) return false;

            int index = TicTacToeAi.BestMove(Board.Cells, Cell.O);
            if (index < 0 || !Board.Play(index)) return false;

            Log(TicTacToeEventType.AiMoved, index);
            SettleRound();
            return true;
        }

        /// <summary>重开一局：棋盘清空、局数加一，比分累计与事件日志保留。</summary>
        public void Restart()
        {
            Board = new TicTacToeBoard();
            Round++;
            Log(TicTacToeEventType.Restarted, -1);
        }

        /// <summary>本局分出胜负或平局时更新比分并写入终局事件。</summary>
        private void SettleRound()
        {
            if (!Board.IsFinished) return;

            switch (Board.Outcome)
            {
                case TicTacToeOutcome.XWins:
                    PlayerWins++;
                    Log(TicTacToeEventType.PlayerWon, -1);
                    break;
                case TicTacToeOutcome.OWins:
                    AiWins++;
                    Log(TicTacToeEventType.AiWon, -1);
                    break;
                case TicTacToeOutcome.Draw:
                    Draws++;
                    Log(TicTacToeEventType.Drawn, -1);
                    break;
            }
        }

        private void Log(TicTacToeEventType type, int cellIndex)
        {
            _events.Add(new TicTacToeEventLog(Round, Board.MoveCount, type, cellIndex));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
