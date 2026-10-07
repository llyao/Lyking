using System;

namespace Lyking.Snake.Core
{
    /// <summary>蛇的四个行进方向。</summary>
    public enum SnakeDir
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何与语义工具：坐标增量、反向判定。</summary>
    public static class SnakeDirExtensions
    {
        public static GridPos ToDelta(this SnakeDir dir) => dir switch
        {
            SnakeDir.Up => new GridPos(0, 1),
            SnakeDir.Down => new GridPos(0, -1),
            SnakeDir.Left => new GridPos(-1, 0),
            SnakeDir.Right => new GridPos(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知方向。"),
        };

        public static SnakeDir Opposite(this SnakeDir dir) => dir switch
        {
            SnakeDir.Up => SnakeDir.Down,
            SnakeDir.Down => SnakeDir.Up,
            SnakeDir.Left => SnakeDir.Right,
            SnakeDir.Right => SnakeDir.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知方向。"),
        };
    }
}
