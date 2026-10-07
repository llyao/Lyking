using System;
using System.Collections.Generic;

namespace Lyking.Breakout.Core
{
    /// <summary>
    /// 打砖块核心对局：确定性连续物理模拟，无 Unity 依赖。
    /// 球为圆（圆心 + 半径），与挡板、砖块 AABB 按最近点做碰撞，反射轴取穿透最小轴；
    /// <see cref="Step"/> 内部按最长 1/120 秒的固定子步推进，防止单帧大步长穿墙穿砖。
    /// 随机数只用于从预设偏角中选择发球方向，由注入种子保证同种子完全一致。
    /// </summary>
    public sealed class BreakoutGame
    {
        private const int MaxEventLogs = 128;

        /// <summary>单次物理子步的最长时长（秒），防止球在一子步内穿过薄砖块。</summary>
        private const float MaxSubstepSeconds = 1f / 120f;

        /// <summary>挡板反弹与发球的最大偏角（弧度，相对竖直方向）。</summary>
        private const float MaxBounceAngle = MathF.PI / 3f;

        /// <summary>碰撞后把球推离表面的间隙，避免下一子步重复判定同一表面。</summary>
        private const float BounceEpsilon = 0.001f;

        /// <summary>发球偏角候选（弧度，相对竖直方向），由注入种子确定性选取。</summary>
        private static readonly float[] LaunchAngleChoices = { -0.6f, -0.2f, 0.2f, 0.6f };

        private readonly BreakoutRules _rules;
        private readonly Random _random;
        private readonly List<BreakoutBrick> _bricks = new List<BreakoutBrick>();
        private readonly List<BreakoutEventLog> _events = new List<BreakoutEventLog>();
        private readonly Vec2 _paddleHalfSize;
        private Vec2 _paddleCenter;
        private float _paddleInput;
        private Vec2 _ballCenter;
        private Vec2 _ballVelocity;
        private float _ballSpeed;
        private bool _ballLaunched;
        private int _aliveBricks;

        public BreakoutGame(BreakoutRules? rules = null, int seed = 0)
        {
            _rules = rules ?? BreakoutRules.Default;
            _random = new Random(seed);
            _paddleHalfSize = new Vec2(_rules.PaddleWidth * 0.5f, _rules.PaddleHeight * 0.5f);
            _paddleCenter = new Vec2(_rules.FieldWidth * 0.5f, _rules.PaddleCenterY);
            _ballSpeed = _rules.BallSpeedForLevel(1);
            Lives = _rules.Lives;
            Level = 1;
            BuildBricks();
            ResetBall();
            Log(BreakoutEventType.Spawned, 0);
        }

        public BreakoutRules Rules => _rules;

        public int Score { get; private set; }

        public int Lives { get; private set; }

        public int Level { get; private set; }

        public bool IsGameOver { get; private set; }

        /// <summary>球是否已发射；false 表示球贴在挡板上等待发球。</summary>
        public bool BallLaunched => _ballLaunched;

        /// <summary>当前球速目标模长：恒定不变，随关卡提升。</summary>
        public float BallSpeed => _ballSpeed;

        public Vec2 BallPosition => _ballCenter;

        public Vec2 BallVelocity => _ballVelocity;

        /// <summary>挡板中心位置。</summary>
        public Vec2 PaddlePosition => _paddleCenter;

        public IReadOnlyList<BreakoutBrick> Bricks => _bricks;

        public int AliveBrickCount => _aliveBricks;

        /// <summary>已完成的物理子步数。</summary>
        public int StepIndex { get; private set; }

        public IReadOnlyList<BreakoutEventLog> Events => _events;

        /// <summary>设置挡板移动方向：-1 左移、0 停止、+1 右移，支持区间内的模拟量。</summary>
        public void MovePaddle(float direction) => _paddleInput = Math.Clamp(direction, -1f, 1f);

        /// <summary>按注入种子从预设偏角中发球。对局结束或球已在飞行中时返回 false。</summary>
        public bool Launch()
        {
            if (IsGameOver || _ballLaunched) return false;
            return Launch(LaunchAngleChoices[_random.Next(LaunchAngleChoices.Length)]);
        }

