using System.Collections.Generic;

namespace Lyking.Gomoku.Core
{
    /// <summary>
    /// 五子棋核心棋盘：确定性离散回合制状态机，无 Unity 依赖、无随机数。
    /// 玩家黑子先手，双方轮流在空格落子；任一方连成五子即胜，六连及以上的长连同样判胜，
    /// 棋满无胜为平局。越界、已占或终局后的落子请求被直接拒绝，棋盘保持当前形状不变。
    /// </summary>
    public sealed class GomokuBoard
    {
        private readonly Cell[,] _cells = new Cell[GomokuRules.Size, GomokuRules.Size];

        /// <summary>棋盘占用方数组，[行, 列] 索引，行 0 为最上行。</summary>
        public Cell[,] Cells => _cells;

        /// <summary>指定行列的占用方；行 0 为最上行，列 0 为最左列。</summary>
        public Cell this[int row, int col] => _cells[row, col];

        /// <summary>当前该落子的一方。</summary>
        public Cell CurrentTurn { get; private set; } = Cell.Black;

        /// <summary>已落子总数。</summary>
        public int MoveCount { get; private set; }

        public bool IsFinished { get; private set; }

        public GomokuOutcome Outcome { get; private set; } = GomokuOutcome.None;

        /// <summary>最后一手的坐标；尚未落子时为 null。</summary>
        public CellPos? LastMove { get; private set; }

        /// <summary>指定坐标是否为可落子的空格。</summary>
        public bool IsEmptyAt(int row, int col)
        {
            return GomokuRules.IsInside(row, col) && _cells[row, col] == Cell.None;
        }

        /// <summary>棋盘无空格时为真，用于棋满无胜时的平局判定。</summary>
        public bool IsFull() => GomokuRules.IsFull(_cells);

        /// <summary>返回当前棋盘获胜连线的坐标，便于渲染高亮；无五连时为 null。</summary>
        public IReadOnlyList<CellPos>? FindWinningLine() => GomokuRules.FindWinningLine(_cells);

        /// <summary>
        /// 在指定空格落子。越界、已占或对局已结束时拒绝并返回 false；
        /// 成功后轮换行棋方，并在五连（含长连）或棋满时写入终局结果。
        /// </summary>
        public bool Play(int row, int col)
        {
            if (IsFinished) return false;
            if (!IsEmptyAt(row, col)) return false;

            _cells[row, col] = CurrentTurn;
            MoveCount++;
            LastMove = new CellPos(row, col);
            SettleOutcome();
            if (!IsFinished) CurrentTurn = CurrentTurn == Cell.Black ? Cell.White : Cell.Black;
            return true;
        }

        /// <summary>五连（含长连）即分胜负；棋满无胜判为平局。</summary>
        private void SettleOutcome()
        {
            var line = GomokuRules.FindWinningLine(_cells);
            if (line != null)
            {
                Outcome = _cells[line[0].Row, line[0].Col] == Cell.Black
                    ? GomokuOutcome.BlackWins
                    : GomokuOutcome.WhiteWins;
                IsFinished = true;
            }
            else if (MoveCount == GomokuRules.CellCount)
            {
                Outcome = GomokuOutcome.Draw;
                IsFinished = true;
            }
        }
    }
}
