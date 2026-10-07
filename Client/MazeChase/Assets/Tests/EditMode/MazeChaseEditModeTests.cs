using System.Linq;
using Lyking.MazeChase.Core;
using NUnit.Framework;

namespace Lyking.MazeChase.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class MazeChaseEditModeTests
    {
        [Test]
        public void 核心棋局在引擎外可创建且初始状态合法()
        {
            var game = new MazeChaseGame(MazeChaseRules.Default, seed: 7);

            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(3, game.Lives);
            Assert.AreEqual(1, game.Level);
            Assert.Greater(game.PelletsRemaining, 0);
            Assert.IsFalse(game.IsFinished);
            Assert.AreEqual(MazeDir.None, game.PlayerDirection);
            Assert.AreEqual(MazeChaseRules.Default.Map.PlayerStart, game.PlayerCell);
            Assert.AreEqual(MazeChaseRules.Default.GhostCount, game.Ghosts.Count);
        }

        [Test]
        public void 意向方向驱动玩家沿通道移动()
        {
            var game = new MazeChaseGame(MazeChaseRules.Default, seed: 7);
            float xBefore = game.PlayerX;

            game.SetIntent(MazeDir.Right);
            game.Step(0.5f);

            Assert.Greater(game.PlayerX, xBefore);
            Assert.AreEqual(MazeDir.Right, game.PlayerDirection);
        }

        [Test]
        public void 吃豆加分并写入事件日志()
        {
            var game = new MazeChaseGame(MazeChaseRules.Default, seed: 7);

            game.SetIntent(MazeDir.Right);
            for (int i = 0; i < 40 && game.Score == 0; i++) game.Step(0.05f);

            Assert.Greater(game.Score, 0);
            Assert.IsTrue(game.Events.Any(e => e.Type == MazeChaseEventType.AtePellet));
        }

        [Test]
        public void 幽灵出生在巢格且初始不惊惧()
        {
            var game = new MazeChaseGame(MazeChaseRules.Default, seed: 7);
            var houses = MazeChaseRules.Default.Map.GhostHouses;

            for (int i = 0; i < game.Ghosts.Count; i++)
            {
                Assert.IsTrue(houses.Any(house => house == game.Ghosts[i].Cell));
                Assert.IsFalse(game.Ghosts[i].IsFrightened);
                Assert.IsFalse(game.Ghosts[i].IsRespawning);
            }
        }
    }
}
