using System;
using System.Linq;
using Lyking.Snake.Core;
using Xunit;

namespace Lyking.Snake.Core.Tests
{
    /// <summary>贪吃蛇核心规则测试：确定性、输入队列、碰撞、进食、加速与胜利。</summary>
    public class SnakeBoardTests
    {
        private static SnakeRules SmallRules(int width = 8, int height = 6) =>
            new SnakeRules(width, height, initialLength: 2, baseStepSeconds: 0.2f, minStepSeconds: 0.1f,
                stepShrinkPerFood: 0.01f, scorePerFood: 10);

        [Fact]
        public void 构造后初始状态合法()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 42);

            Assert.Equal(SnakeRules.Default.InitialLength, board.Length);
            Assert.Equal(SnakeDir.Right, board.CurrentDirection);
            Assert.Equal(0, board.Score);
            Assert.Equal(0, board.StepIndex);
            Assert.False(board.IsFinished);
            Assert.InRange(board.Head.X, 0, board.Width - 1);
            Assert.InRange(board.Head.Y, 0, board.Height - 1);
            Assert.NotEqual(board.Head, board.Food);
        }

        [Fact]
        public void 直行推进时蛇身跟随且长度不变()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 42);
            var bodyBefore = board.Body.ToArray();

            Assert.True(board.Tick());

            Assert.Equal(bodyBefore.Length, board.Length);
            Assert.Equal(bodyBefore[0].X + 1, board.Head.X);
            Assert.Equal(bodyBefore[0].Y, board.Head.Y);
            // 尾巴前进一格：倒数第二节与原倒数第一节重合
            Assert.Equal(bodyBefore[^2], board.Body[^1]);
        }

        [Fact]
        public void 反向与相同方向入队被忽略()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 42);

            Assert.False(board.EnqueueDirection(SnakeDir.Left));
            Assert.False(board.EnqueueDirection(SnakeDir.Right));
            Assert.True(board.EnqueueDirection(SnakeDir.Up));
            Assert.False(board.EnqueueDirection(SnakeDir.Down));

            board.Tick();
            Assert.Equal(SnakeDir.Up, board.CurrentDirection);
        }

        [Fact]
        public void 两步内的转向序列按队列依次生效()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 42);

            Assert.True(board.EnqueueDirection(SnakeDir.Up));
            Assert.True(board.EnqueueDirection(SnakeDir.Left));

            board.Tick();
            Assert.Equal(SnakeDir.Up, board.CurrentDirection);
            board.Tick();
            Assert.Equal(SnakeDir.Left, board.CurrentDirection);
            // 第三步继续向左：队列为空后保持当前方向
            board.Tick();
            Assert.Equal(SnakeDir.Left, board.CurrentDirection);
        }

        [Fact]
        public void 超过两条的排队转向被拒绝()
        {
            var board = new SnakeBoard(SnakeRules.Default, seed: 42);

            Assert.True(board.EnqueueDirection(SnakeDir.Up));
            Assert.True(board.EnqueueDirection(SnakeDir.Left));
            Assert.False(board.EnqueueDirection(SnakeDir.Down));
        }

        [Fact]
        public void 撞墙结束棋局且蛇身保持完整()
        {
            var rules = SmallRules(width: 4, height: 3);
            var board = new SnakeBoard(rules, seed: 1);
            int lengthBefore = board.Length;

            for (int i = 0; i < 20 && !board.IsFinished; i++) board.Tick();

            Assert.True(board.IsGameOver);
            Assert.False(board.IsWon);
            Assert.Equal(lengthBefore, board.Length);
            Assert.Contains(SnakeEventType.WallCollision, board.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 转向撞上自己身体即结束()
        {
            // 初始长 5 蛇横排向右：Up、Left、Down 三步 U 形折返，头必然落在自己的身体中段。
            var rules = new SnakeRules(width: 9, height: 3, initialLength: 5, baseStepSeconds: 0.2f,
                minStepSeconds: 0.1f, stepShrinkPerFood: 0.01f, scorePerFood: 10);
            var board = new SnakeBoard(rules, seed: 3);

            board.EnqueueDirection(SnakeDir.Up);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Left);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Down);
            board.Tick();

            Assert.True(board.IsGameOver);
            Assert.Equal(SnakeEventType.SelfCollision, board.Events[^1].Type);
        }

        [Fact]
        public void 尾巴即将腾出的格子可以直接进入()
        {
            // 直线蛇长 2 掉头追尾：头进入原尾格前尾巴已移走，合法
            var rules = SmallRules(width: 6, height: 6);
            var board = new SnakeBoard(rules, seed: 5);
            board.EnqueueDirection(SnakeDir.Up);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Left);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Down);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Right);
            board.Tick();
            board.EnqueueDirection(SnakeDir.Up);
            board.Tick();

            // 2 格蛇在 6x6 内完成一个回环，不应结束
            Assert.False(board.IsFinished);
        }

        [Fact]
        public void 吃到食物后加分加长并更换食物()
        {
            // 长 2 的蛇绕初始位置的 2x2 方块循环；选一个食物恰好落在循环上的种子，
            // 使进食在少数几步内确定发生。
            var loop = new HashSet<GridPos> { new GridPos(5, 3), new GridPos(5, 4), new GridPos(4, 4), new GridPos(4, 3) };
            SnakeBoard? board = null;
            for (int seed = 1; seed < 200 && board is null; seed++)
            {
                var candidate = new SnakeBoard(SmallRules(), seed);
                if (loop.Contains(candidate.Food)) board = candidate;
            }

            Assert.NotNull(board);
            int scoreBefore = board!.Score;
            int lengthBefore = board.Length;
            var foodBefore = board.Food;

            var turns = new[] { SnakeDir.Up, SnakeDir.Left, SnakeDir.Down, SnakeDir.Right };
            bool ate = false;
            for (int i = 0; i < 8 && !ate; i++)
            {
                board.EnqueueDirection(turns[i % turns.Length]);
                ate = board.Tick() && board.Score > scoreBefore;
            }

            Assert.True(ate, "绕行 8 步内应吃到圈上的食物。");
            Assert.Equal(scoreBefore + 10, board.Score);
            Assert.Equal(lengthBefore + 1, board.Length);
            Assert.Equal(1, board.FoodEaten);
            Assert.NotEqual(foodBefore, board.Food);
            Assert.Contains(SnakeEventType.AteFood, board.Events.Select(e => e.Type).ToList());
        }

        [Fact]
        public void 进食后步进间隔缩短并被下限夹住()
        {
            var rules = SmallRules();
            Assert.Equal(0.2f, rules.StepSecondsFor(0));
            Assert.Equal(0.15f, rules.StepSecondsFor(5));
            Assert.Equal(0.1f, rules.StepSecondsFor(50));
            Assert.Equal(0.1f, rules.StepSecondsFor(1000));
        }

        [Fact]
        public void 相同种子两次模拟完全一致()
        {
            static int Simulate(int seed)
            {
                var board = new SnakeBoard(SnakeRules.Default, seed);
                var turns = new[] { SnakeDir.Up, SnakeDir.Left, SnakeDir.Down, SnakeDir.Right };
                for (int i = 0; i < 200 && !board.IsFinished; i++)
                {
                    board.EnqueueDirection(turns[i % turns.Length]);
                    board.Tick();
                }

                return HashCode.Combine(board.Score, board.Length, board.StepIndex,
                    string.Join(';', board.Body), board.Food);
            }

            Assert.Equal(Simulate(777), Simulate(777));
            Assert.NotEqual(Simulate(777), Simulate(778));
        }

        [Fact]
        public void 结束后入队与推进均被拒绝()
        {
            var rules = SmallRules(width: 4, height: 3);
            var board = new SnakeBoard(rules, seed: 1);
            for (int i = 0; i < 20 && !board.IsFinished; i++) board.Tick();
            Assert.True(board.IsFinished);

            int stepIndex = board.StepIndex;
            Assert.False(board.EnqueueDirection(SnakeDir.Up));
            Assert.False(board.Tick());
            Assert.Equal(stepIndex, board.StepIndex);
        }
    }
}
