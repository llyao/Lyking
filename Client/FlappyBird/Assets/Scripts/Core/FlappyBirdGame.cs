using System;
using System.Collections.Generic;

namespace Lyking.FlappyBird.Core
{
    /// <summary>
    /// Flappy 核心棋局：确定性连续物理模拟，无 Unity 依赖、无输入轮询。
    /// 小鸟水平位置固定，只有竖直速度；调用方按固定子步长调用 <see cref="Advance"/>：
    /// 扇翅把竖直速度置顶为上升速度（不叠加），重力持续加速并被下落上限夹住；
    /// 触地或小鸟矩形与管道矩形重叠即结束，顶棚不死但上升速度清零。
    /// 管道缺口中心由注入种子的 <see cref="Random"/> 生成，随世界左移并按固定间距在右侧补充。
    /// </summary>
    public sealed class FlappyBirdGame
    {
        private const int MaxEventLogs = 128;

        private readonly FlappyRules _rules;
        private readonly List<PipePair> _pipes = new List<PipePair>();
        private readonly List<FlappyEventLog> _events = new List<FlappyEventLog>();
        private Random _random;
        private float _distanceToNextSpawn;

        public FlappyBirdGame(FlappyRules? rules = null, int seed = 0)
        {
            _rules = rules ?? FlappyRules.Default;
            _random = new Random(seed);
            ResetState();
            Log(FlappyEventType.Spawned);
        }

        public FlappyRules Rules => _rules;

        public float FieldWidth => _rules.FieldWidth;

        public float FieldHeight => _rules.FieldHeight;

        public FlappyPhase Phase { get; private set; }

        public bool IsRunning => Phase == FlappyPhase.Running;

        public bool IsFinished => Phase == FlappyPhase.GameOver;

        /// <summary>小鸟中心位置（X 固定，Y 随竖直速度变化）。</summary>
        public Vec2 Position { get; private set; }

        /// <summary>小鸟速度（X 恒为 0，Y 为竖直速度）。</summary>
        public Vec2 Velocity { get; private set; }

        /// <summary>已穿过的管道数，即当前分数。</summary>
        public int Score { get; private set; }

        /// <summary>已完成的物理子步数（仅飞行阶段累计）。</summary>
        public int StepIndex { get; private set; }

        public IReadOnlyList<PipePair> Pipes => _pipes;

        public IReadOnlyList<FlappyEventLog> Events => _events;

        /// <summary>重开一局：清空全部状态并以新种子重建随机序列，等价于重新构造核心。</summary>
        public void Reset(int seed)
        {
            _random = new Random(seed);
            ResetState();
            Log(FlappyEventType.Reset);
        }

        /// <summary>
        /// 推进一个物理子步。<paramref name="flap"/> 表示本子步是否点按扇翅；
        /// 就绪阶段收到点按即起飞。返回 false 表示棋局已结束。
        /// </summary>
        public bool Advance(float deltaTime, bool flap)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "推进时长不能为负。");
            if (IsFinished) return false;

            if (flap)
            {
                if (Phase == FlappyPhase.Ready) Phase = FlappyPhase.Running;
                Velocity = Velocity.WithY(_rules.FlapSpeed);
                Log(FlappyEventType.Flapped);
            }

            if (Phase == FlappyPhase.Ready) return true;

            StepIndex++;
            ScrollPipes(deltaTime);
            Integrate(deltaTime);
            if (CheckPipes() || CheckGround()) return false;
            UpdateScore();
            return true;
        }

        private void ResetState()
        {
            Phase = FlappyPhase.Ready;
            Position = new Vec2(_rules.BirdX, _rules.HoverHeight);
            Velocity = new Vec2(0f, 0f);
            Score = 0;
            StepIndex = 0;
            _distanceToNextSpawn = _rules.FirstPipeDistance;
            _pipes.Clear();
            _events.Clear();
        }

        /// <summary>先左移并移除越界管道，再按累计滚动距离在场地右侧补充新管道。</summary>
        private void ScrollPipes(float deltaTime)
        {
            float move = _rules.PipeSpeed * deltaTime;
            for (int i = _pipes.Count - 1; i >= 0; i--)
            {
                var pipe = _pipes[i];
                var center = pipe.Center.WithX(pipe.Center.X - move);
                _pipes[i] = new PipePair(center, pipe.Scored);
                if (center.X + _rules.PipeWidth * 0.5f < 0f) _pipes.RemoveAt(i);
            }

            _distanceToNextSpawn -= move;
            while (_distanceToNextSpawn <= 0f)
            {
                SpawnPipe();
                _distanceToNextSpawn += _rules.PipeSpacing;
            }
        }

        private void SpawnPipe()
        {
            float gapCenter = _rules.GapCenterMin + (float)_random.NextDouble() * _rules.GapCenterRange;
            _pipes.Add(new PipePair(new Vec2(_rules.FieldWidth + _rules.PipeWidth * 0.5f, gapCenter), false));
        }

        /// <summary>重力积分与顶棚处理：下落速度夹在上限内，顶棚只清零上升速度不判死。</summary>
        private void Integrate(float deltaTime)
        {
            float vy = Velocity.Y - _rules.Gravity * deltaTime;
            if (vy < -_rules.MaxFallSpeed) vy = -_rules.MaxFallSpeed;
            float y = Position.Y + vy * deltaTime;
            if (y + _rules.BirdHalfHeight >= _rules.FieldHeight)
            {
                y = _rules.FieldHeight - _rules.BirdHalfHeight;
                if (vy > 0f) vy = 0f;
            }

            Velocity = new Vec2(0f, vy);
            Position = Position.WithY(y);
        }

        /// <summary>小鸟矩形与管道矩形严格重叠（缺口边缘贴住不判死）即结束。</summary>
        private bool CheckPipes()
        {
            float birdLeft = Position.X - _rules.BirdHalfWidth;
            float birdRight = Position.X + _rules.BirdHalfWidth;
            float birdBottom = Position.Y - _rules.BirdHalfHeight;
            float birdTop = Position.Y + _rules.BirdHalfHeight;
            float gapHalf = _rules.GapHeight * 0.5f;
            foreach (var pipe in _pipes)
            {
                float left = pipe.Center.X - _rules.PipeWidth * 0.5f;
                float right = pipe.Center.X + _rules.PipeWidth * 0.5f;
                if (birdLeft >= right || birdRight <= left) continue;
                if (birdTop <= pipe.Center.Y + gapHalf && birdBottom >= pipe.Center.Y - gapHalf) continue;
                Die(FlappyEventType.HitPipe);
                return true;
            }

            return false;
        }

        /// <summary>小鸟触地即结束，位置停在地面之上。</summary>
        private bool CheckGround()
        {
            if (Position.Y - _rules.BirdHalfHeight > 0f) return false;
            Position = Position.WithY(_rules.BirdHalfHeight);
            Die(FlappyEventType.HitGround);
            return true;
        }

        /// <summary>小鸟越过管道缺口中心横坐标即得分，每对管道只计一次。</summary>
        private void UpdateScore()
        {
            for (int i = 0; i < _pipes.Count; i++)
            {
                var pipe = _pipes[i];
                if (pipe.Scored || pipe.Center.X >= _rules.BirdX) continue;
                _pipes[i] = new PipePair(pipe.Center, true);
                Score++;
                Log(FlappyEventType.Scored);
            }
        }

        private void Die(FlappyEventType type)
        {
            Phase = FlappyPhase.GameOver;
            Log(type);
        }

        private void Log(FlappyEventType type)
        {
            _events.Add(new FlappyEventLog(StepIndex, type));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
