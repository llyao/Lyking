using System.Linq;
using Lyking.BattleCity.Core;
using NUnit.Framework;

namespace Lyking.BattleCity.EditModeTests
{
    /// <summary>编辑模式冒烟：核心战局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class BattleCityEditModeTests
    {
        private static readonly string[] BrickSteelMap =
        {
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".....#.......",
            ".....B.......",
            ".....B.......",
            ".....P......E",
        };

        private static readonly string[] BaseMap =
        {
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            ".............",
            "..P..BE......",
        };

        /// <summary>禁用敌人开火用的超长冷却：测试时间窗内敌人不会射击。</summary>
        private const float NoFire = 1_000_000f;

        [Test]
        public void 战局在引擎外可创建并推进()
        {
            var game = new BattleCityGame(BattleCityRules.Default, seed: 7);

            Assert.AreEqual(new Vec2(4.5f, 0.5f), game.Player.Position);
            Assert.AreEqual(1, game.Enemies.Count);
            Assert.IsFalse(game.IsFinished);
            Assert.GreaterOrEqual(game.Enemies[0].Position.X, 0.5f);
            Assert.LessOrEqual(game.Enemies[0].Position.X, 12.5f);

            float yBefore = game.Enemies[0].Position.Y;
            game.Step(0.5f, new TankInput(TankDir.Up, false));
            Assert.Greater(game.Elapsed, 0f);
            Assert.Less(game.Enemies[0].Position.Y, yBefore);
            Assert.Greater(game.Player.Position.Y, 0.5f);
        }

        [Test]
        public void 射击沿出膛且冷却内被拒绝()
        {
            var game = new BattleCityGame(BattleCityRules.Default, seed: 14);

            game.Step(0f, new TankInput(TankDir.None, true));
            Assert.AreEqual(1, game.Bullets.Count);
            Assert.AreEqual(1.12f, game.Bullets[0].Position.Y, 0.001f);

            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.2f, new TankInput(TankDir.None, true));
            Assert.AreEqual(1, game.Bullets.Count);
            game.Step(0.1f, new TankInput(TankDir.None, true));
            Assert.AreEqual(2, game.Bullets.Count);
        }

        [Test]
        public void 砖墙被摧毁而钢墙阻挡子弹()
        {
            var rules = new BattleCityRules(
                BrickSteelMap, 3.2f, 0f, 6f, 0.12f, 0.5f, 0.2f, NoFire, 6, 3f, 3, 2f, 100, 0.1f);
            var game = new BattleCityGame(rules, seed: 3);

            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, new TankInput(TankDir.None, true));
            game.Step(0.3f, default);

            Assert.AreEqual(BattleCityCell.Empty, game.Map.CellAt(new GridPos(5, 1)));
            Assert.AreEqual(BattleCityCell.Empty, game.Map.CellAt(new GridPos(5, 2)));
            Assert.AreEqual(BattleCityCell.Steel, game.Map.CellAt(new GridPos(5, 3)));
            Assert.AreEqual(2, game.Events.Count(e => e.Type == BattleCityEventType.BrickDestroyed));
            Assert.IsEmpty(game.Bullets);
        }

        [Test]
        public void 游戏结束后推进保持状态不变()
        {
            var rules = new BattleCityRules(
                BaseMap, 3.2f, 0f, 6f, 0.12f, 0.5f, 0.2f, NoFire, 6, 3f, 3, 2f, 100, 0.1f);
            var game = new BattleCityGame(rules, seed: 3);

            // 先转向右，再两连发：第一发摧毁护基地砖墙，第二发命中基地
            game.Step(0.1f, new TankInput(TankDir.Right, false));
            game.Step(0.5f, new TankInput(TankDir.None, true));
            Assert.IsFalse(game.IsFinished);
            game.Step(0.5f, new TankInput(TankDir.None, true));
            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(BattleCityEventType.GameOver, game.Events[game.Events.Count - 1].Type);

            float elapsed = game.Elapsed;
            int score = game.Score;
            game.Step(1f, new TankInput(TankDir.Up, true));
            Assert.AreEqual(elapsed, game.Elapsed, 0.0001f);
            Assert.AreEqual(score, game.Score);
            Assert.IsEmpty(game.Bullets);
        }
    }
}
