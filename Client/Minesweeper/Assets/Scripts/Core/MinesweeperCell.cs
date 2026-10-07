#nullable enable
namespace Lyking.Minesweeper.Core
{
    /// <summary>单个格子的可见状态。</summary>
    public enum CellState
    {
        Hidden,
        Revealed,
        Flagged,
    }

    /// <summary>格子只读快照。地雷与邻雷数在布雷前均为默认值。</summary>
    public readonly struct MinesweeperCell
    {
        public MinesweeperCell(CellState state, bool hasMine, int adjacentMines)
        {
            State = state;
            HasMine = hasMine;
            AdjacentMines = adjacentMines;
        }

        public CellState State { get; }

        public bool HasMine { get; }

        public int AdjacentMines { get; }
    }
}
