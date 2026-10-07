using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.LightsOut.Core;
using NUnit.Framework;
using UnityEngine;

namespace Lyking.LightsOut.EditModeTests
{
    /// <summary>编辑模式冒烟：核心棋局在 Unity 程序集边界内可用，关键规则表现一致。</summary>
    public sealed class LightsOutEditModeTests
    {
        [Test]
        public void 构造后谜题就绪且事件从生成开始()
        {
            var board = new LightsBoard(seed: 7);

            Assert.AreEqual(LightsOutRules.BoardSize, board.Size);
            Assert.AreEqual(0, board.Moves);
            Assert.AreEqual(1, board.PuzzleNumber);
            Assert.IsFalse(board.IsSolved);
            Assert.Greater(board.LightCount, 0);
            Assert.AreEqual(LightsOutEventType.Generated, board.Events[0].Type);

            bool[,] lights = board.Lights;
            int litCount = 0;
            foreach (bool light in lights)
            {
                if (light) litCount++;
            }

            Assert.AreEqual(board.LightCount, litCount);
        }

        [Test]
        public void 中心按下翻转十字且步数递增()
        {
            var board = new LightsBoard(seed: 7);
            bool[,] before = board.Lights;
            int center = LightsOutRules.BoardSize / 2;

            Assert.IsTrue(board.Press(center, center));

            Assert.AreEqual(1, board.Moves);
            Assert.AreEqual(5, board.Events[^1].FlippedCount);
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    bool inCross = Math.Abs(row - center) + Math.Abs(column - center) <= 1;
                    Assert.AreEqual(inCross ? !before[row, column] : before[row, column], board.IsLit(row, column));
                }
            }
        }

        [Test]
        public void 全部熄灭后写入胜利事件并拒绝继续按下()
        {
            var board = new LightsBoard(seed: 5, size: 3);
            var solution = BruteForceMinimalSolution(board);
            Assert.IsNotNull(solution);
            foreach (var (row, column) in solution!) board.Press(row, column);

            Assert.IsTrue(board.IsSolved);
            Assert.AreEqual(0, board.LightCount);
            var types = board.Events.Select(e => e.Type).ToList();
            Assert.Contains(LightsOutEventType.Pressed, types);
            Assert.AreEqual(LightsOutEventType.Won, types[types.Count - 1]);

            int eventCount = board.Events.Count;
            Assert.IsFalse(board.Press(0, 0));
            Assert.AreEqual(eventCount, board.Events.Count);
        }

        [Test]
        public void 重置恢复谜题初始布局并清零步数()
        {
            var board = new LightsBoard(seed: 7);
            bool[,] initial = board.Lights;

            board.Press(2, 2);
            board.Press(0, 0);
            Assert.Greater(board.Moves, 0);

            board.Reset();

            Assert.AreEqual(0, board.Moves);
            Assert.IsFalse(board.IsSolved);
            Assert.AreEqual(LightsOutEventType.Reset, board.Events[^1].Type);
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Assert.AreEqual(initial[row, column], board.IsLit(row, column));
                }
            }
        }

        /// <summary>穷举全部按法找最少按下的解；3x3 谜题由反向按下生成，解必然存在。</summary>
        private static List<(int Row, int Column)>? BruteForceMinimalSolution(LightsBoard board)
        {
            int size = board.Size;
            List<(int Row, int Column)>? best = null;
            for (int mask = 0; mask < 1 << (size * size); mask++)
            {
                var grid = board.Lights;
                var presses = new List<(int Row, int Column)>();
                for (int cell = 0; cell < size * size; cell++)
                {
                    if ((mask & (1 << cell)) == 0) continue;

                    int row = cell / size;
                    int column = cell % size;
                    ToggleCross(grid, size, row, column);
                    presses.Add((row, column));
                }

                if (AllOff(grid) && (best == null || presses.Count < best.Count)) best = presses;
            }

            return best;
        }

        private static void ToggleCross(bool[,] grid, int size, int row, int column)
        {
            Toggle(grid, size, row, column);
            Toggle(grid, size, row - 1, column);
            Toggle(grid, size, row + 1, column);
            Toggle(grid, size, row, column - 1);
            Toggle(grid, size, row, column + 1);
        }

        private static void Toggle(bool[,] grid, int size, int row, int column)
        {
            if (row < 0 || row >= size || column < 0 || column >= size) return;

            grid[row, column] = !grid[row, column];
        }

        private static bool AllOff(bool[,] grid)
        {
            foreach (bool light in grid)
            {
                if (light) return false;
            }

            return true;
        }
    }
}
