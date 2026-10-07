using System;

namespace Lyking.MazeChase.Core
{
    /// <summary>迷宫行进方向：<see cref="None"/> 表示停在某个格点等待决策。</summary>
    public enum MazeDir
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何与语义工具：坐标增量、反向判定。</summary>
    public static class MazeDirExtensions
    {
        public static GridPos ToDelta(this MazeDir dir) => dir switch
        {
            MazeDir.Up => new GridPos(0, 1),
            MazeDir.Down => new GridPos(0, -1),
            MazeDir.Left => new GridPos(-1, 0),
            MazeDir.Right => new GridPos(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知方向。"),
        };

        public static MazeDir Opposite(this MazeDir dir) => dir switch
        {
            MazeDir.Up => MazeDir.Down,
            MazeDir.Down => MazeDir.Up,
            MazeDir.Left => MazeDir.Right,
            MazeDir.Right => MazeDir.Left,
            MazeDir.None => MazeDir.None,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知方向。"),
        };
    }
}
