namespace Lyking.Breakout.Core
{
    /// <summary>砖块状态：行列位置、剩余耐久、击破分值与碰撞包围盒；不可变，受击后生成新状态替换。</summary>
    public readonly struct BreakoutBrick
    {
        public BreakoutBrick(int column, int row, int durability, int score, Aabb bounds)
        {
            Column = column;
            Row = row;
            Durability = durability;
            Score = score;
            Bounds = bounds;
        }

        public int Column { get; }

        public int Row { get; }

        /// <summary>剩余耐久：不大于 0 表示已被击破。</summary>
        public int Durability { get; }

        public int Score { get; }

        public Aabb Bounds { get; }

        public bool IsAlive => Durability > 0;

        public BreakoutBrick WithDurability(int durability) =>
            new BreakoutBrick(Column, Row, durability, Score, Bounds);

        public override string ToString() => $"({Column},{Row}) 耐久 {Durability} 分值 {Score}";
    }
}