        /// <summary>以指定偏角发球（0 为竖直向上，弧度），越界被夹住，供测试与回放精确控制初速。</summary>
        public bool Launch(float angleRadians)
        {
            if (IsGameOver || _ballLaunched) return false;

            var angle = Math.Clamp(angleRadians, -MaxBounceAngle, MaxBounceAngle);
            _ballVelocity = NormalizeVelocity(new Vec2(MathF.Sin(angle), MathF.Cos(angle)) * _ballSpeed);
            _ballLaunched = true;
            Log(BreakoutEventType.Launched, 0);
            return true;
        }

        /// <summary>推进对局：内部按最长 1/120 秒的固定子步推进物理，任意帧时长都安全。</summary>
        public void Step(float deltaTime)
        {
            if (IsGameOver || deltaTime <= 0f) return;

            var remaining = deltaTime;
            while (remaining > 0f)
            {
                var h = MathF.Min(MaxSubstepSeconds, remaining);
                Advance(h);
                remaining -= h;
            }
        }

        private void Advance(float h)
        {
            StepIndex++;
            MovePaddleBy(h);
            if (!_ballLaunched)
            {
                StickBallToPaddle();
                return;
            }

            _ballCenter += _ballVelocity * h;
            ResolveWalls();
            ResolvePaddle();
            ResolveBricks();
            ResolveBallLost();
        }

        private void MovePaddleBy(float h)
        {
            var half = _paddleHalfSize.X;
            var x = Math.Clamp(_paddleCenter.X + _paddleInput * _rules.PaddleSpeed * h, half, _rules.FieldWidth - half);
            _paddleCenter = new Vec2(x, _rules.PaddleCenterY);
        }

        private void StickBallToPaddle() =>
            _ballCenter = new Vec2(_paddleCenter.X, _rules.PaddleCenterY + _paddleHalfSize.Y + _rules.BallRadius);

        /// <summary>失命或过关后把球收回挡板上方，等待再次发球。</summary>
        private void ResetBall()
        {
            _ballLaunched = false;
            _ballVelocity = Vec2.Zero;
            StickBallToPaddle();
        }

        private void ResolveWalls()
        {
            var r = _rules.BallRadius;
            if (_ballCenter.X < r)
            {
                _ballCenter = _ballCenter.WithX(r);
                if (_ballVelocity.X < 0f) _ballVelocity = _ballVelocity.WithX(-_ballVelocity.X);
                Log(BreakoutEventType.WallBounced, 0);
            }
            else if (_ballCenter.X > _rules.FieldWidth - r)
            {
                _ballCenter = _ballCenter.WithX(_rules.FieldWidth - r);
                if (_ballVelocity.X > 0f) _ballVelocity = _ballVelocity.WithX(-_ballVelocity.X);
                Log(BreakoutEventType.WallBounced, 0);
            }

            if (_ballCenter.Y > _rules.FieldHeight - r)
            {
                _ballCenter = _ballCenter.WithY(_rules.FieldHeight - r);
                if (_ballVelocity.Y > 0f) _ballVelocity = _ballVelocity.WithY(-_ballVelocity.Y);
                Log(BreakoutEventType.WallBounced, 0);
            }
        }

        private void ResolvePaddle()
        {
            if (_ballVelocity.Y >= 0f) return;

            var reachX = _paddleHalfSize.X + _rules.BallRadius;
            var reachY = _paddleHalfSize.Y + _rules.BallRadius;
            var deltaX = _ballCenter.X - _paddleCenter.X;
            var deltaY = _ballCenter.Y - _paddleCenter.Y;
            if (MathF.Abs(deltaX) > reachX || MathF.Abs(deltaY) > reachY) return;

            // 命中位置偏离挡板中心的比例决定反射角：中心竖直上弹，越靠边越斜（最大 60°）。
            var offset = Math.Clamp(deltaX / reachX, -1f, 1f);
            var angle = offset * MaxBounceAngle;
            _ballVelocity = NormalizeVelocity(new Vec2(MathF.Sin(angle), MathF.Cos(angle)) * _ballSpeed);
            _ballCenter = new Vec2(_ballCenter.X, _paddleCenter.Y + _paddleHalfSize.Y + _rules.BallRadius + BounceEpsilon);
            Log(BreakoutEventType.PaddleBounced, 0);
        }

