namespace Lyking.SlidingPuzzle.Core
{
    /// <summary>
    /// 滑动方向语义（全工程统一）：表示滑块被推入空位的方向，即"玩家把滑块推向该方向"。
    /// <see cref="SlideDir.Left"/> 表示紧邻空位右侧的滑块向左滑入空位（空位等效向右移动一格），
    /// 与键盘 ← 键"把空位右侧的块左移"的含义一致；其余方向同理。
    /// </summary>
    public enum SlideDir
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>方向的几何工具：相反方向，供撤销、打乱防回头与测试回放使用。</summary>
    public static class SlideDirExtensions
    {
        public static SlideDir Opposite(this SlideDir dir) => dir switch
        {
            SlideDir.Up => SlideDir.Down,
            SlideDir.Down => SlideDir.Up,
            SlideDir.Left => SlideDir.Right,
            SlideDir.Right => SlideDir.Left,
            _ => SlideDir.None,
        };
    }
}
