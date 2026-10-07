namespace Lyking.Game2048.Core
{
    /// <summary>一次滑动移动的结果：棋盘是否变化、本次合并得分与合并次数。</summary>
    public readonly struct MoveResult
    {
        public MoveResult(bool changed, int gainedScore, int mergedCount)
        {
            Changed = changed;
            GainedScore = gainedScore;
            MergedCount = mergedCount;
        }

        /// <summary>棋盘是否发生变化；无变化时不应生成新块。</summary>
        public bool Changed { get; }

        /// <summary>本次合并累加的得分。</summary>
        public int GainedScore { get; }

        /// <summary>本次发生的合并次数。</summary>
        public int MergedCount { get; }
    }
}
