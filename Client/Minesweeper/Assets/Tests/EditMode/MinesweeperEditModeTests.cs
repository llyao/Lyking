#nullable enable
using System.Collections.Generic;
using Lyking.Minesweeper.Core;
using Lyking.Minesweeper.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lyking.Minesweeper.EditModeTests
{
    /// <summary>
    /// 编辑模式测试：验证核心规则随 Unity 程序集编译后行为一致，
    /// 并核对演示场景资产存在且被正确导入。
    /// </summary>
    public sealed class MinesweeperEditModeTests
    {
        private const int Seed = 42;

        [Test]
        public void 难度预设与最小规格在编辑器内成局()
        {
            Assert.AreEqual((9, 9, 10), (BoardSpec.Beginner.Width, BoardSpec.Beginner.Height, BoardSpec.Beginner.MineCount));
            Assert.AreEqual((16, 16, 40), (BoardSpec.Intermediate.Width, BoardSpec.Intermediate.Height, BoardSpec.Intermediate.MineCount));
            Assert.AreEqual((30, 16, 99), (BoardSpec.Expert.Width, BoardSpec.Expert.Height, BoardSpec.Expert.MineCount));

            var tiny = new MinesweeperBoard(new BoardSpec(2, 2, 3), new SystemRandomWrapper(Seed));
            Assert.AreEqual(RevealOutcome.Revealed, tiny.Reveal(0, 0));
            Assert.AreEqual(GameState.Won, tiny.State);
        }

        [Test]
        public void 固定种子首击安全且踩雷判负()
        {
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

            var board = new MinesweeperBoard(BoardSpec.Beginner, new SystemRandomWrapper(Seed));
            var events = new List<MinesweeperEvent>();
            board.EventPublished += events.Add;

            Assert.AreEqual(RevealOutcome.Revealed, board.Reveal(4, 4));
            Assert.AreEqual(GameState.Playing, board.State);
            Assert.IsFalse(board.CellAt(4, 4).HasMine);

            Assert.AreEqual(RevealOutcome.HitMine, board.Reveal(mineX, mineY));
            Assert.AreEqual(GameState.Lost, board.State);
            Assert.Contains(MinesweeperEventType.MineHit, ToTypes(events));
            Assert.Contains(MinesweeperEventType.Lost, ToTypes(events));
        }

        [Test]
        public void 演示场景资产存在()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MinesweeperDemo.unity");
            Assert.IsNotNull(scene, "Assets/Scenes/MinesweeperDemo.unity 应作为资产导入");
        }

        [Test]
        public void 面板坐标命中测试使用默认网格布局()
        {
            var container = new GameObject("PanelHitTest");
            try
            {
                var bootstrap = container.AddComponent<MinesweeperDemoBootstrap>();
                var panel = container.AddComponent<MinesweeperDemoPanel>();
                bootstrap.Restart();
                panel.Bind(bootstrap);

                Assert.IsTrue(panel.TryGetCellIndex(new Vector2(330f + 17f, 16f + 17f), out int x, out int y), "格 (0,0) 中心应命中");
                Assert.AreEqual((0, 0), (x, y));

                Assert.IsTrue(panel.TryGetCellIndex(new Vector2(330f + 2 * 37f + 17f, 16f + 37f + 17f), out x, out y), "格 (2,1) 中心应命中");
                Assert.AreEqual((2, 1), (x, y));

                Assert.IsFalse(panel.TryGetCellIndex(new Vector2(330f + 34f + 1.5f, 16f + 17f), out _, out _), "点在格间间隙上视为未命中");
                Assert.IsFalse(panel.TryGetCellIndex(new Vector2(330f + 9f * 37f + 10f, 16f + 17f), out _, out _), "棋盘右侧之外视为未命中");
                Assert.IsFalse(panel.TryGetCellIndex(new Vector2(-10f, -10f), out _, out _), "负坐标视为未命中");
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        private static List<MinesweeperEventType> ToTypes(List<MinesweeperEvent> events)
        {
            var types = new List<MinesweeperEventType>();
            foreach (MinesweeperEvent item in events)
            {
                types.Add(item.Type);
            }

            return types;
        }
    }
}
