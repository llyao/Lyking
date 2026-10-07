using System;

namespace Lyking.Bomberman.Core
{
    /// <summary>棋盘格坐标：X 向右、Y 向上，左下角为原点，只做加法与相等比较。</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public static GridPos operator +(GridPos left, GridPos delta) => new(left.X + delta.X, left.Y + delta.Y);

        public static bool operator ==(GridPos left, GridPos right) => left.Equals(right);

        public static bool operator !=(GridPos left, GridPos right) => !left.Equals(right);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X},{Y})";
    }
}
