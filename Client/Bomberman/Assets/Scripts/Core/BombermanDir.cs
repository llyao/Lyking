using System;

namespace Lyking.Bomberman.Core
{
    /// <summary>移动方向；None 仅表示本帧没有移动输入，不作为几何方向使用。</summary>
    public enum BombermanDir
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何工具：坐标增量与相反方向。</summary>
    public static class BombermanDirExtensions
    {
        public static GridPos ToDelta(this BombermanDir dir) => dir switch
        {
            BombermanDir.Up => new GridPos(0, 1),
            BombermanDir.Down => new GridPos(0, -1),
            BombermanDir.Left => new GridPos(-1, 0),
            BombermanDir.Right => new GridPos(1, 0),
            _ => new GridPos(0, 0),
        };

        public static BombermanDir Opposite(this BombermanDir dir) => dir switch
        {
            BombermanDir.Up => BombermanDir.Down,
            BombermanDir.Down => BombermanDir.Up,
            BombermanDir.Left => BombermanDir.Right,
            BombermanDir.Right => BombermanDir.Left,
            _ => BombermanDir.None,
        };
    }
}
