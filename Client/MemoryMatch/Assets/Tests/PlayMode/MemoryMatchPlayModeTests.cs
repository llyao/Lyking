using System.Collections;
using Lyking.MemoryMatch.Core;
using Lyking.MemoryMatch.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.MemoryMatch.PlayModeTests
{
    public sealed class MemoryMatchPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MemoryMatchDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MemoryMatchDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(MemoryMatchRules.CardCount, bootstrap.Board.CardCount);
            Assert.AreEqual(MemoryMatchPhase.Idle, bootstrap.Board.Phase);
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.IsFalse(bootstrap.Board.IsWon);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 模拟入口推进倒计时翻回且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MemoryMatchDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MemoryMatchDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            var board = bootstrap!.Board;
            var pair = FindMismatchPair(board);
            Assert.IsTrue(bootstrap.Flip(pair.First));
            Assert.IsTrue(bootstrap.Flip(pair.Second));
            Assert.AreEqual(MemoryMatchPhase.TwoOpen, board.Phase);

            bootstrap.Step(MemoryMatchRules.FlipBackSeconds + 0.5f);
            Assert.AreEqual(MemoryMatchPhase.Idle, board.Phase);
            Assert.AreEqual(MemoryCardState.FaceDown, board.GetCard(pair.First).State);
            Assert.AreEqual(MemoryCardState.FaceDown, board.GetCard(pair.Second).State);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.Moves);
            Assert.AreEqual(0, bootstrap.Board.MatchedPairs);
            Assert.AreEqual(MemoryMatchRules.CardCount, bootstrap.Board.CardCount);
            Assert.IsFalse(bootstrap.Board.IsWon);
        }

        private static (int First, int Second) FindMismatchPair(MemoryBoard board)
        {
            for (int j = 1; j < board.CardCount; j++)
            {
                if (board.GetCard(0).Pattern != board.GetCard(j).Pattern) return (0, j);
            }

            Assert.Fail("4x4 至少两种图案，必能找到不配对组合。");
            return (0, 1);
        }
    }
}
