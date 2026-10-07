using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.Sokoban.Core;
using Xunit;

namespace Lyking.Sokoban.Core.Tests
{
    /// <summary>推箱子核心规则测试：关卡解析、推箱、拒绝、目标、过关、撤销、重置与关卡切换。</summary>
    public class SokobanPuzzleTests
    {
        /// <summary>一推即过关的极小关卡。</summary>
        private static readonly string[] OnePushMap =
        {
            "#####",
            "#@$.#",
            "#####",
        };

        /// <summary>箱子前方还有另一箱的关卡。</summary>
        private static readonly string[] TwoBoxesMap =
        {
            "######",
            "#@$$.#",
            "#  . #",
            "######",
        };

        /// <summary>箱子紧贴墙的关卡。</summary>
        private static readonly string[] BoxAgainstWallMap =
        {
            "#######",
            "#.@$# #",
            "#######",
        };

        /// <summary>玩家可以走上目标点的关卡。</summary>
        private static readonly string[] WalkOnGoalMap =
        {
            "#####",
            "#@.$#",
            "#####",
        };

        [Fact]
        public void 构造后第一关初始状态合法()
        {
            var puzzle = new SokobanPuzzle();

            Assert.Equal(0, puzzle.LevelIndex);
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.False(puzzle.IsSolved);
            Assert.True(puzzle.HasNextLevel);
            Assert.False(puzzle.IsAllCompleted);
            Assert.Equal(new GridPos(2, 2), puzzle.Player);
            Assert.Contains(new GridPos(3, 2), puzzle.Boxes);
            Assert.Single(puzzle.CurrentLevel.InitialBoxes);
            Assert.True(puzzle.CurrentLevel.IsGoal(new GridPos(4, 2)));
            Assert.Equal(SokobanEventType.LevelChanged, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 推箱成功且计入步数与推动数()
        {
            var puzzle = new SokobanPuzzle();

            Assert.True(puzzle.Move(SokobanDir.Right));

            Assert.Equal(new GridPos(3, 2), puzzle.Player);
            Assert.Contains(new GridPos(4, 2), puzzle.Boxes);
            Assert.DoesNotContain(new GridPos(3, 2), puzzle.Boxes);
            Assert.Equal(1, puzzle.MoveCount);
            Assert.Equal(1, puzzle.PushCount);
            Assert.Contains(puzzle.Events, e => e.Type == SokobanEventType.Pushed);
        }

        [Fact]
        public void 推向墙被拒绝且不产生位移()
        {
            var puzzle = new SokobanPuzzle(new SokobanRules(new[] { OnePushMap }));

            Assert.False(puzzle.Move(SokobanDir.Up));

            Assert.Equal(new GridPos(1, 1), puzzle.Player);
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.Equal(SokobanEventType.MoveRejected, puzzle.Events[^1].Type);
        }

        [Fact]
        public void 推双箱被拒绝()
        {
            var puzzle = new SokobanPuzzle(new SokobanRules(new[] { TwoBoxesMap }));

            Assert.False(puzzle.Move(SokobanDir.Right));

            Assert.Equal(new GridPos(1, 2), puzzle.Player);
            Assert.Contains(new GridPos(2, 2), puzzle.Boxes);
            Assert.Contains(new GridPos(3, 2), puzzle.Boxes);
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
        }

        [Fact]
        public void 箱子前方是墙时推动被拒绝()
        {
            var puzzle = new SokobanPuzzle(new SokobanRules(new[] { BoxAgainstWallMap }));

            Assert.False(puzzle.Move(SokobanDir.Right));

            Assert.Equal(new GridPos(2, 1), puzzle.Player);
            Assert.Contains(new GridPos(3, 1), puzzle.Boxes);
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
        }

        [Fact]
        public void 玩家可以走上目标点()
        {
            var puzzle = new SokobanPuzzle(new SokobanRules(new[] { WalkOnGoalMap }));

            Assert.True(puzzle.Move(SokobanDir.Right));

            Assert.Equal(new GridPos(2, 1), puzzle.Player);
            Assert.True(puzzle.CurrentLevel.IsGoal(puzzle.Player));
            Assert.Equal(1, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.False(puzzle.IsSolved);
            Assert.Contains(puzzle.Events, e => e.Type == SokobanEventType.Moved);
        }

        [Fact]
        public void 全部箱子就位即过关并拒绝继续移动()
        {
            var puzzle = new SokobanPuzzle();

            Assert.True(puzzle.Move(SokobanDir.Right));
            Assert.True(puzzle.IsSolved);
            Assert.Contains(puzzle.Events, e => e.Type == SokobanEventType.LevelSolved);

            int moveCount = puzzle.MoveCount;
            Assert.False(puzzle.Move(SokobanDir.Up));
            Assert.Equal(moveCount, puzzle.MoveCount);
        }

        [Fact]
        public void 撤销恢复步数推动数与箱子位置()
        {
            var puzzle = new SokobanPuzzle();

            // 上、右两次普通移动，再向下把箱子推离初始位。
            Assert.True(puzzle.Move(SokobanDir.Up));
            Assert.True(puzzle.Move(SokobanDir.Right));
            Assert.True(puzzle.Move(SokobanDir.Down));
            Assert.Equal(3, puzzle.MoveCount);
            Assert.Equal(1, puzzle.PushCount);
            Assert.Contains(new GridPos(3, 1), puzzle.Boxes);

            Assert.True(puzzle.Undo());
            Assert.Equal(2, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.Equal(new GridPos(3, 3), puzzle.Player);
            Assert.Contains(new GridPos(3, 2), puzzle.Boxes);

            Assert.True(puzzle.Undo());
            Assert.Equal(1, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.Equal(new GridPos(2, 3), puzzle.Player);

            Assert.True(puzzle.Undo());
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.Equal(new GridPos(2, 2), puzzle.Player);
            Assert.Contains(new GridPos(3, 2), puzzle.Boxes);
            Assert.Contains(puzzle.Events, e => e.Type == SokobanEventType.Undone);
        }

        [Fact]
        public void 无步可撤时撤销返回false()
        {
            var puzzle = new SokobanPuzzle();

            Assert.False(puzzle.Undo());

            Assert.True(puzzle.Move(SokobanDir.Up));
            Assert.True(puzzle.Undo());
            Assert.False(puzzle.Undo());
        }

        [Fact]
        public void 重置当前关回到初始状态并清空撤销栈()
        {
            var puzzle = new SokobanPuzzle();

            Assert.True(puzzle.Move(SokobanDir.Up));
            Assert.True(puzzle.Move(SokobanDir.Right));
            puzzle.Reset();

            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.Equal(new GridPos(2, 2), puzzle.Player);
            Assert.Contains(new GridPos(3, 2), puzzle.Boxes);
            Assert.False(puzzle.IsSolved);
            Assert.False(puzzle.Undo());
            Assert.Contains(puzzle.Events, e => e.Type == SokobanEventType.Reset);
        }

        [Fact]
        public void 过关后切换下一关并重置计数()
        {
            var puzzle = new SokobanPuzzle();

            Assert.False(puzzle.NextLevel());

            Assert.True(puzzle.Move(SokobanDir.Right));
            Assert.True(puzzle.NextLevel());

            Assert.Equal(1, puzzle.LevelIndex);
            Assert.Equal(0, puzzle.MoveCount);
            Assert.Equal(0, puzzle.PushCount);
            Assert.False(puzzle.IsSolved);
            Assert.Equal(new GridPos(3, 3), puzzle.Player);
            Assert.Contains(new GridPos(4, 3), puzzle.Boxes);
            Assert.Equal(SokobanEventType.LevelChanged, puzzle.Events[^1].Type);

            // 第二关解法：绕到箱子上方连推两格向下入目标。
            Assert.True(puzzle.Move(SokobanDir.Up));
            Assert.True(puzzle.Move(SokobanDir.Right));
            Assert.True(puzzle.Move(SokobanDir.Down));
            Assert.True(puzzle.Move(SokobanDir.Down));
            Assert.True(puzzle.IsSolved);
            Assert.True(puzzle.NextLevel());
            Assert.Equal(2, puzzle.LevelIndex);
        }

        [Fact]
        public void 最后一关通关后没有下一关()
        {
            var puzzle = new SokobanPuzzle(SokobanRules.Default, 4);

            Assert.False(puzzle.HasNextLevel);
            Assert.False(puzzle.NextLevel());

            // 第五关解法：从左列绕到箱子上方，三个箱子依次向下推入一排目标。
            var solution = new[] { SokobanDir.Left, SokobanDir.Left, SokobanDir.Left, SokobanDir.Up,
                SokobanDir.Up, SokobanDir.Up, SokobanDir.Right, SokobanDir.Right, SokobanDir.Down,
                SokobanDir.Up, SokobanDir.Right, SokobanDir.Down, SokobanDir.Up, SokobanDir.Right, SokobanDir.Down };
            foreach (var dir in solution) Assert.True(puzzle.Move(dir), $"第五关解法在 {dir} 处失败。");

            Assert.True(puzzle.IsSolved);
            Assert.True(puzzle.IsAllCompleted);
            Assert.False(puzzle.NextLevel());
        }

        [Fact]
        public void 关卡序号越界抛出异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SokobanPuzzle(SokobanRules.Default, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SokobanPuzzle(SokobanRules.Default, -1));
        }

        [Fact]
        public void 五个内置关卡结构完整且宽度一致()
        {
            var rules = SokobanRules.Default;

            Assert.Equal(5, rules.LevelCount);
            foreach (var level in rules.Levels)
            {
                Assert.False(string.IsNullOrWhiteSpace(level.Name));
                Assert.True(level.Width >= 5);
                Assert.True(level.Height >= 5);
                Assert.True(level.Goals.Count >= 1);
                Assert.Equal(level.Goals.Count, level.InitialBoxes.Count);
                // 行宽一致性由关卡解析强制，这里再确认玩家起点在界内且不是墙。
                Assert.False(level.IsWall(level.PlayerStart));
            }
        }

        [Fact]
        public void 五个内置关卡都能在限定步数内解出()
        {
            foreach (var level in SokobanRules.Default.Levels)
            {
                int minMoves = SolveMinMoves(level, maxMoves: 64);
                Assert.True(minMoves >= 0, $"关卡 {level.Name} 在 64 步内无解。");
                Assert.InRange(minMoves, 1, 64);
            }
        }

        [Fact]
        public void 相同操作序列两次模拟完全一致()
        {
            static int Simulate()
            {
                var puzzle = new SokobanPuzzle();
                var dirs = new[] { SokobanDir.Up, SokobanDir.Right, SokobanDir.Down, SokobanDir.Left,
                    SokobanDir.Right, SokobanDir.Right, SokobanDir.Up, SokobanDir.Left, SokobanDir.Down };
                foreach (var dir in dirs) puzzle.Move(dir);
                return HashCode.Combine(puzzle.MoveCount, puzzle.PushCount, puzzle.Player,
                    string.Join(';', puzzle.Boxes), puzzle.Events.Count);
            }

            Assert.Equal(Simulate(), Simulate());
        }

        [Fact]
        public void 关卡解析非法输入抛出异常()
        {
            // 无玩家。
            Assert.Throws<ArgumentException>(() => new SokobanLevel("无玩家", new[] { "#####", "# $.#", "#####" }));
            // 箱子数与目标数不相等。
            Assert.Throws<ArgumentException>(() => new SokobanLevel("数量失衡", new[] { "######", "#@$..#", "######" }));
            // 行宽不一致。
            Assert.Throws<ArgumentException>(() => new SokobanLevel("宽度不齐", new[] { "#####", "#@$.#", "####" }));
            // 非法字符。
            Assert.Throws<ArgumentException>(() => new SokobanLevel("非法字符", new[] { "#####", "#@x.#", "#####" }));
            // 空关卡。
            Assert.Throws<ArgumentException>(() => new SokobanLevel("空关卡", Array.Empty<string>()));
        }

        [Fact]
        public void 星号与加号解析为箱子在目标上与玩家在目标上()
        {
            var level = new SokobanLevel("符号解析", new[] { "#####", "#+* #", "# $ #", "#####" });

            Assert.Equal(new GridPos(1, 2), level.PlayerStart);
            Assert.Equal(2, level.InitialBoxes.Count);
            Assert.Contains(new GridPos(2, 2), level.InitialBoxes);
            Assert.Contains(new GridPos(2, 1), level.InitialBoxes);
            // '+' 与 '*' 各自隐含一个目标点。
            Assert.True(level.IsGoal(new GridPos(1, 2)));
            Assert.True(level.IsGoal(new GridPos(2, 2)));
            Assert.Equal(2, level.Goals.Count);
        }

        [Fact]
        public void 连续被拒的事件日志封顶裁剪()
        {
            var puzzle = new SokobanPuzzle(new SokobanRules(new[] { OnePushMap }));

            for (int i = 0; i < 300; i++) puzzle.Move(SokobanDir.Left);

            Assert.Equal(128, puzzle.Events.Count);
        }

        /// <summary>宽度优先搜索求关卡最少步数解；超过上限或无解返回 -1。</summary>
        private static int SolveMinMoves(SokobanLevel level, int maxMoves)
        {
            var startBoxes = level.InitialBoxes.OrderBy(b => b.X).ThenBy(b => b.Y).ToList();
            if (AllOnGoals(level, startBoxes)) return 0;

            var visited = new HashSet<string> { StateKey(level.PlayerStart, startBoxes) };
            var queue = new Queue<(GridPos Player, List<GridPos> Boxes, int Depth)>();
            queue.Enqueue((level.PlayerStart, startBoxes, 0));
            var dirs = new[] { SokobanDir.Up, SokobanDir.Down, SokobanDir.Left, SokobanDir.Right };

            while (queue.Count > 0)
            {
                var (player, boxes, depth) = queue.Dequeue();
                if (depth >= maxMoves) continue;

                foreach (var dir in dirs)
                {
                    var delta = dir.ToDelta();
                    var target = player + delta;
                    if (level.IsWall(target)) continue;

                    var nextBoxes = boxes;
                    if (boxes.Contains(target))
                    {
                        var beyond = target + delta;
                        if (level.IsWall(beyond) || boxes.Contains(beyond)) continue;
                        nextBoxes = boxes.ToList();
                        nextBoxes[nextBoxes.IndexOf(target)] = beyond;
                        nextBoxes.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
                    }

                    if (AllOnGoals(level, nextBoxes)) return depth + 1;
                    if (visited.Add(StateKey(target, nextBoxes))) queue.Enqueue((target, nextBoxes, depth + 1));
                }
            }

            return -1;
        }

        private static bool AllOnGoals(SokobanLevel level, List<GridPos> boxes)
        {
            foreach (var box in boxes)
            {
                if (!level.IsGoal(box)) return false;
            }

            return true;
        }

        private static string StateKey(GridPos player, List<GridPos> boxes) =>
            $"{player.X},{player.Y}|{string.Join(';', boxes.Select(b => $"{b.X}:{b.Y}"))}";
    }
}
