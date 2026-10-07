using System.Collections.Generic;
using Lyking.Wordle.Core;
using NUnit.Framework;

namespace Lyking.Wordle.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，关键规则与 dotnet 侧表现一致。</summary>
    public sealed class WordleEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建且词库就绪()
        {
            var game = new WordleGame(7);

            Assert.AreEqual(WordleRules.WordLength, game.Answer.Length);
            Assert.IsTrue(WordleRules.ContainsWord(game.Answer));
            Assert.IsTrue(WordleRules.WordBank.Count >= 60);
            Assert.AreEqual(WordleGameState.Input, game.State);
            Assert.AreEqual(0, game.AttemptCount);
            Assert.AreEqual(1, game.Round);
            Assert.IsFalse(game.IsFinished);
        }

        [Test]
        public void 评估器重复字母判定在引擎内一致()
        {
            // 答案 onion 含两个 n：nanny 的三个 n 只能拿到两个配额，其余为灰。
            var hints = WordleEvaluator.Evaluate("nanny", "onion");

            Assert.AreEqual(LetterHint.Present, hints[0]);
            Assert.AreEqual(LetterHint.Miss, hints[1]);
            Assert.AreEqual(LetterHint.Present, hints[2]);
            Assert.AreEqual(LetterHint.Miss, hints[3]);
            Assert.AreEqual(LetterHint.Miss, hints[4]);
        }

        [Test]
        public void 输入提交与拒绝在引擎内正常推进()
        {
            var game = new WordleGame(7);
            Assert.IsFalse(game.Submit()); // 空行提交被拒。
            Assert.AreEqual(0, game.AttemptCount);

            foreach (var letter in game.Answer) Assert.IsTrue(game.TypeLetter(letter));

            Assert.AreEqual(game.Answer, game.CurrentGuess);
            Assert.IsTrue(game.Submit());
            Assert.AreEqual(WordleGameState.Won, game.State);
            Assert.AreEqual(1, game.WinStreak);
            Assert.AreEqual(1, game.Guesses.Count);
        }

        [Test]
        public void 六次未中判负且新局复位保留局数()
        {
            var game = new WordleGame(7);
            foreach (var word in CollectWrongWords(game, WordleRules.MaxAttempts)) 提交错词(game, word);

            Assert.AreEqual(WordleGameState.Lost, game.State);
            Assert.AreEqual(0, game.WinStreak);

            game.NextGame(9);

            Assert.AreEqual(2, game.Round);
            Assert.AreEqual(WordleGameState.Input, game.State);
            Assert.AreEqual(0, game.AttemptCount);
            Assert.AreEqual(0, game.Guesses.Count);
            Assert.AreEqual(string.Empty, game.CurrentGuess);
        }

        /// <summary>逐字母输入并提交一个错词，任何一步失败即测试失败。</summary>
        private static void 提交错词(WordleGame game, string word)
        {
            foreach (var letter in word) Assert.IsTrue(game.TypeLetter(letter), $"输入 {letter} 应当成功。");
            Assert.IsTrue(game.Submit(), $"提交 {word} 应当被接受。");
        }

        /// <summary>取词库中前 count 个不等于当前答案的词。</summary>
        private static List<string> CollectWrongWords(WordleGame game, int count)
        {
            var wrongs = new List<string>();
            foreach (var word in WordleRules.WordBank)
            {
                if (word == game.Answer) continue;
                wrongs.Add(word);
                if (wrongs.Count == count) break;
            }

            Assert.AreEqual(count, wrongs.Count);
            return wrongs;
        }
    }
}
