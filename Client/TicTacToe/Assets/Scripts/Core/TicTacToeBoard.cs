using System.Collections.Generic;

namespace Lyking.TicTacToe.Core
{
    /// <summary>
    /// 井字棋核心棋盘：确定性离散回合制状态机，无 Unity 依赖、无随机数。
    /// X 先手，双方轮流在空格落子；任一方三连即胜，棋满无胜为平局。
    /// 越界、已占或终局后的落子请求被直接拒绝，棋盘保持当前形状不变。
    /// </summary>
    public sealed class TicTacToeBoard
    {
        private readonly Cell[] _cells = new Cell[TicTacToeRules.CellCount];

        public TicTacToeBoard()
        {
            CurrentTurn = Cell.X;
        }

        /// <summary>9 个格子的当前占用方，索引为行优先布局（0 为左上格）。</summary>
        public IReadOnlyList<Cell> Cells => _cells;

        /// <summary>当前该落子的一方。</summary>
        public Cell CurrentTurn { get; private set; }

        /// <summary>已落子总数。</summary>
        public int MoveCount { get; private set; }

        public bool IsFinished { get; private set; }

        public TicTacToeOutcome Outcome { get; private set; } = TicTacToeOutcome.None;

        /// <summary>指定索引是否为可落子的空格。</summary>
        public bool IsEmptyAt(int index) => index >= 0 && index < TicTacToeRules.CellCount && _cells[index] == Cell.None;

        /// <summary>
        /// 在指定空格落子。越界、已占或对局已结束时拒绝并返回 false；
        /// 成功后轮换先手方，并在三连或棋满时写入终局结果。
        /// </summary>
        public bool Play(int index)
        {
            if (IsFinished) return false;
            if (index < 0 || index >= TicTacToeRules.CellCount) return false;
            if (_cells[index] != Cell.None) return false;

            _cells[index] = CurrentTurn;
            MoveCount++;
            SettleOutcome();
            if (!IsFinished) CurrentTurn = CurrentTurn == Cell.X ? Cell.O : Cell.X;
            return true;
        }

        /// <summary>三连即分胜负；棋满无胜判为平局。</summary>
        private void SettleOutcome()
        {
            var winner = TicTacToeRules.FindWinner(_cells);
            if (winner == Cell.X)
            {
                Outcome = TicTacToeOutcome.XWins;
                IsFinished = true;
            }
            else if (winner == Cell.O)
            {
                Outcome = TicTacToeOutcome.OWins;
                IsFinished = true;
            }
            else if (MoveCount == TicTacToeRules.CellCount)
            {
                Outcome = TicTacToeOutcome.Draw;
                IsFinished = true;
            }
        }
    }
}
