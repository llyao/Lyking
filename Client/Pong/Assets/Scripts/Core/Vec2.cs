using System;

namespace Lyking.Pong.Core
{
    /// <summary>二维向量：X 向右、Y 向上，用于场地内的位置与速度运算。</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public static Vec2 Zero => default;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public float LengthSquared => X * X + Y * Y;

        public float Length => MathF.Sqrt(LengthSquared);

        public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Vec2 operator -(Vec2 left, Vec2 right) => new(left.X - right.X, left.Y - right.Y);

        public static Vec2 operator *(Vec2 value, float scale) => new(value.X * scale, value.Y * scale);

        public static Vec2 operator *(float scale, Vec2 value) => value * scale;

        public Vec2 WithX(float x) => new Vec2(x, Y);

        public Vec2 WithY(float y) => new Vec2(X, y);

        /// <summary>归一化为单位向量；长度接近零时返回 <see cref="Zero"/>，避免除零。</summary>
        public Vec2 Normalized() => LengthSquared < 1e-8f ? Zero : this * (1f / Length);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }
}
