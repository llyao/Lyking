using System.Collections;
using Lyking.SlidingPuzzle.Core;
using Lyking.SlidingPuzzle.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.SlidingPuzzle.PlayModeTests
{
    public sealed class SlidingPuzzlePlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SlidingPuzzleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SlidingPuzzleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(PuzzleRules.DefaultSize, bootstrap.Board.Size);
            Assert.AreEqual(PuzzleRules.DefaultSize * PuzzleRules.DefaultSize, bootstrap.Board.TileCount);
            Assert.AreEqual(0, bootstrap.Board.Steps);
            Assert.IsFalse(bootstrap.Board.IsSolved);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 核心推进一步且重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SlidingPuzzleDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SlidingPuzzleDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 打乱后空位至少有一个合法方向，逐个试探即可推进一步。
            var moved = SlideDir.None;
            foreach (var dir in new[] { SlideDir.Up, SlideDir.Down, SlideDir.Left, SlideDir.Right })
            {
                if (bootstrap!.Move(dir))
                {
                    moved = dir;
                    break;
                }
            }

            Assert.AreNotEqual(SlideDir.None, moved);
            Assert.AreEqual(1, bootstrap!.Board.Steps);
            Assert.AreEqual(SlidingPuzzleEventType.Slid, bootstrap.Board.Events[bootstrap.Board.Events.Count - 1].Type);

            bootstrap.StartGame();
            Assert.AreEqual(0, bootstrap.Board.Steps);
            Assert.AreEqual(PuzzleRules.DefaultSize, bootstrap.Board.Size);
            Assert.AreEqual(PuzzleRules.DefaultSize * PuzzleRules.DefaultSize, bootstrap.Board.TileCount);
            Assert.IsFalse(bootstrap.Board.IsSolved);
        }
    }
}
