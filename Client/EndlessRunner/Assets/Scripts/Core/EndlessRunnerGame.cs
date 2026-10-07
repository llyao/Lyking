using System;
using System.Collections.Generic;

namespace Lyking.EndlessRunner.Core
{
    /// <summary>
    /// 无尽跑酷核心棋局：确定性连续模拟，无 Unity 依赖、无输入轮询。
    /// 玩家横向位置固定，世界以随距离提升的速度向左滚动；调用方按固定子步长调用
    /// <see cref="Advance"/>：地面跳 + 空中二段跳（跳段落地重置），快落把重力按倍率放大并被下落上限夹住；
    /// 障碍由注入种子的 <see cref="Random"/> 在前方生成线放置，高矮与间距取自固定顺序的随机序列，
    /// 玩家矩形与障碍矩形重叠即结束，越过障碍记入事件，地面不是致死物。
    /// </summary>
    public sealed class EndlessRunnerGame
    {
        private const int MaxEventLogs = 128;

        private readonly RunnerRules _rules;
        private readonly List<RunnerObstacle> _obstacles = new List<RunnerObstacle>();
        private readonly List<RunnerEventLog> _events = new List<RunnerEventLog>();
        private Random _random;
        private float _distanceToNextSpawn;

        public EndlessRunnerGame(RunnerRules? rules = null, int seed = 0)
        {
            _rules = rules ?? RunnerRules.Default;
            _random = new Random(seed);
            ResetState();
            Log(RunnerEventType.Spawned);
        }

        public RunnerRules Rules => _rules;

        public float FieldWidth => _rules.FieldWidth;

        public float FieldHeight => _rules.FieldHeight;

        /// <summary>玩家中心位置（X 固定为 <see cref="RunnerRules.PlayerX"/>，Y 随竖直速度变化）。</summary>
        public Vec2 PlayerPosition { get; private set; }

        /// <summary>玩家竖直速度（上升为正、下落为负，被下落上限夹住）。</summary>
        public float VelocityY { get; private set; }

        public bool IsGrounded { get; private set; }

        /// <summary>本次腾空已消耗的跳段数，落地时清零。</summary>
        public int JumpsUsed { get; private set; }

        public bool IsFastFalling { get; private set; }

        /// <summary>世界已向左滚动的累计距离（米）。</summary>
        public float Distance { get; private set; }

        /// <summary>当前滚动速度（米/秒），随距离线性提升并有上限。</summary>
        public float Speed => _rules.SpeedFor(Distance);

        /// <summary>当前分数：滚动距离乘每米得分，向下取整。</summary>
        public int Score { get; private set; }

        public bool IsGameOver { get; private set; }

        public bool IsFinished => IsGameOver;

        /// <summary>已完成的物理子步数。</summary>
        public int StepIndex { get; private set; }

        public IReadOnlyList<RunnerObstacle> Obstacles => _obstacles;

        public IReadOnlyList<RunnerEventLog> Events => _events;

        /// <summary>重开一局：清空全部状态并以新种子重建随机序列，等价于重新构造核心。</summary>
        public void Reset(int seed)
        {
            _random = new Random(seed);
            ResetState();
            Log(RunnerEventType.Reset);
        }

        /// <summary>
        /// 推进一个物理子步：<paramref name="input.JumpPressed"/> 的按下沿在本子步生效，
        /// <paramref name="input.FastFallHeld"/> 持续生效。返回 false 表示棋局已结束。
        /// </summary>
        public bool Advance(float deltaTime, in RunnerInput input)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时长不能为负。");
            if (IsFinished) return false;

            StepIndex++;
            HandleJump(input);
            HandleFastFall(input);
            Integrate(deltaTime);
            ScrollWorld(deltaTime);
            if (CheckCollision()) return false;
            UpdateCleared();
            UpdateScore();
            return true;
        }

        private void ResetState()
        {
            PlayerPosition = new Vec2(_rules.PlayerX, _rules.GroundHeight + _rules.PlayerHalfHeight);
            VelocityY = 0f;
            IsGrounded = true;
            JumpsUsed = 0;
            IsFastFalling = false;
            Distance = 0f;
            Score = 0;
            StepIndex = 0;
            IsGameOver = false;
            _distanceToNextSpawn = _rules.FirstObstacleDistance;
            _obstacles.Clear();
            _events.Clear();
        }

        /// <summary>地面跳消耗首段、空中跳消耗二段；跳段用尽后按下沿被静默忽略。</summary>
        private void HandleJump(in RunnerInput input)
        {
            if (!input.JumpPressed) return;
            if (IsGrounded)
            {
                IsGrounded = false;
                JumpsUsed = 1;
                VelocityY = _rules.JumpSpeed;
                Log(RunnerEventType.Jumped);
            }
            else if (JumpsUsed < _rules.MaxJumps)
            {
                JumpsUsed++;
                VelocityY = _rules.DoubleJumpSpeed;
                Log(RunnerEventType.DoubleJumped);
            }
        }

