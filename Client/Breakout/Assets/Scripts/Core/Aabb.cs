using System;

namespace Lyking.Breakout.Core
{
    /// <summary>轴对齐包围盒：挡板与砖块的碰撞外形，构造时归一化角点，只做最近点与包含判断。</summary>
    public readonly struct Aabb
    {
        public Aabb(Vec2 cornerA, Vec2 cornerB)
        {
            Min = new Vec2(MathF.Min(cornerA.X, cornerB.X), MathF.Min(cornerA.Y, cornerB.Y));
            Max = new Vec2(MathF.Max(cornerA.X, cornerB.X), MathF.Max(cornerA.Y, cornerB.Y));
        }

        /// <summary>以中心点与尺寸构造包围盒。</summary>
        public static Aabb FromCenterAndSize(Vec2 center, Vec2 size)
        {
            var half = size * 0.5f;
            return new Aabb(center - half, center + half);
        }

        public Vec2 Min { get; }

        public Vec2 Max { get; }

        /// <summary>盒内离指定点最近的点；点在盒内时返回其自身，是圆与盒碰撞判定的基础。</summary>
        public Vec2 ClosestPoint(Vec2 point) =>
            new(Math.Clamp(point.X, Min.X, Max.X), Math.Clamp(point.Y, Min.Y, Max.Y));

        public bool Contains(Vec2 point) =>
            point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;

        public override string ToString() => $"[({Min.X:0.00}, {Min.Y:0.00}) ~ ({Max.X:0.00}, {Max.Y:0.00})]";
    }
}
