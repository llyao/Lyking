namespace Lyking.Simon.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum SimonEventType
    {
        /// <summary>首次输入开局。</summary>
        Started,

        /// <summary>开始播放某一等级的序列，附带等级。</summary>
        ShowingStarted,

        /// <summary>一个色块点亮，附带色块编号。</summary>
        Lit,

        /// <summary>复述按对一项，附带已正确的进度。</summary>
        InputCorrect,

        /// <summary>复述按错一项，附带正确答案。</summary>
        InputWrong,

        /// <summary>对局失败（按错或复述超时）。</summary>
        Failed,

        /// <summary>刷新最长纪录，附带纪录值。</summary>
        NewRecord,

        /// <summary>重开一局，日志清空后仅保留此事件。</summary>
        Reset,
    }

    /// <summary>一条带等级、色块编号与附加数值的事件记录。</summary>
    public readonly struct SimonEventLog
    {
        public SimonEventLog(int level, SimonEventType type, int padIndex, int detail)
        {
            Level = level;
            Type = type;
            PadIndex = padIndex;
            Detail = detail;
        }

        /// <summary>事件发生时的等级，开局事件为 0。</summary>
        public int Level { get; }

        public SimonEventType Type { get; }

        /// <summary>关联的色块编号，无关联色块的事件为 -1。</summary>
        public int PadIndex { get; }

        /// <summary>附加数值：复述正确为进度、复述错误为正确答案、新纪录为纪录值，其余为 -1。</summary>
        public int Detail { get; }

        public override string ToString()
        {
            string baseText = PadIndex >= 0 ? $"等级 {Level}: {Type} 色块 {PadIndex}" : $"等级 {Level}: {Type}";
            return Detail >= 0 ? $"{baseText} 数值 {Detail}" : baseText;
        }
    }
}
