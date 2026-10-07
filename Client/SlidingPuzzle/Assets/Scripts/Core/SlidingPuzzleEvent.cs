namespace Lyking.SlidingPuzzle.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum SlidingPuzzleEventType
    {
        /// <summary>重置回当前尺寸的已解状态（含构造后的初始局面）。</summary>
        Reset,

        /// <summary>从已解状态反向随机行走完成打乱。</summary>
        Shuffled,

        /// <summary>一枚滑块滑入空位。</summary>
        Slid,

        /// <summary>滑动被拒绝：方向无相邻滑块、与空位不共线或棋局已还原。</summary>
        SlideRejected,

        /// <summary>撤销上一次滑动。</summary>
        Undone,

        /// <summary>滑块全部归位且空位回到右下角，还原成功。</summary>
        Won,
    }

    /// <summary>一条带步数、方向与滑块编号的事件记录。</summary>
    public readonly struct SlidingPuzzleEventLog
    {
        public SlidingPuzzleEventLog(int steps, SlidingPuzzleEventType type, SlideDir direction, int tile)
        {
            Steps = steps;
            Type = type;
            Direction = direction;
            Tile = tile;
        }

        /// <summary>事件发生时已用的步数。</summary>
        public int Steps { get; }

        public SlidingPuzzleEventType Type { get; }

        /// <summary>触发事件的方向；与方向无关的事件为 <see cref="SlideDir.None"/>。</summary>
        public SlideDir Direction { get; }

        /// <summary>关联滑块的编号（从 1 开始）；无关联滑块时为 -1。</summary>
        public int Tile { get; }

        public override string ToString() => Direction == SlideDir.None
            ? Tile >= 0 ? $"步数 {Steps}: {Type} 块 {Tile}" : $"步数 {Steps}: {Type}"
            : Tile >= 0 ? $"步数 {Steps}: {Type}({Direction}) 块 {Tile}" : $"步数 {Steps}: {Type}({Direction})";
    }
}
