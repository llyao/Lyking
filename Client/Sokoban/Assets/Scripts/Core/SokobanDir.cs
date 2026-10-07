using System;

namespace Lyking.Sokoban.Core
{
    /// <summary>移动方向；None 仅用于与方向无关的事件记录，不作为输入。</summary>
    public enum SokobanDir
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何工具：坐标增量与相反方向。</summary>
    public static class SokobanDirExtensions
    {
        public static GridPos ToDelta(this SokobanDir dir) => dir switch
        {
            SokobanDir.Up => new GridPos(0, 1),
            SokobanDir.Down => new GridPos(0, -1),
            SokobanDir.Left => new GridPos(-1, 0),
            SokobanDir.Right => new GridPos(1, 0),
            _ => new GridPos(0, 0),
        };

        public static SokobanDir Opposite(this SokobanDir dir) => dir switch
        {
            SokobanDir.Up => SokobanDir.Down,
            SokobanDir.Down => SokobanDir.Up,
            SokobanDir.Left => SokobanDir.Right,
            SokobanDir.Right => SokobanDir.Left,
            _ => SokobanDir.None,
        };
    }
}
