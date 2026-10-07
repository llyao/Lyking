using System.Collections.Generic;
using Lyking.Wordle.Core;
using Xunit;

namespace Lyking.Wordle.Core.Tests
{
    /// <summary>猜单词核心测试：词库、两趟判定算法、状态机、统计与确定性。</summary>
    public class WordleCoreTests
    {
        [Fact]
        public void 词库规模区间与格式满足要求()
        {
            var bank = WordleRules.WordBank;

            Assert.InRange(bank.Count, 60, 120);
            Assert.All(bank, word => Assert.Equal(WordleRules.WordLength, word.Length));
            Assert.All(bank, word => Assert.Matches("^[a-z]+$", word));
            Assert.Equal(bank.Count, bank.Distinct().Count());
            // 答案词库与可输入词库共用同一份，判定规则写死一致。
            Assert.All(bank, word => Assert.True(WordleRules.ContainsWord(word)));
            Assert.False(WordleRules.ContainsWord("zzzzz"));
        }

        [Fact]
        public void 固定种子选词可复现()
        {
            Assert.Equal(new WordleGame(2024).Answer, new WordleGame(2024).Answer);
            Assert.Equal(new WordleGame(-7).Answer, new WordleGame(-7).Answer);
            Assert.True(WordleRules.ContainsWord(new WordleGame(2024).Answer));
        }

        [Fact]
        public void 字母逐个输入且超长与非字母被拒()
        {
            var game = new WordleGame(42);

            Assert.True(game.TypeLetter('h'));
            Assert.True(game.TypeLetter('e'));
            Assert.True(game.TypeLetter('l'));
            Assert.True(game.TypeLetter('l'));
            Assert.True(game.TypeLetter('o'));
            Assert.Equal("hello", game.CurrentGuess);

            // 当前行已满、大写、数字与符号一律拒绝，当前行保持不变。
            Assert.False(game.TypeLetter('x'));
            Assert.False(game.TypeLetter('A'));
            Assert.False(game.TypeLetter('3'));
            Assert.False(game.TypeLetter('!'));
            Assert.Equal("hello", game.CurrentGuess);
        }

        [Fact]
        public void 退格删除最后一个字母且空行退格被拒()
        {
            var game = new WordleGame(13);
            foreach (var letter in "tea") Assert.True(game.TypeLetter(letter));

            Assert.True(game.Backspace());
            Assert.Equal("te", game.CurrentGuess);
            Assert.True(game.Backspace());
            Assert.True(game.Backspace());
            Assert.Equal(string.Empty, game.CurrentGuess);
            Assert.False(game.Backspace());
            Assert.Equal(string.Empty, game.CurrentGuess);
        }

        [Fact]
        public void 输入与退格不产生事件()
        {
            var game = new WordleGame(13);
            Assert.Empty(game.Events);

            Assert.True(game.TypeLetter('c'));
            Assert.True(game.TypeLetter('a'));
            Assert.True(game.TypeLetter('t'));
            Assert.True(game.Backspace());
            Assert.Equal("ca", game.CurrentGuess);
            Assert.Empty(game.Events);
        }

        [Fact]
        public void 不足五字母提交被拒且不消耗尝试()
        {
            var game = new WordleGame(42);
            foreach (var letter in "cat") Assert.True(game.TypeLetter(letter));

            Assert.False(game.Submit());
            Assert.Equal(0, game.AttemptCount);
            Assert.Equal(WordleGameState.Input, game.State);
            Assert.Equal("cat", game.CurrentGuess);
            Assert.Equal(WordleEventType.WordRejected, game.Events[game.Events.Count - 1].Type);
            Assert.Equal("cat", game.Events[game.Events.Count - 1].Guess);
        }

        [Fact]
        public void 非词库词提交被拒且不消耗尝试()
        {
            var game = new WordleGame(42);
            foreach (var letter in "zzzzz") Assert.True(game.TypeLetter(letter));

            Assert.False(game.Submit());
            Assert.Equal(0, game.AttemptCount);
            Assert.Equal(WordleGameState.Input, game.State);
            Assert.Equal(WordleEventType.WordRejected, game.Events[game.Events.Count - 1].Type);
            Assert.Equal("zzzzz", game.Events[game.Events.Count - 1].Guess);
        }

