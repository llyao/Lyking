using System.Collections;
using Lyking.Reversi.Core;
using Lyking.Reversi.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Reversi.PlayModeTests
{
    public sealed class ReversiPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后对局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("ReversiDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<ReversiDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasMatch);
            Assert.AreEqual((2, 2), bootstrap.Match.Board.Counts);
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board.CurrentTurn);
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 玩家落子触发AI应答且重开复位棋盘()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("ReversiDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<ReversiDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            bootstrap!.PlayAt(2, 3);   // d3 翻 d4
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board[2, 3]);
            Assert.AreEqual((4, 1), bootstrap.Match.Board.Counts);
            Assert.AreEqual(Cell.White, bootstrap.Match.Board.CurrentTurn);

            // AI 在下一帧的 Update 中立即应答。
            yield return null;
            Assert.AreEqual(2, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual(Cell.Black, bootstrap.Match.Board.CurrentTurn);
            Assert.AreEqual(6, bootstrap.Match.Board.Counts.Black + bootstrap.Match.Board.Counts.White);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Match.Board.MoveCount);
            Assert.AreEqual((2, 2), bootstrap.Match.Board.Counts);
            Assert.AreEqual(2, bootstrap.Match.Round);
            Assert.IsFalse(bootstrap.Match.Board.IsFinished);
        }
    }
}