        private void ResolveBricks()
        {
            var radiusSquared = _rules.BallRadius * _rules.BallRadius;
            for (int i = 0; i < _bricks.Count; i++)
            {
                var brick = _bricks[i];
                if (!brick.IsAlive) continue;

                var closest = brick.Bounds.ClosestPoint(_ballCenter);
                var delta = _ballCenter - closest;
                if (delta.LengthSquared > radiusSquared) continue;

                // 反射轴取穿透最小轴：与最近点偏差更大的分量方向穿透更浅，沿该轴推出并反射。
                if (MathF.Abs(delta.X) >= MathF.Abs(delta.Y))
                {
                    var side = delta.X >= 0f ? 1f : -1f;
                    _ballCenter = new Vec2(closest.X + side * (_rules.BallRadius + BounceEpsilon), _ballCenter.Y);
                    if (_ballVelocity.X * side < 0f) _ballVelocity = _ballVelocity.WithX(-_ballVelocity.X);
                }
                else
                {
                    var side = delta.Y >= 0f ? 1f : -1f;
                    _ballCenter = new Vec2(_ballCenter.X, closest.Y + side * (_rules.BallRadius + BounceEpsilon));
                    if (_ballVelocity.Y * side < 0f) _ballVelocity = _ballVelocity.WithY(-_ballVelocity.Y);
                }

                DamageBrick(i);
                return; // 每个子步最多处理一块砖，相邻碰撞留给后续子步
            }
        }

        private void DamageBrick(int index)
        {
            var brick = _bricks[index];
            var durability = brick.Durability - 1;
            _bricks[index] = brick.WithDurability(durability);
            if (durability > 0)
            {
                Log(BreakoutEventType.BrickDamaged, durability);
                return;
            }

            _aliveBricks--;
            Score += brick.Score;
            Log(BreakoutEventType.BrickDestroyed, brick.Score);
            if (_aliveBricks == 0) AdvanceLevel();
        }

        private void AdvanceLevel()
        {
            Log(BreakoutEventType.LevelCleared, Level + 1);
            Level++;
            _ballSpeed = _rules.BallSpeedForLevel(Level);
            BuildBricks();
            ResetBall();
        }

        private void ResolveBallLost()
        {
            if (_ballCenter.Y >= -_rules.BallRadius) return;

            Lives--;
            if (Lives <= 0)
            {
                Lives = 0;
                IsGameOver = true;
                Log(BreakoutEventType.GameOver, 0);
                return;
            }

            Log(BreakoutEventType.LifeLost, Lives);
            ResetBall();
        }

        private void BuildBricks()
        {
            _bricks.Clear();
            _aliveBricks = 0;
            for (int row = 0; row < _rules.BrickRows; row++)
            {
                for (int column = 0; column < _rules.BrickColumns; column++)
                {
                    _bricks.Add(new BreakoutBrick(column, row, _rules.DurabilityForBrick(row),
                        _rules.ScoreForBrick(row), _rules.BrickRect(column, row)));
                    _aliveBricks++;
                }
            }
        }

        /// <summary>把速度缩放到当前球速模长；垂直分量过小时强制最小垂直分量，防止水平死循环。</summary>
        private Vec2 NormalizeVelocity(Vec2 velocity)
        {
            if (velocity.LengthSquared < 1e-8f) velocity = new Vec2(0f, 1f);
            var result = velocity * (_ballSpeed / velocity.Length);

            if (MathF.Abs(result.Y) < _rules.MinVerticalSpeed)
            {
                var signY = result.Y >= 0f ? 1f : -1f;
                var signX = result.X >= 0f ? 1f : -1f;
                var x = MathF.Sqrt(MathF.Max(0f, _ballSpeed * _ballSpeed - _rules.MinVerticalSpeed * _rules.MinVerticalSpeed));
                result = new Vec2(signX * x, signY * _rules.MinVerticalSpeed);
            }

            return result;
        }

        private void Log(BreakoutEventType type, int value)
        {
            _events.Add(new BreakoutEventLog(StepIndex, type, value));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