        /// <summary>快落只在空中生效；从非快落转入快落的子步记录一次事件。</summary>
        private void HandleFastFall(in RunnerInput input)
        {
            bool fastFalling = input.FastFallHeld && !IsGrounded;
            if (fastFalling && !IsFastFalling) Log(RunnerEventType.FastFallStarted);
            IsFastFalling = fastFalling;
        }

        /// <summary>重力积分与落地处理：下落速度夹在上限内，触地即站稳（速度清零、跳段重置、快落结束）。</summary>
        private void Integrate(float deltaTime)
        {
            bool wasGrounded = IsGrounded;
            float gravity = _rules.Gravity * (IsFastFalling ? _rules.FastFallMultiplier : 1f);
            float vy = VelocityY - gravity * deltaTime;
            if (vy < -_rules.MaxFallSpeed) vy = -_rules.MaxFallSpeed;
            float y = PlayerPosition.Y + vy * deltaTime;
            if (y - _rules.PlayerHalfHeight <= _rules.GroundHeight)
            {
                y = _rules.GroundHeight + _rules.PlayerHalfHeight;
                vy = 0f;
                if (!wasGrounded) Log(RunnerEventType.Landed);
                IsGrounded = true;
                JumpsUsed = 0;
                IsFastFalling = false;
            }
            else
            {
                IsGrounded = false;
            }

            VelocityY = vy;
            PlayerPosition = PlayerPosition.WithY(y);
        }

        /// <summary>先按当前速度左移世界并移除越出左缘的障碍，再按累计滚动距离在生成线补充新障碍。</summary>
        private void ScrollWorld(float deltaTime)
        {
            float move = Speed * deltaTime;
            Distance += move;
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var obstacle = _obstacles[i];
                var center = obstacle.Center.WithX(obstacle.Center.X - move);
                _obstacles[i] = new RunnerObstacle(center, obstacle.IsTall, obstacle.Passed);
                if (center.X + HalfWidth(obstacle) < 0f) _obstacles.RemoveAt(i);
            }

            _distanceToNextSpawn -= move;
            while (_distanceToNextSpawn <= 0f)
            {
                SpawnObstacle();
                _distanceToNextSpawn += NextSpacing();
            }
        }

        private float HalfWidth(RunnerObstacle obstacle) =>
            (obstacle.IsTall ? _rules.TallObstacleWidth : _rules.ShortObstacleWidth) * 0.5f;

        private void SpawnObstacle()
        {
            // 先掷高矮、再掷与下一个障碍的间距，随机序列顺序固定保证可复现。
            bool tall = _random.NextDouble() < _rules.TallObstacleChance;
            float height = tall ? _rules.TallObstacleHeight : _rules.ShortObstacleHeight;
            var center = new Vec2(_rules.SpawnX, _rules.GroundHeight + height * 0.5f);
            _obstacles.Add(new RunnerObstacle(center, tall, false));
        }

        private float NextSpacing()
        {
            double sample = _random.NextDouble();
            return _rules.MinSpacing + (float)sample * _rules.SpacingRange;
        }

        /// <summary>玩家矩形与障碍矩形严格重叠（边缘贴住不判死）即结束。</summary>
        private bool CheckCollision()
        {
            float playerLeft = PlayerPosition.X - _rules.PlayerHalfWidth;
            float playerRight = PlayerPosition.X + _rules.PlayerHalfWidth;
            float playerBottom = PlayerPosition.Y - _rules.PlayerHalfHeight;
            float playerTop = PlayerPosition.Y + _rules.PlayerHalfHeight;
            foreach (var obstacle in _obstacles)
            {
                float halfWidth = HalfWidth(obstacle);
                float halfHeight = (obstacle.IsTall ? _rules.TallObstacleHeight : _rules.ShortObstacleHeight) * 0.5f;
                float left = obstacle.Center.X - halfWidth;
                float right = obstacle.Center.X + halfWidth;
                if (playerLeft >= right || playerRight <= left) continue;
                if (playerBottom >= obstacle.Center.Y + halfHeight || playerTop <= obstacle.Center.Y - halfHeight) continue;
                IsGameOver = true;
                Log(RunnerEventType.HitObstacle);
                return true;
            }

            return false;
        }

        /// <summary>障碍右缘越到玩家左缘身后记为越过，每个障碍只记一次。</summary>
        private void UpdateCleared()
        {
            float playerLeft = PlayerPosition.X - _rules.PlayerHalfWidth;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obstacle = _obstacles[i];
                if (obstacle.Passed) continue;
                if (obstacle.Center.X + HalfWidth(obstacle) >= playerLeft) continue;
                _obstacles[i] = new RunnerObstacle(obstacle.Center, obstacle.IsTall, true);
                Log(RunnerEventType.ClearedObstacle);
            }
        }

        private void UpdateScore()
        {
            Score = (int)(Distance * _rules.ScorePerMeter);
        }

        private void Log(RunnerEventType type)
        {
            _events.Add(new RunnerEventLog(StepIndex, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