        [Fact]
        public void 全对判定全绿()
        {
            var hints = WordleEvaluator.Evaluate("apple", "apple");

            Assert.Equal(WordleRules.WordLength, hints.Count);
            Assert.All(hints, hint => Assert.Equal(LetterHint.Correct, hint));
        }

        [Fact]
        public void 位置不对标黄且不存在标灰()
        {
            // 答案 apple：guess penal 中 p 与 e 存在但位置不对（黄），n 不存在（灰），a 与 l 存在但位置不对（黄）。
            var hints = WordleEvaluator.Evaluate("penal", "apple");

            Assert.Equal(LetterHint.Present, hints[0]);
            Assert.Equal(LetterHint.Present, hints[1]);
            Assert.Equal(LetterHint.Miss, hints[2]);
            Assert.Equal(LetterHint.Present, hints[3]);
            Assert.Equal(LetterHint.Present, hints[4]);
        }

        [Fact]
        public void 重复字母先绿后黄贪心匹配()
        {
            // 答案 alarm 含两个 a：猜 aabbz，位置 0 的 a 命中为绿，位置 1 的 a 仍有配额为黄，其余为灰。
            var hints = WordleEvaluator.Evaluate("aabbz", "alarm");

            Assert.Equal(LetterHint.Correct, hints[0]);
            Assert.Equal(LetterHint.Present, hints[1]);
            Assert.Equal(LetterHint.Miss, hints[2]);
            Assert.Equal(LetterHint.Miss, hints[3]);
            Assert.Equal(LetterHint.Miss, hints[4]);
        }

        [Fact]
        public void 超量重复字母只按答案剩余量标黄()
        {
            // 答案 onion 含两个 n：guess nanny 出现三个 n，只有两个能拿到配额标黄，第三个为灰。
            var hints = WordleEvaluator.Evaluate("nanny", "onion");

            Assert.Equal(LetterHint.Present, hints[0]);
            Assert.Equal(LetterHint.Miss, hints[1]);
            Assert.Equal(LetterHint.Present, hints[2]);
            Assert.Equal(LetterHint.Miss, hints[3]);
            Assert.Equal(LetterHint.Miss, hints[4]);
        }

        [Fact]
        public void 猜中即胜且与剩余行数无关()
        {
            // 第一发直接命中，提交成功后当前行清空。
            var first = new WordleGame(5);
            TypeAndSubmit(first, first.Answer);

            Assert.Equal(WordleGameState.Won, first.State);
            Assert.Equal(string.Empty, first.CurrentGuess);
            Assert.Equal(1, first.WinStreak);
            Assert.Equal(WordleEventType.Won, first.Events[first.Events.Count - 1].Type);

            // 拖到第 6 发命中同样获胜而非判负。
            var last = new WordleGame(5);
            foreach (var word in WrongWords(last, WordleRules.MaxAttempts - 1)) TypeAndSubmit(last, word);
            TypeAndSubmit(last, last.Answer);

            Assert.Equal(WordleGameState.Won, last.State);
            Assert.Equal(WordleRules.MaxAttempts, last.AttemptCount);
            Assert.Equal(1, last.WinStreak);
        }

        [Fact]
        public void 六次未中判负且连胜清零并公布答案()
        {
            var game = new WordleGame(11);
            TypeAndSubmit(game, game.Answer); // 先赢一局攒连胜。
            game.NextGame(12);
            foreach (var word in WrongWords(game, WordleRules.MaxAttempts)) TypeAndSubmit(game, word);

            Assert.Equal(WordleGameState.Lost, game.State);
            Assert.True(game.IsFinished);
            Assert.Equal(0, game.WinStreak);
            var lost = game.Events[game.Events.Count - 1];
            Assert.Equal(WordleEventType.Lost, lost.Type);
            Assert.Equal(game.Answer, lost.Answer);
            // 终局后提交与输入均被拒绝。
            Assert.False(game.Submit());
            Assert.False(game.TypeLetter('a'));
        }

