using System.Collections.Generic;
using System.Text;

namespace Lyking.Wordle.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum WordleEventType
    {
        /// <summary>提交一次满 5 字母且在词库内的猜测，附带判定结果。</summary>
        Submitted,

        /// <summary>提交被拒：不足 5 字母或不在词库内。</summary>
        WordRejected,

        /// <summary>猜中答案获胜。</summary>
        Won,

        /// <summary>六次尝试未中失败，附带答案。</summary>
        Lost,

        /// <summary>开新局（统计保留）。</summary>
        NewGame,

        /// <summary>整体重置：局数回到 1、连胜清零。</summary>
        Reset,
    }

    /// <summary>一条带局数、尝试序号与附带数据的事件记录。</summary>
    public readonly struct WordleEventLog
    {
        public WordleEventLog(int round, int attempt, WordleEventType type, string? guess, string? answer,
            IReadOnlyList<LetterHint>? hints)
        {
            Round = round;
            Attempt = attempt;
            Type = type;
            Guess = guess;
            Answer = answer;
            Hints = hints;
        }

        /// <summary>事件所属局数（从 1 起）。</summary>
        public int Round { get; }

        /// <summary>事件发生时本局已完成的尝试次数；新局与重置事件为 0。</summary>
        public int Attempt { get; }

        public WordleEventType Type { get; }

        /// <summary>提交或被拒的输入词；其余事件为 null。</summary>
        public string? Guess { get; }

        /// <summary>胜负事件携带的答案；其余事件为 null。</summary>
        public string? Answer { get; }

        /// <summary>提交事件附带的逐格判定结果；其余事件为 null。</summary>
        public IReadOnlyList<LetterHint>? Hints { get; }

        public override string ToString()
        {
            switch (Type)
            {
                case WordleEventType.Submitted:
                    return $"第 {Round} 局 第 {Attempt} 次: 提交 {Guess} 判定 {FormatHints(Hints)}";
                case WordleEventType.WordRejected:
                    return $"第 {Round} 局: 拒绝 {Guess}(需 5 字母且在词库)";
                case WordleEventType.Won:
                    return $"第 {Round} 局 第 {Attempt} 次: 猜中，答案 {Answer}";
                case WordleEventType.Lost:
                    return $"第 {Round} 局: 六次未中，答案 {Answer}";
                case WordleEventType.NewGame:
                    return $"第 {Round} 局: 新局开始";
                default:
                    return $"第 {Round} 局: 统计重置";
            }
        }

        /// <summary>把逐格判定渲染为中文串：中=位置正确、存=存在位置不对、无=不存在。</summary>
        private static string FormatHints(IReadOnlyList<LetterHint>? hints)
        {
            if (hints == null) return "-";
            var builder = new StringBuilder(hints.Count);
            for (int i = 0; i < hints.Count; i++)
            {
                builder.Append(hints[i] switch
                {
                    LetterHint.Correct => '中',
                    LetterHint.Present => '存',
                    _ => '无',
                });
            }

            return builder.ToString();
        }
    }
}
