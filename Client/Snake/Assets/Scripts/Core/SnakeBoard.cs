using System;
using System.Collections.Generic;

namespace Lyking.Snake.Core
{
    /// <summary>
    /// 贪吃蛇核心棋局：确定性回合制模拟，无 Unity 依赖、无时间概念。
    /// 蛇身头在索引 0；食物由注入种子的 <see cref="Random"/> 在空格中选取；
    /// 输入队列最多缓存 2 个转向，重复与原地掉头方向被忽略。
    /// 每次调用 <see cref="Tick"/> 前进一格：吃食增长并重新生成食物，撞墙或撞身即结束。
    /// </summary>
    public sealed class SnakeBoard
    {
        private const int MaxEventLogs = 128;
        private const int MaxPendingTurns = 2;

        private readonly SnakeRules _rules;
        private readonly Random _random;
        private readonly List<GridPos> _body = new List<GridPos>();
        private readonly HashSet<GridPos> _occupied = new HashSet<GridPos>();
        private readonly List<SnakeDir> _pending = new List<SnakeDir>();
        private readonly List<SnakeEventLog> _events = new List<SnakeEventLog>();
        private SnakeDir _lastQueued;

        public SnakeBoard(SnakeRules? rules = null, int seed = 0)
        {
            _rules = rules ?? SnakeRules.Default;
            _random = new Random(seed);

            var centerX = _rules.Width / 2;
            var centerY = _rules.Height / 2;
            for (int i = 0; i < _rules.InitialLength; i++)
            {
                // 头在最右端向右行进，身体向左排开，保证初始蛇身完整落在棋盘内。
                var part = new GridPos(centerX + _rules.InitialLength - 1 - i, centerY);
                _body.Add(part);
                _occupied.Add(part);
            }

            CurrentDirection = SnakeDir.Right;
            _lastQueued = SnakeDir.Right;
            Log(SnakeEventType.Spawned);
            SpawnFood();
        }

        public int Width => _rules.Width;

        public int Height => _rules.Height;

        public SnakeDir CurrentDirection { get; private set; }

        public GridPos Head => _body[0];

        public int Length => _body.Count;

        public int Score { get; private set; }

        public int FoodEaten { get; private set; }

        public GridPos Food { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsWon { get; private set; }

        public bool IsFinished => IsGameOver || IsWon;

        /// <summary>已完成的步数。</summary>
        public int StepIndex { get; private set; }

        /// <summary>当前步进间隔（秒）：随进食数缩短，由驱动方按此节奏调用 <see cref="Tick"/>。</summary>
        public float StepSeconds => _rules.StepSecondsFor(FoodEaten);

        public IReadOnlyList<GridPos> Body => _body;

        public IReadOnlyList<SnakeEventLog> Events => _events;

        /// <summary>入队一个转向。与队尾方向相同或相反、队列已满或棋局已结束时被忽略，返回是否成功。</summary>
        public bool EnqueueDirection(SnakeDir dir)
        {
            if (IsFinished || _pending.Count >= MaxPendingTurns) return false;
            if (dir == _lastQueued || dir == _lastQueued.Opposite()) return false;

            _pending.Add(dir);
            _lastQueued = dir;
            return true;
        }

        /// <summary>推进一格。返回 false 表示棋局已结束，蛇身保持死亡瞬间形状。</summary>
        public bool Tick()
        {
            if (IsFinished) return false;

            if (_pending.Count > 0)
            {
                var next = _pending[0];
                _pending.RemoveAt(0);
                if (next != CurrentDirection)
                {
                    CurrentDirection = next;
                    Log(SnakeEventType.Turned);
                }
            }

            var head = Head + CurrentDirection.ToDelta();
            if (!InBounds(head))
            {
                IsGameOver = true;
                Log(SnakeEventType.WallCollision);
                return false;
            }

            bool eats = head == Food;
            int bodyCheckCount = eats ? _body.Count : _body.Count - 1;
            for (int i = 0; i < bodyCheckCount; i++)
            {
                if (_body[i] == head)
                {
                    IsGameOver = true;
                    Log(SnakeEventType.SelfCollision);
                    return false;
                }
            }

            if (!eats)
            {
                var tail = _body[_body.Count - 1];
                _occupied.Remove(tail);
                _body.RemoveAt(_body.Count - 1);
            }

            _body.Insert(0, head);
            _occupied.Add(head);
            StepIndex++;

            if (eats)
            {
                FoodEaten++;
                Score += _rules.ScorePerFood;
                Log(SnakeEventType.AteFood);
                SpawnFood();
            }

            return true;
        }

        private bool InBounds(GridPos pos) => pos.X >= 0 && pos.X < _rules.Width && pos.Y >= 0 && pos.Y < _rules.Height;

        private void SpawnFood()
        {
            var free = new List<GridPos>(_rules.CellCount - _occupied.Count);
            for (int y = 0; y < _rules.Height; y++)
            {
                for (int x = 0; x < _rules.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (!_occupied.Contains(pos)) free.Add(pos);
                }
            }

            if (free.Count == 0)
            {
                Food = new GridPos(-1, -1);
                IsWon = true;
                Log(SnakeEventType.Won);
                return;
            }

            Food = free[_random.Next(free.Count)];
        }

        private void Log(SnakeEventType type)
        {
            _events.Add(new SnakeEventLog(StepIndex, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
