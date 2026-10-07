using System.Collections.Generic;

namespace Lyking.Reversi.Core
{
    /// <summary>一个合法落步：落点坐标与将被翻转的对方格子索引集合。</summary>
    public readonly struct ReversiMove
    {
        public ReversiMove(int row, int col, IReadOnlyList<int> flipped)
        {
            Row = row;
            Col = col;
            Flipped = flipped;
        }

        /// <summary>落点行（从 0 起，0 为最上行）。</summary>
        public int Row { get; }

        /// <summary>落点列（从 0 起，0 为最左列）。</summary>
        public int Col { get; }

        /// <summary>将被翻转的格子索引集合（<see cref="ReversiRules.FlatIndex"/> 行优先）。</summary>
        public IReadOnlyList<int> Flipped { get; }
    }
}
