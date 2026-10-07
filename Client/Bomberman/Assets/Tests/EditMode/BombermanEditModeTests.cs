using Lyking.Bomberman.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Bomberman.EditModeTests
{
    /// <summary>编辑模式冒烟：核心战局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class BombermanEditModeTests
    {
        [Test]
        public void 战局在引擎外可创建且初始状态合法()
        {
            var game = new BombermanGame(3);

            Assert.AreEqual(1, game.Level);
            Assert.AreEqual(13, game.Width);
            Assert.AreEqual(11, game.Height);
            Assert.AreEqual(new GridPos(1, 1), game.Player);
            Assert.AreEqual(3, game.Lives);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(3, game.Enemies.Count);
            Assert.AreEqual(0, game.Bombs.Count);
            Assert.IsFalse(game.IsExitOpen);
            Assert.IsFalse(game.IsGameOver);
            Assert.AreEqual(BombermanEventType.LevelStarted, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 移动撞边框硬墙被拒绝()
        {
            var game = new BombermanGame(3);

            game.Step(0.25f, new BombInput(BombermanDir.Left, false));

            Assert.AreEqual(new GridPos(1, 1), game.Player);
            Assert.AreEqual(BombermanEventType.MoveRejected, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 放弹推进引信到期爆炸并烧到自己()
        {
            var game = new BombermanGame(3);

            game.Step(0.25f, new BombInput(BombermanDir.None, true));
            Assert.AreEqual(1, game.Bombs.Count);
            Assert.IsTrue(game.IsBombAt(new GridPos(1, 1)));

            game.Step(1.25f, default);
            game.Step(1.25f, default);

            Assert.AreEqual(0, game.Bombs.Count);

            var exploded = false;
            foreach (var e in game.Events)
            {
                if (e.Type == BombermanEventType.Explosion) exploded = true;
            }

            Assert.IsTrue(exploded);
            Assert.AreEqual(2, game.Lives);
            Assert.AreEqual(new GridPos(1, 1), game.Player);
        }

        [Test]
        public void 相同种子对局推进结果一致()
        {
            static (int Score, int Lives, GridPos Player, int Events) Simulate()
            {
                var game = new BombermanGame(7);
                var dirs = new[] { BombermanDir.Up, BombermanDir.Right, BombermanDir.Down, BombermanDir.None };
                for (int frame = 0; frame < 40; frame++)
                {
                    var input = new BombInput(dirs[frame % dirs.Length], frame % 9 == 0);
                    game.Step(0.1f, input);
                }

                return (game.Score, game.Lives, game.Player, game.Events.Count);
            }

            var first = Simulate();
            var second = Simulate();
            Assert.AreEqual(first.Score, second.Score);
            Assert.AreEqual(first.Lives, second.Lives);
            Assert.AreEqual(first.Player, second.Player);
            Assert.AreEqual(first.Events, second.Events);
        }
    }
}
