namespace Lyking.Bomberman.Core
{
    /// <summary>一帧玩家输入：网格移动方向与放置炸弹的按下沿，纯数据结构、无引擎依赖。</summary>
    public readonly struct BombInput
    {
        public BombInput(BombermanDir moveDir, bool placeBomb)
        {
            MoveDir = moveDir;
            PlaceBomb = placeBomb;
        }

        /// <summary>本帧移动方向（按下沿），<see cref="BombermanDir.None"/> 表示不移动。</summary>
        public BombermanDir MoveDir { get; }

        /// <summary>本帧放置炸弹键是否刚按下（按下沿）。</summary>
        public bool PlaceBomb { get; }
    }

    /// <summary>已放置的炸弹：所在格与剩余引信时间。</summary>
    public readonly struct BombermanBomb
    {
        public BombermanBomb(GridPos position, float fuseRemaining)
        {
            Position = position;
            FuseRemaining = fuseRemaining;
        }

        public GridPos Position { get; }

        /// <summary>距离爆炸的剩余秒数。</summary>
        public float FuseRemaining { get; }
    }

    /// <summary>爆炸产生的火焰格：所在格与剩余燃烧时间。</summary>
    public readonly struct BombermanFlame
    {
        public BombermanFlame(GridPos position, float remaining)
        {
            Position = position;
            Remaining = remaining;
        }

        public GridPos Position { get; }

        /// <summary>距离熄灭的剩余秒数。</summary>
        public float Remaining { get; }
    }

    /// <summary>敌人的运行时状态：所在格、当前朝向与本格行进进度（0..1）。</summary>
    public readonly struct BombermanEnemy
    {
        public BombermanEnemy(GridPos position, BombermanDir direction, float progress)
        {
            Position = position;
            Direction = direction;
            Progress = progress;
        }

        public GridPos Position { get; }

        /// <summary>当前朝向；被完全围死时为 <see cref="BombermanDir.None"/>。</summary>
        public BombermanDir Direction { get; }

        /// <summary>向当前朝向的下一格行进的进度（0..1）。</summary>
        public float Progress { get; }
    }
}
