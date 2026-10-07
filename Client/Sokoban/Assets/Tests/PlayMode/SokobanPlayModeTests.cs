using System.Collections;
using Lyking.Sokoban.Core;
using Lyking.Sokoban.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Sokoban.PlayModeTests
{
    public sealed class SokobanPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SokobanDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SokobanDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasPuzzle);
            Assert.AreEqual(0, bootstrap.Puzzle.LevelIndex);
            Assert.AreEqual(0, bootstrap.Puzzle.MoveCount);
            Assert.IsFalse(bootstrap.Puzzle.IsSolved);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 核心推进通关后重置复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("SokobanDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<SokobanDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 第一关一推即过：向右把箱子推上目标。
            bootstrap!.Move(SokobanDir.Right);
            Assert.IsTrue(bootstrap.Puzzle.IsSolved);
            Assert.AreEqual(1, bootstrap.Puzzle.MoveCount);
            Assert.AreEqual(1, bootstrap.Puzzle.PushCount);

            bootstrap.Puzzle.Reset();
            Assert.AreEqual(0, bootstrap.Puzzle.MoveCount);
            Assert.AreEqual(0, bootstrap.Puzzle.PushCount);
            Assert.IsFalse(bootstrap.Puzzle.IsSolved);
            Assert.AreEqual(new GridPos(2, 2), bootstrap.Puzzle.Player);
            Assert.IsTrue(bootstrap.Puzzle.Boxes.Contains(new GridPos(3, 2)));
        }
    }
}
