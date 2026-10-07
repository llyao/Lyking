using System;

namespace Lyking.Frogger.Core
{
    /// <summary>青蛙的四个跳跃方向。</summary>
    public enum FroggerDir
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>跳跃方向的几何工具：网格增量。</summary>
    public static class FroggerDirExtensions
    {
        public static GridPos ToDelta(this FroggerDir dir) => dir switch
        {
            FroggerDir.Up => new GridPos(0, 1),
            FroggerDir.Down => new GridPos(0, -1),
            FroggerDir.Left => new GridPos(-1, 0),
            FroggerDir.Right => new GridPos(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知方向。"),
        };
    }
}
