using System.Collections;
using Lyking.Wordle.Core;
using Lyking.Wordle.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Wordle.PlayModeTests
{
    public sealed class WordlePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WordleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WordleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasGame);
            Assert.AreEqual(WordleGameState.Input, bootstrap.Game.State);
            Assert.AreEqual(0, bootstrap.Game.AttemptCount);
            Assert.AreEqual(1, bootstrap.Game.Round);
            Assert.AreEqual(WordleRules.WordLength, bootstrap.Game.Answer.Length);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 提交一次猜测推进核心且重开复位保留局数()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WordleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WordleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 直接驱动测试入口提交一个词库词（可能命中也可能未中，核心都会记一次尝试）。
            string word = WordleRules.WordBank[0];
            foreach (var letter in word) Assert.IsTrue(bootstrap!.TypeLetter(letter));
            Assert.AreEqual(word, bootstrap!.Game.CurrentGuess);
            Assert.IsTrue(bootstrap.Submit());
            Assert.AreEqual(1, bootstrap.Game.AttemptCount);
            Assert.AreEqual(1, bootstrap.Game.Guesses.Count);
            Assert.AreEqual(word, bootstrap.Game.Guesses[0]);

            bootstrap.StartGame();

            Assert.AreEqual(2, bootstrap.Game.Round);
            Assert.AreEqual(0, bootstrap.Game.AttemptCount);
            Assert.AreEqual(0, bootstrap.Game.Guesses.Count);
            Assert.AreEqual(string.Empty, bootstrap.Game.CurrentGuess);
            Assert.AreEqual(WordleGameState.Input, bootstrap.Game.State);
        }
    }
}
