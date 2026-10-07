using System.Collections.Generic;

namespace Lyking.Wordle.Core
{
    /// <summary>
    /// 猜单词规则常量：5 字母词、6 次尝试与内置词库。
    /// 答案词库与可输入词库共用同一份内置词表，判定规则写死一致。
    /// </summary>
    public static class WordleRules
    {
        /// <summary>单词长度：每局答案与提交词均为 5 个字母。</summary>
        public const int WordLength = 5;

        /// <summary>每局最大尝试次数：六次未中即失败。</summary>
        public const int MaxAttempts = 6;

        private static readonly string[] BankBuffer =
        {
            "apple", "beach", "brain", "bread",
            "brush", "candy", "chair", "chest",
            "chord", "click", "clock", "cloud",
            "dance", "diary", "dough", "dream",
            "drink", "eagle", "earth", "feast",
            "field", "flame", "frost", "fruit",
            "glass", "globe", "grape", "grass",
            "green", "heart", "honey", "house",
            "juice", "light", "lemon", "mango",
            "money", "month", "mouse", "music",
            "night", "ocean", "onion", "paint",
            "paper", "party", "peace", "pearl",
            "piano", "pizza", "plant", "prize",
            "queen", "radio", "river", "salad",
            "sheep", "shirt", "smile", "snake",
            "sound", "sport", "stone", "storm",
            "sugar", "table", "tiger", "toast",
            "train", "truck", "voice", "watch",
            "water", "whale", "wheat", "wheel",
            "world", "zebra", "broom",
        };

        /// <summary>内置词库：答案与合法输入共用，互不重复的 5 字母小写词。</summary>
        public static IReadOnlyList<string> WordBank { get; } = BankBuffer;

        /// <summary>词是否在词库内（词库全为小写，大小写敏感比对）。</summary>
        public static bool ContainsWord(string word)
        {
            for (int i = 0; i < BankBuffer.Length; i++)
            {
                if (BankBuffer[i] == word) return true;
            }

            return false;
        }

        /// <summary>词是否满足格式：长度 5 且只含小写字母 a-z。</summary>
        public static bool IsLegalFormat(string word)
        {
            if (word == null || word.Length != WordLength) return false;
            for (int i = 0; i < word.Length; i++)
            {
                if (word[i] < 'a' || word[i] > 'z') return false;
            }

            return true;
        }
    }
}
