namespace Lyking.BattleCity.Core
{
    /// <summary>坦克朝向与移动方向；None 仅表示原地不动或与方向无关，不作为射击朝向。</summary>
    public enum TankDir
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何工具：格坐标增量与连续单位向量。</summary>
    public static class TankDirExtensions
    {
        public static GridPos ToDelta(this TankDir dir) => dir switch
        {
            TankDir.Up => new GridPos(0, 1),
            TankDir.Down => new GridPos(0, -1),
            TankDir.Left => new GridPos(-1, 0),
            TankDir.Right => new GridPos(1, 0),
            _ => new GridPos(0, 0),
        };

        public static Vec2 ToVector(this TankDir dir) => dir switch
        {
            TankDir.Up => new Vec2(0f, 1f),
            TankDir.Down => new Vec2(0f, -1f),
            TankDir.Left => new Vec2(-1f, 0f),
            TankDir.Right => new Vec2(1f, 0f),
            _ => new Vec2(0f, 0f),
        };

        public static TankDir Opposite(this TankDir dir) => dir switch
        {
            TankDir.Up => TankDir.Down,
            TankDir.Down => TankDir.Up,
            TankDir.Left => TankDir.Right,
            TankDir.Right => TankDir.Left,
            _ => TankDir.None,
        };
    }
}
