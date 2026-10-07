namespace Lyking.WhackAMole.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum WhackAMoleEventType
    {
        /// <summary>首次输入开局，倒计时从零开始。</summary>
        Started,

        /// <summary>一只地鼠冒头。</summary>
        Spawned,

        /// <summary>命中普通地鼠，得分且连击增加。</summary>
        Hit,

        /// <summary>打空（空洞或打错洞），连击清零。</summary>
        Missed,

        /// <summary>命中炸弹地鼠，扣分且连击清零。</summary>
        BombHit,

        /// <summary>地鼠到时未被打击，自动缩回。</summary>
        Retreated,

        /// <summary>倒计时归零。</summary>
        TimeUp,

        /// <summary>对局结束。</summary>
        Finished,
    }

    /// <summary>一条带对局时刻、地洞编号、总分与连击数的事件记录。</summary>
    public readonly struct WhackAMoleEventLog
    {
        public WhackAMoleEventLog(float time, WhackAMoleEventType type, int holeIndex, int score, int combo)
        {
            Time = time;
            Type = type;
            HoleIndex = holeIndex;
            Score = score;
            Combo = combo;
        }

        /// <summary>事件发生时的对局时刻（秒）。</summary>
        public float Time { get; }

        public WhackAMoleEventType Type { get; }

        /// <summary>关联的地洞编号，无关联地洞的事件为 -1。</summary>
        public int HoleIndex { get; }

        /// <summary>事件发生后的总分。</summary>
        public int Score { get; }

        /// <summary>事件发生后的连击数。</summary>
        public int Combo { get; }

        public override string ToString() =>
            HoleIndex >= 0 ? $"时刻 {Time:0.0}: {Type} 洞 {HoleIndex} 连击 {Combo}" : $"时刻 {Time:0.0}: {Type}";
    }
}
