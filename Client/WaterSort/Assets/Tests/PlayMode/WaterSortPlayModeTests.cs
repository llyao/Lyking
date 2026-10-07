using System.Collections;
using Lyking.WaterSort.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.WaterSort.PlayModeTests
{
    public sealed class WaterSortPlayModeTests
    {
        [UnityTest]
        public IEnumerator 场景启动后棋局就绪且面板已绑定()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WaterSortDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WaterSortDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasPuzzle);
            Assert.AreEqual(7, bootstrap.Puzzle.TubeCount);
            Assert.AreEqual(0, bootstrap.Puzzle.Moves);
            Assert.IsFalse(bootstrap.Puzzle.IsSolved);
            Assert.AreEqual(0, bootstrap.Cursor);
            Assert.IsNotNull(bootstrap.Panel);
        }

        [UnityTest]
        public IEnumerator 核心推进撤销与重开复位()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("WaterSortDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<WaterSortDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            // 两段式选瓶：先选中第一瓶，再点尾部空瓶完成倒入。
            int emptyTube = bootstrap!.Puzzle.ColorCount;
            int before = bootstrap.Puzzle.Tubes[0].Length;
            bootstrap.Select(0);
            Assert.AreEqual(0, bootstrap.Puzzle.SelectedTube);
            bootstrap.Select(emptyTube);
            Assert.AreEqual(1, bootstrap.Puzzle.Moves);
            Assert.Less(bootstrap.Puzzle.Tubes[0].Length, before);

            // 撤销恢复层数与步数，再倒一次后重开复位。
            bootstrap.Undo();
            Assert.AreEqual(0, bootstrap.Puzzle.Moves);
            Assert.AreEqual(before, bootstrap.Puzzle.Tubes[0].Length);

            bootstrap.Select(0);
            bootstrap.Select(emptyTube);
            Assert.AreEqual(1, bootstrap.Puzzle.Moves);
            bootstrap.ResetGame();
            Assert.AreEqual(0, bootstrap.Puzzle.Moves);
            Assert.AreEqual(before, bootstrap.Puzzle.Tubes[0].Length);
            Assert.IsEmpty(bootstrap.Puzzle.Tubes[emptyTube]);
            Assert.IsFalse(bootstrap.Puzzle.CanUndo);
        }
    }
}
