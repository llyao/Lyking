namespace Lyking.Sudoku.Core
{
    /// <summary>数独过程事件的类型，供表现层日志与测试断言。</summary>
    public enum SudokuEventType
    {
        /// <summary>生成新谜题（含构造时的首局）。</summary>
        NewPuzzle,

        /// <summary>在空格填入一个数字（覆盖旧玩家数同样计一次填数）。</summary>
        Filled,

        /// <summary>填入后该格与所在行、列或宫中的另一格重复。</summary>
        Conflict,

        /// <summary>清除一个玩家填数。</summary>
        Cleared,

        /// <summary>提示自动在空格填入正确答案，永不产生冲突。</summary>
        Hint,

        /// <summary>全盘填满且与解一致，通关。</summary>
        Completed,

        /// <summary>清除全部玩家填数回到谜题开局。</summary>
        Reset,
    }

    /// <summary>一条带步序号、格位与数值的事件记录。</summary>
    public readonly struct SudokuEventLog
    {
        public SudokuEventLog(int moveNumber, SudokuEventType type, int row = -1, int column = -1, int value = 0)
        {
            MoveNumber = moveNumber;
            Type = type;
            Row = row;
            Column = column;
            Value = value;
        }

        /// <summary>事件发生时已完成的有效操作步数。</summary>
        public int MoveNumber { get; }

        public SudokuEventType Type { get; }

        /// <summary>关联格位的行，无关联格位的事件为 -1。</summary>
        public int Row { get; }

        /// <summary>关联格位的列，无关联格位的事件为 -1。</summary>
        public int Column { get; }

        /// <summary>关联的数字：填数与提示为填入值，清除为被移除的值，其余事件为 0。</summary>
        public int Value { get; }

        public override string ToString()
        {
            if (Row < 0) return $"步数 {MoveNumber}: {Type}";
            return Value > 0
                ? $"步数 {MoveNumber}: {Type} 格 ({Row},{Column}) 值 {Value}"
                : $"步数 {MoveNumber}: {Type} 格 ({Row},{Column})";
        }
    }
}
