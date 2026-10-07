using System;

namespace Lyking.SpaceShooter.Core
{
    /// <summary>射击场平面坐标：X 向右、Y 向上，左下角为原点，支持加减、数乘、相等比较与距离。</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Vec2 operator -(Vec2 left, Vec2 right) => new(left.X - right.X, left.Y - right.Y);

        public static Vec2 operator *(Vec2 left, float factor) => new(left.X * factor, left.Y * factor);

        public static bool operator ==(Vec2 left, Vec2 right) => left.Equals(right);

        public static bool operator !=(Vec2 left, Vec2 right) => !left.Equals(right);

        public Vec2 WithX(float x) => new Vec2(x, Y);

        public Vec2 WithY(float y) => new Vec2(X, y);

        public float DistanceTo(Vec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }
}