        [Fact]
        public void 连胜统计跨局累计()
        {
            var game = new WordleGame(3);
            TypeAndSubmit(game, game.Answer);
            Assert.Equal(1, game.WinStreak);
            Assert.Equal(1, game.Round);

            game.NextGame(4);
            TypeAndSubmit(game, game.Answer);

            Assert.Equal(2, game.WinStreak);
            Assert.Equal(2, game.Round);
        }

        [Fact]
        public void 重开清盘且保留统计()
        {
            var game = new WordleGame(6);
            // 提交一个错词（不终局）后再输入两个字母，随后重开应清盘并保留统计。
            TypeAndSubmit(game, WrongWords(game, 1)[0]);
            int streakBefore = game.WinStreak;
            Assert.Equal(1, game.AttemptCount);
            foreach (var letter in "qu") Assert.True(game.TypeLetter(letter));

            game.NextGame(7);

            Assert.Equal(2, game.Round);
            Assert.Equal(0, game.AttemptCount);
            Assert.Empty(game.Guesses);
            Assert.Empty(game.Hints);
            Assert.Equal(string.Empty, game.CurrentGuess);
            Assert.Equal(WordleGameState.Input, game.State);
            Assert.Equal(streakBefore, game.WinStreak);
            Assert.Equal(WordleEventType.NewGame, game.Events[game.Events.Count - 1].Type);
        }

        [Fact]
        public void 统计重置清零连胜与局数()
        {
            var game = new WordleGame(21);
            TypeAndSubmit(game, game.Answer);
            Assert.Equal(1, game.WinStreak);

            game.Reset(22);

            Assert.Equal(1, game.Round);
            Assert.Equal(0, game.WinStreak);
            Assert.Equal(WordleGameState.Input, game.State);
            Assert.Equal(0, game.AttemptCount);
            Assert.Equal(WordleEventType.Reset, game.Events[game.Events.Count - 1].Type);
        }

        [Fact]
        public void 事件日志封顶裁剪且保留最近事件()
        {
            var game = new WordleGame(8);
            for (int round = 0; round < 50; round++)
            {
                if (round > 0) game.NextGame(8 + round);
                TypeAndSubmit(game, game.Answer);
            }

            Assert.True(game.Events.Count <= 128, "事件日志必须按上限封顶。");
            Assert.Equal(WordleEventType.Won, game.Events[game.Events.Count - 1].Type);
            // 裁剪后保留的是最近事件，首条不应再属于第 1 局。
            Assert.NotEqual(1, game.Events[0].Round);
        }

        [Fact]
        public void 同种子同输入序列结果完全一致()
        {
            var left = new WordleGame(99);
            var right = new WordleGame(99);
            Assert.Equal(left.Answer, right.Answer);

            foreach (var word in WrongWords(left, WordleRules.MaxAttempts))
            {
                foreach (var game in new[] { left, right }) TypeAndSubmit(game, word);
            }

            for (int i = 0; i < WordleRules.MaxAttempts; i++)
            {
                Assert.Equal(left.Guesses[i], right.Guesses[i]);
                Assert.Equal(left.Hints[i], right.Hints[i]);
            }

            Assert.Equal(left.State, right.State);
            Assert.Equal(left.Events.Count, right.Events.Count);
        }

        /// <summary>逐字母输入并提交一个词，任何一步失败即测试失败。</summary>
        private static void TypeAndSubmit(WordleGame game, string word)
        {
            foreach (var letter in word) Assert.True(game.TypeLetter(letter), $"输入 {letter} 应当成功。");
            Assert.True(game.Submit(), $"提交 {word} 应当被接受。");
        }

        /// <summary>取词库中前 count 个不等于当前答案的词。</summary>
        private static List<string> WrongWords(WordleGame game, int count)
        {
            var wrongs = new List<string>();
            foreach (var word in WordleRules.WordBank)
            {
                if (word == game.Answer) continue;
                wrongs.Add(word);
                if (wrongs.Count == count) break;
            }

            Assert.Equal(count, wrongs.Count);
            return wrongs;
        }
    }
}
