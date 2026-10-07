using System;
using System.Collections.Generic;

namespace Lyking.Wordle.Core
{
    /// <summary>
    /// 字母判定纯函数：两趟算法正确处理重复字母。
    /// 第一趟按位置标出全部 Correct（绿），并把答案中对应字母的配额扣掉；
    /// 第二趟对剩余字母按答案中该字母的剩余配额标 Present（黄），配额用尽或对不上标 Miss（灰）。
    /// 因此同一字母重复出现时先保证位置命中的绿，剩余配额才给黄，超出部分一律为灰。
    /// </summary>
    public static class WordleEvaluator
    {
        /// <summary>评估一次猜测：返回 5 个 <see cref="LetterHint"/>，与提交顺序一一对应。</summary>
        public static IReadOnlyList<LetterHint> Evaluate(string guess, string answer)
        {
            if (guess == null) throw new ArgumentNullException(nameof(guess));
            if (answer == null) throw new ArgumentNullException(nameof(answer));
            if (guess.Length != WordleRules.WordLength || answer.Length != WordleRules.WordLength)
            {
                throw new ArgumentException($"判定要求双方均为 {WordleRules.WordLength} 个字母。");
            }

            var hints = new LetterHint[WordleRules.WordLength];
            var remaining = new int[26];
            for (int i = 0; i < answer.Length; i++) remaining[answer[i] - 'a']++;

            // 第一趟：位置命中即 Correct，并占用答案中的该字母配额。
            for (int i = 0; i < guess.Length; i++)
            {
                if (guess[i] != answer[i]) continue;
                hints[i] = LetterHint.Correct;
                remaining[guess[i] - 'a']--;
            }

            // 第二趟：剩余配额内标 Present，配额用尽或字母不存在标 Miss。
            for (int i = 0; i < guess.Length; i++)
            {
                if (hints[i] == LetterHint.Correct) continue;
                int letter = guess[i] - 'a';
                if (remaining[letter] > 0)
                {
                    hints[i] = LetterHint.Present;
                    remaining[letter]--;
                }
                else
                {
                    hints[i] = LetterHint.Miss;
                }
            }

            return hints;
        }
    }
}
