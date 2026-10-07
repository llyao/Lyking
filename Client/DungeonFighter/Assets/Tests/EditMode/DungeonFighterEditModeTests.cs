using Lyking.DungeonFighter.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.DungeonFighter.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用且表现层映射函数稳定。</summary>
    public sealed class DungeonFighterEditModeTests
    {
        [Test]
        public void 对局在引擎外可直接创建并推进()
        {
            var game = new DungeonGame();

            Assert.AreEqual(3, game.RoomCount);
            Assert.AreEqual(GamePhase.InProgress, game.Phase);
            Assert.IsTrue(game.Player.IsAlive);

            var right = new PlayerInputFrame { MoveX = 1f };
            for (int i = 0; i < 60; i++) game.Tick(1f / 60f, right);
            Assert.Greater(game.Player.Position.X, 2.5f);
        }

        [Test]
        public void 三段普攻在引擎边界内同样推进连击()
        {
            var game = new DungeonGame();
            for (int i = 0; i < 30; i++) game.Tick(1f / 60f, PlayerInputFrame.None); // 等首层怪物落地

            var attack = new PlayerInputFrame { Attack = true };
            game.Tick(1f / 60f, attack);
            Assert.AreEqual(FighterState.Attack, game.Player.State);
            Assert.AreEqual(1, game.Player.ComboIndex);
            Assert.IsNotNull(game.Player.CurrentAttack);
        }

        [Test]
        public void 事件日志保留最近条目()
        {
            var game = new DungeonGame();
            for (int i = 0; i < 300; i++) game.Tick(1f / 60f, PlayerInputFrame.None);

            Assert.IsNotEmpty(game.Events);
            Assert.LessOrEqual(game.Events.Count, DungeonGame.EventCapacity);
            Assert.AreEqual("进入地下城【格兰之森的哥布林巢穴】第 1 层：格兰之森入口", game.Events[0].Message);
        }
    }
}
