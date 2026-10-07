using Lyking.Game2048.Core;
using Lyking.Game2048.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.Game2048.EditModeTests
{
    public sealed class Game2048ViewTests
    {
        [Test]
        public void 空格与各数值底色各不相同()
        {
            Color empty = Game2048View.GetTileColor(0);
            Color two = Game2048View.GetTileColor(2);
            Color eight = Game2048View.GetTileColor(8);
            Color target = Game2048View.GetTileColor(2048);
            Color beyond = Game2048View.GetTileColor(4096);

            Assert.AreNotEqual(empty, two);
            Assert.AreNotEqual(two, eight);
            Assert.AreNotEqual(eight, target);
            Assert.AreNotEqual(target, beyond);
        }

        [Test]
        public void 高数值使用浅色文字低数值使用深色文字()
        {
            Assert.AreEqual(Game2048View.GetTileTextColor(2), Game2048View.GetTileTextColor(4));
            Assert.AreEqual(Game2048View.GetTileTextColor(8), Game2048View.GetTileTextColor(2048));
            Assert.AreNotEqual(Game2048View.GetTileTextColor(4), Game2048View.GetTileTextColor(8));
        }

        [Test]
        public void 状态文案随对局状态变化()
        {
            var playing = new Game2048Game(random: new System.Random(1));
            Assert.AreEqual("方向键或 WASD 滑动，R 键重新开始。", Game2048View.GetStatusMessage(playing));

            playing.Move(MoveDirection.Left);
            Assert.IsNotEmpty(Game2048View.GetStatusMessage(playing));
        }

        [Test]
        public void 结束状态文案包含最终分数()
        {
            var game = new Game2048Game(random: new System.Random(1));
            for (int row = 0; row < game.Board.Rows; row++)
            {
                for (int column = 0; column < game.Board.Columns; column++)
                {
                    game.Board[row, column] = (row + column) % 2 == 0 ? 2 : 4;
                }
            }

            game.Move(MoveDirection.Left);

            StringAssert.Contains("游戏结束", Game2048View.GetStatusMessage(game));
        }
    }

    public sealed class Game2048InputMapTests
    {
        [Test]
        public void 方向键与WASD映射到对应方向()
        {
            Assert.AreEqual(MoveDirection.Up, Game2048InputMap.MapKey(KeyCode.UpArrow));
            Assert.AreEqual(MoveDirection.Up, Game2048InputMap.MapKey(KeyCode.W));
            Assert.AreEqual(MoveDirection.Down, Game2048InputMap.MapKey(KeyCode.DownArrow));
            Assert.AreEqual(MoveDirection.Down, Game2048InputMap.MapKey(KeyCode.S));
            Assert.AreEqual(MoveDirection.Left, Game2048InputMap.MapKey(KeyCode.LeftArrow));
            Assert.AreEqual(MoveDirection.Left, Game2048InputMap.MapKey(KeyCode.A));
            Assert.AreEqual(MoveDirection.Right, Game2048InputMap.MapKey(KeyCode.RightArrow));
            Assert.AreEqual(MoveDirection.Right, Game2048InputMap.MapKey(KeyCode.D));
        }

        [Test]
        public void 非方向键返回空()
        {
            Assert.IsNull(Game2048InputMap.MapKey(KeyCode.Space));
            Assert.IsNull(Game2048InputMap.MapKey(KeyCode.R));
            Assert.IsNull(Game2048InputMap.MapKey(KeyCode.None));
        }
    }
}
