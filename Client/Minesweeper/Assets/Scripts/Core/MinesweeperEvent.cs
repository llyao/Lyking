#nullable enable
namespace Lyking.Minesweeper.Core
{
    /// <summary>对局状态：待首击、进行中、胜利、失败。</summary>
    public enum GameState
    {
        Ready,
        Playing,
        Won,
        Lost,
    }

    /// <summary>翻开操作结果。</summary>
    public enum RevealOutcome
    {
        Revealed,
        HitMine,
        AlreadyRevealed,
        BlockedByFlag,
        InvalidCell,
        GameOver,
    }

    /// <summary>插旗操作结果。</summary>
    public enum FlagOutcome
    {
        Flagged,
        Unflagged,
        InvalidCell,
        Rejected,
        GameOver,
    }

    /// <summary>棋盘事件类型。坐标不适用时事件坐标为 -1。</summary>
    public enum MinesweeperEventType
    {
        Revealed,
        FloodRevealed,
        Flagged,
        Unflagged,
        MineHit,
        Won,
        Lost,
        NewGame,
    }

    /// <summary>棋盘事件快照。<see cref="Count"/> 用于洪水展开格数等数量信息。</summary>
    public readonly struct MinesweeperEvent
    {
        public MinesweeperEvent(MinesweeperEventType type, int x = -1, int y = -1, int count = 0)
        {
            Type = type;
            X = x;
            Y = y;
            Count = count;
        }

        public MinesweeperEventType Type { get; }

        public int X { get; }

        public int Y { get; }

        public int Count { get; }
    }
}
