using Lyking.GameOfLife.Core;
using Lyking.GameOfLife.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.GameOfLife.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局与演示引导在 Unity 程序集边界内可用，固定节拍推进确定。</summary>
    public sealed class GameOfLifeEditModeTests
    {
        [Test]
        public void 构造后装配随机棋局并可推进()
        {
            var go = new GameObject("bootstrap");
            var bootstrap = go.AddComponent<GameOfLifeDemoBootstrap>();

            bootstrap.StartGame();

            Assert.IsTrue(bootstrap.HasBoard);
            Assert.AreEqual(LifeRules.DefaultWidth, bootstrap.Board.Width);
            Assert.AreEqual(LifeRules.DefaultHeight, bootstrap.Board.Height);
            Assert.Greater(bootstrap.Board.AliveCount, 0);
            Assert.AreEqual(0, bootstrap.Board.Generation);

            bootstrap.Board.Step();
            Assert.AreEqual(1, bootstrap.Board.Generation);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void 固定节拍批量推进与逐代单步一致()
        {
            var firstGo = new GameObject("batched");
            var secondGo = new GameObject("stepped");
            var batched = firstGo.AddComponent<GameOfLifeDemoBootstrap>();
            var stepped = secondGo.AddComponent<GameOfLifeDemoBootstrap>();

            batched.StartGame();
            stepped.StartGame();
            batched.Board.Randomize(2024, LifeRules.DefaultDensity);
            stepped.Board.Randomize(2024, LifeRules.DefaultDensity);

            batched.Step(1f);                       // 1 秒按 0.125 秒/代 = 恰好 8 代

            Assert.AreEqual(8, batched.Board.Generation);
            for (int i = 0; i < 8; i++) stepped.StepOnce();
            Assert.AreEqual(8, stepped.Board.Generation);
            Assert.AreEqual(Snapshot(stepped.Board), Snapshot(batched.Board));

            for (int i = 0; i < 4; i++) batched.Step(0.04f);    // 累积 0.16 秒再推进 1 代
            stepped.StepOnce();

            Assert.AreEqual(9, batched.Board.Generation);
            Assert.AreEqual(Snapshot(stepped.Board), Snapshot(batched.Board));

            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
        }

        [Test]
        public void 暂停时固定节拍不推进而单步仍可用()
        {
            var go = new GameObject("bootstrap");
            var bootstrap = go.AddComponent<GameOfLifeDemoBootstrap>();
            bootstrap.StartGame();
            int baseline = bootstrap.Board.Generation;

            bootstrap.SetPaused(true);
            bootstrap.Step(1f);
            Assert.AreEqual(baseline, bootstrap.Board.Generation);
            Assert.IsTrue(bootstrap.Board.IsPaused);

            bootstrap.StepOnce();
            Assert.AreEqual(baseline + 1, bootstrap.Board.Generation);

            bootstrap.SetPaused(false);
            bootstrap.Step(0.125f);
            Assert.AreEqual(baseline + 2, bootstrap.Board.Generation);

            var types = new System.Collections.Generic.List<LifeEventType>();
            foreach (LifeEventLog entry in bootstrap.Board.Events) types.Add(entry.Type);
            Assert.Contains(LifeEventType.Paused, types);
            Assert.Contains(LifeEventType.Resumed, types);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void 清空载入图案与随机重撒经引导生效()
        {
            var go = new GameObject("bootstrap");
            var bootstrap = go.AddComponent<GameOfLifeDemoBootstrap>();
            bootstrap.StartGame();
            Assert.Greater(bootstrap.Board.AliveCount, 0);

            bootstrap.Clear();
            Assert.AreEqual(0, bootstrap.Board.AliveCount);
            Assert.AreEqual(0, bootstrap.Board.Generation);

            bootstrap.LoadGun();
            Assert.AreEqual(36, bootstrap.Board.AliveCount);
            Assert.AreEqual("滑翔机枪", bootstrap.Board.Events[bootstrap.Board.Events.Count - 1].PatternName);

            bootstrap.Randomize();
            Assert.Greater(bootstrap.Board.AliveCount, 0);
            Assert.AreEqual(LifeEventType.Randomized, bootstrap.Board.Events[bootstrap.Board.Events.Count - 1].Type);

            Object.DestroyImmediate(go);
        }

        private static string Snapshot(LifeBoard board)
        {
            bool[,] cells = board.Cells;
            var builder = new System.Text.StringBuilder();
            for (int y = 0; y < cells.GetLength(1); y++)
            {
                for (int x = 0; x < cells.GetLength(0); x++)
                {
                    builder.Append(cells[x, y] ? '1' : '0');
                }
            }

            return builder.ToString();
        }
    }
}
