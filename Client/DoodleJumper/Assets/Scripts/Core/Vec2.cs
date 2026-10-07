using System;

namespace Lyking.DoodleJumper.Core
{
    /// <summary>场地平面坐标：X 向右、Y 向上，用于玩家与平台的连续位置。</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public Vec2 WithX(float x) => new Vec2(x, Y);

        public Vec2 WithY(float y) => new Vec2(X, y);

        public static Vec2 operator +(Vec2 left, Vec2 right) => new Vec2(left.X + right.X, left.Y + right.Y);

        public static Vec2 operator -(Vec2 left, Vec2 right) => new Vec2(left.X - right.X, left.Y - right.Y);

        public static bool operator ==(Vec2 left, Vec2 right) => left.Equals(right);

        public static bool operator !=(Vec2 left, Vec2 right) => !left.Equals(right);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }
}
