using System;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>地下城地面平面坐标：X 为横向，Y 为纵深（屏幕上的上下方向）。</summary>
    public readonly struct Vec2
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

        public float DistanceTo(Vec2 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }
}
