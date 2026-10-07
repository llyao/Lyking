#nullable enable
using System.Collections;
using System.Collections.Generic;
using Lyking.Minesweeper.Core;
using Lyking.Minesweeper.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lyking.Minesweeper.PlayModeTests
{
    public sealed class MinesweeperDemoPlayModeTests
    {
        private const int Seed = 42;

        [UnityTest]
        public IEnumerator 场景首击进入进行中并开始计时()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MinesweeperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MinesweeperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap!.HasBoard);
            Assert.AreEqual(GameState.Ready, bootstrap.Board.State);

            Assert.AreEqual(RevealOutcome.Revealed, bootstrap.RevealCell(4, 4));
            Assert.AreEqual(GameState.Playing, bootstrap.Board.State);

            yield return new WaitForSecondsRealtime(0.1f);
            Assert.GreaterOrEqual(bootstrap.ElapsedSeconds, 0.05f, "进行中应累计计时");
        }

        [UnityTest]
        public IEnumerator 插旗与重开恢复初始状态()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MinesweeperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MinesweeperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            Assert.AreEqual(FlagOutcome.Flagged, bootstrap!.ToggleFlagCell(0, 0));
            Assert.AreEqual(9, bootstrap.Board.RemainingMineCount);

            Assert.AreEqual(RevealOutcome.Revealed, bootstrap.RevealCell(4, 4));
            Assert.AreEqual(GameState.Playing, bootstrap.Board.State);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(bootstrap.ElapsedSeconds, 0f, "重开前计时应已累计");

            bootstrap.Restart();
            Assert.AreEqual(GameState.Ready, bootstrap.Board.State);
            Assert.AreEqual(0, bootstrap.Board.FlagCount);
            Assert.AreEqual(10, bootstrap.Board.RemainingMineCount);
            Assert.AreEqual(0f, bootstrap.ElapsedSeconds);
            Assert.IsFalse(bootstrap.Board.AreMinesPlaced);
        }

        [UnityTest]
        public IEnumerator 非法坐标不改变状态()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MinesweeperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MinesweeperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);

            Assert.AreEqual(RevealOutcome.InvalidCell, bootstrap!.RevealCell(-1, 0));
            Assert.AreEqual(RevealOutcome.InvalidCell, bootstrap.RevealCell(9, 0));
            Assert.AreEqual(FlagOutcome.InvalidCell, bootstrap.ToggleFlagCell(0, 9));
            Assert.AreEqual(GameState.Ready, bootstrap.Board.State);
            Assert.IsFalse(bootstrap.Board.AreMinesPlaced);
        }

        [UnityTest]
        public IEnumerator 固定种子对局可探测地雷并判负()
        {
            var container = new GameObject("FixedSeedBootstrap");
            var bootstrap = container.AddComponent<MinesweeperDemoBootstrap>();
            bootstrap.UseFixedSeed = true;
            bootstrap.FixedSeed = Seed;
            bootstrap.Restart();

            var probe = new MinesweeperBoard(BoardSpec.Beginner, new SystemRandomWrapper(Seed));
            Assert.AreEqual(RevealOutcome.Revealed, probe.Reveal(4, 4));

            (int mineX, int mineY) = (-1, -1);
            for (int y = 0; y < probe.Height && mineX < 0; y++)
            {
                for (int x = 0; x < probe.Width; x++)
                {
                    if (probe.CellAt(x, y).HasMine)
                    {
                        (mineX, mineY) = (x, y);
                        break;
                    }
                }
            }

            Assert.GreaterOrEqual(mineX, 0, "探针棋盘应能找到地雷");
            Assert.AreEqual(9, probe.Width);

            Assert.AreEqual(RevealOutcome.Revealed, bootstrap.RevealCell(4, 4));
            Assert.AreEqual(RevealOutcome.HitMine, bootstrap.RevealCell(mineX, mineY));
            Assert.AreEqual(GameState.Lost, bootstrap.Board.State);

            Object.Destroy(container);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator 固定种子对局可翻完全部安全格判胜()
        {
            var container = new GameObject("FixedSeedWinBootstrap");
            var bootstrap = container.AddComponent<MinesweeperDemoBootstrap>();
            bootstrap.UseFixedSeed = true;
            bootstrap.FixedSeed = 7;
            bootstrap.Restart();

            var probe = new MinesweeperBoard(BoardSpec.Beginner, new SystemRandomWrapper(7));
            Assert.AreEqual(RevealOutcome.Revealed, probe.Reveal(4, 4));

            Assert.AreEqual(RevealOutcome.Revealed, bootstrap.RevealCell(4, 4));
            for (int y = 0; y < probe.Height; y++)
            {
                for (int x = 0; x < probe.Width; x++)
                {
                    if (bootstrap.Board.State == GameState.Won)
                    {
                        break;
                    }

                    if (!probe.CellAt(x, y).HasMine
                        && bootstrap.Board.CellAt(x, y).State == CellState.Hidden)
                    {
                        Assert.AreEqual(RevealOutcome.Revealed, bootstrap.RevealCell(x, y));
                    }
                }
            }

            Assert.AreEqual(GameState.Won, bootstrap.Board.State);
            Assert.AreEqual(probe.Spec.SafeCellCount, bootstrap.Board.RevealedSafeCount);

            Object.Destroy(container);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator 场景销毁后不会抛出异常()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MinesweeperDemo");
            yield return load;
            yield return null;

            var bootstrap = Object.FindObjectOfType<MinesweeperDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            Object.Destroy(bootstrap!.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
