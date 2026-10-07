using System.Collections.Generic;

namespace Lyking.ConnectFour.Core
{
    /// <summary>
    /// 四子连珠核心棋盘：确定性离散回合制状态机，无 Unity 依赖、无随机数。
    /// 玩家红子先手，双方轮流往 7 列中落子，子受重力堆叠到该列最低空位；
    /// 任一方四连即胜，42 格全满无胜为平局。
    /// 越界列、满列或终局后的落子请求被直接拒绝，棋盘保持当前形状不变。
    /// </summary>
    public sealed class ConnectFourBoard
    {
        private readonly Cell[,] _cells = new Cell[ConnectFourRules.Columns, ConnectFourRules.Rows];

        public ConnectFourBoard()
        {
            CurrentTurn = Cell.Red;
        }

        /// <summary>棋盘占用方数组，[列, 行] 索引，行 0 为最底层。</summary>
        public Cell[,] Cells => _cells;

        /// <summary>指定列行的占用方；行 0 为最底层。</summary>
        public Cell this[int col, int row] => _cells[col, row];

        /// <summary>当前该落子的一方。</summary>
        public Cell CurrentTurn { get; private set; }

        /// <summary>已落子总数。</summary>
        public int MoveCount { get; private set; }

        public bool IsFinished { get; private set; }

        public ConnectFourOutcome Outcome { get; private set; } = ConnectFourOutcome.None;

        /// <summary>获胜四连的坐标；对局未分出胜负时为 null。</summary>
        public IReadOnlyList<CellPos>? WinningLine { get; private set; }

        /// <summary>指定列当前是否可落子（未越界、未满列且对局未结束）。</summary>
        public bool CanDrop(int col)
        {
            return !IsFinished && ConnectFourRules.LowestEmptyRow(_cells, col) >= 0;
        }

        /// <summary>
        /// 往指定列落子，子落在该列最低空位。越界、满列或对局已结束时拒绝并返回 false；
        /// 成功后轮换行棋方，并在四连或棋满时写入终局结果。
        /// </summary>
        public bool Drop(int col)
        {
            if (IsFinished) return false;
            int row = ConnectFourRules.LowestEmptyRow(_cells, col);
            if (row < 0) return false;

            _cells[col, row] = CurrentTurn;
            MoveCount++;
            SettleOutcome();
            if (!IsFinished) CurrentTurn = CurrentTurn == Cell.Red ? Cell.Yellow : Cell.Red;
            return true;
        }

        /// <summary>四连即分胜负；棋满无胜判为平局。</summary>
        private void SettleOutcome()
        {
            var line = ConnectFourRules.FindWinningLine(_cells);
            if (line != null)
            {
                WinningLine = line;
                Outcome = _cells[line[0].Col, line[0].Row] == Cell.Red
                    ? ConnectFourOutcome.RedWins
                    : ConnectFourOutcome.YellowWins;
                IsFinished = true;
            }
            else if (MoveCount == ConnectFourRules.CellCount)
            {
                Outcome = ConnectFourOutcome.Draw;
                IsFinished = true;
            }
        }
    }
}
