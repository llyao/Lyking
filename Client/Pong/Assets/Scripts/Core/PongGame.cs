using System;
using System.Collections.Generic;

namespace Lyking.Pong.Core
{
    /// <summary>
    /// 乒乓核心对局：玩家左挡板对 AI 右挡板的确定性连续模拟，无 Unity 依赖。
    /// 球为圆（圆心 + 半径），上下墙反射取反 Y 分量；挡板反射角由命中位置偏离中心的比例决定（±60° 内），
    /// 每次挡板反弹球速提升并夹在上限；球飞出左右边界对方得分，先到胜利分获胜。
    /// <see cref="Step"/> 内部按最长 1/120 秒的固定子步推进防止穿透；
    /// 发球上下方向等随机由注入种子保证同种子完全一致。
    /// </summary>
    public sealed class PongGame
    {
        private const int MaxEventLogs = 128;

        /// <summary>单次物理子步的最长时长（秒），防止球在一子步内穿过挡板。</summary>
        private const float MaxSubstepSeconds = 1f / 120f;

        /// <summary>挡板反弹的最大偏角（弧度，相对水平方向）：中心水平回弹，越靠边越斜。</summary>
        public const float MaxBounceAngleRadians = MathF.PI / 3f;

        /// <summary>碰撞后把球推离挡板表面的间隙，避免下一子步重复判定同一表面。</summary>
        private const float BounceEpsilon = 0.001f;

        private readonly PongRules _rules;
        private readonly Random _random;
        private readonly List<PongEventLog> _events = new List<PongEventLog>();
        private Vec2 _ballPosition;
        private Vec2 _ballVelocity;
        private float _ballSpeed;
        private float _leftPaddleY;
        private float _rightPaddleY;

        public PongGame(PongRules? rules = null, int seed = 0)
        {
            _rules = rules ?? PongRules.Default;
            _random = new Random(seed);
            _leftPaddleY = _rules.FieldHeight * 0.5f;
            _rightPaddleY = _leftPaddleY;
            Log(PongEventType.Spawned, 0);
            ServeToward(_random.Next(2) == 0);
        }

        public PongRules Rules => _rules;

        public PongState State { get; private set; }

        public bool IsFinished => State != PongState.Running;

        public int LeftScore { get; private set; }

        public int RightScore { get; private set; }

        public Vec2 BallPosition => _ballPosition;

        public Vec2 BallVelocity => _ballVelocity;

        /// <summary>当前球速模长：发球回到初速，每次挡板反弹提升并夹在上限。</summary>
        public float BallSpeed => _ballSpeed;

        public Vec2 LeftPaddlePosition => new Vec2(_rules.PaddleOffsetX, _leftPaddleY);

        public Vec2 RightPaddlePosition => new Vec2(_rules.FieldWidth - _rules.PaddleOffsetX, _rightPaddleY);

        /// <summary>已完成的物理子步数。</summary>
        public int StepIndex { get; private set; }

        public IReadOnlyList<PongEventLog> Events => _events;

        /// <summary>挡板反弹角纯函数：命中偏移（-1 下边缘到 +1 上边缘）映射为 ±60° 内、相对水平方向的反射角。</summary>
        public static float ReflectAngle(float hitOffset) => Math.Clamp(hitOffset, -1f, 1f) * MaxBounceAngleRadians;

        /// <summary>AI 目标高度纯函数：球朝右挡板飞来（X 分量为正）时追踪球心高度，否则回中等待。</summary>
        public static float AiTargetY(float ballY, float ballVelocityX, float fieldCenterY) =>
            ballVelocityX > 0f ? ballY : fieldCenterY;

        /// <summary>
        /// 推进对局：<paramref name="leftMove"/> 为左挡板移动输入（-1 下移、0 停止、+1 上移，支持区间内模拟量），
        /// 内部按最长 1/120 秒的固定子步推进物理，任意帧时长都安全。
        /// </summary>
        public void Step(float deltaTime, float leftMove)
        {
            if (IsFinished || deltaTime <= 0f) return;

            var direction = Math.Clamp(leftMove, -1f, 1f);
            var remaining = deltaTime;
            while (remaining > 0f && !IsFinished)
            {
                var h = MathF.Min(MaxSubstepSeconds, remaining);
                Advance(h, direction);
                remaining -= h;
            }
        }

        /// <summary>测试与回放入口：把球重置到场心并按指定反射角（弧度，相对水平方向）与朝向直接发球；对局结束后返回 false。</summary>
        public bool ResetServe(float angleRadians, bool towardRight)
        {
            if (IsFinished) return false;

            ServeWith(Math.Clamp(angleRadians, -1.55f, 1.55f), towardRight);
            return true;
        }

        private void Advance(float h, float leftMove)
        {
            StepIndex++;
            MoveLeftPaddle(h, leftMove);
            MoveRightPaddleByAi(h);
            _ballPosition += _ballVelocity * h;
            ResolveWalls();
            ResolvePaddles();
            ResolveScore();
        }

        private void MoveLeftPaddle(float h, float leftMove)
        {
            var halfHeight = _rules.PaddleHeight * 0.5f;
            var maxDelta = _rules.PaddleSpeed * h;
            _leftPaddleY = Math.Clamp(_leftPaddleY + leftMove * maxDelta, halfHeight, _rules.FieldHeight - halfHeight);
        }

        /// <summary>AI 挡板以限速追踪目标高度，目标差在死区内时停止，避免贴球抖动。</summary>
        private void MoveRightPaddleByAi(float h)
        {
            var target = AiTargetY(_ballPosition.Y, _ballVelocity.X, _rules.FieldHeight * 0.5f);
            var deltaY = target - _rightPaddleY;
            if (MathF.Abs(deltaY) <= _rules.AiDeadZone) return;

            var halfHeight = _rules.PaddleHeight * 0.5f;
            var maxDelta = _rules.AiPaddleSpeed * h;
            _rightPaddleY = Math.Clamp(_rightPaddleY + Math.Clamp(deltaY, -maxDelta, maxDelta), halfHeight, _rules.FieldHeight - halfHeight);
        }

        private void ResolveWalls()
        {
            var r = _rules.BallRadius;
            if (_ballPosition.Y < r)
            {
                _ballPosition = _ballPosition.WithY(r);
                if (_ballVelocity.Y < 0f) _ballVelocity = _ballVelocity.WithY(-_ballVelocity.Y);
                Log(PongEventType.WallBounced, -1);
            }
            else if (_ballPosition.Y > _rules.FieldHeight - r)
            {
                _ballPosition = _ballPosition.WithY(_rules.FieldHeight - r);
                if (_ballVelocity.Y > 0f) _ballVelocity = _ballVelocity.WithY(-_ballVelocity.Y);
                Log(PongEventType.WallBounced, 1);
            }
        }

        private void ResolvePaddles()
        {
            var r = _rules.BallRadius;
            var halfWidth = _rules.PaddleWidth * 0.5f;
            var reachY = _rules.PaddleHeight * 0.5f + r;

            if (_ballVelocity.X < 0f)
            {
                var left = LeftPaddlePosition;
                var frontX = left.X + halfWidth;
                if (_ballPosition.X <= frontX + r && MathF.Abs(_ballPosition.Y - left.Y) <= reachY)
                {
                    BounceOffPaddle(left.Y, reachY, +1, frontX + r + BounceEpsilon);
                }
            }
            else if (_ballVelocity.X > 0f)
            {
                var right = RightPaddlePosition;
                var backX = right.X - halfWidth;
                if (_ballPosition.X >= backX - r && MathF.Abs(_ballPosition.Y - right.Y) <= reachY)
                {
                    BounceOffPaddle(right.Y, reachY, -1, backX - r - BounceEpsilon);
                }
            }
        }

        /// <summary>按命中偏移设置反射角并加速，把球推回挡板表面之外；<paramref name="side"/> 为 +1 左挡板、-1 右挡板。</summary>
        private void BounceOffPaddle(float paddleY, float reachY, int side, float ballXAfter)
        {
            var offset = (_ballPosition.Y - paddleY) / reachY;
            var angle = ReflectAngle(offset);
            _ballSpeed = MathF.Min(_rules.MaxBallSpeed, _ballSpeed * _rules.BallSpeedUpPerHit);
            _ballVelocity = new Vec2(side * MathF.Cos(angle), MathF.Sin(angle)) * _ballSpeed;
            _ballPosition = _ballPosition.WithX(ballXAfter);
            Log(PongEventType.PaddleBounced, side);
        }

        private void ResolveScore()
        {
            var r = _rules.BallRadius;
            if (_ballPosition.X + r < 0f)
            {
                ScorePoint(rightScored: true);
            }
            else if (_ballPosition.X - r > _rules.FieldWidth)
            {
                ScorePoint(rightScored: false);
            }
        }

        private void ScorePoint(bool rightScored)
        {
            if (rightScored) RightScore++;
            else LeftScore++;
            Log(PongEventType.Scored, rightScored ? -1 : 1);

            if (LeftScore >= _rules.WinScore)
            {
                State = PongState.LeftWon;
                Log(PongEventType.Won, 1);
                return;
            }

            if (RightScore >= _rules.WinScore)
            {
                State = PongState.RightWon;
                Log(PongEventType.Won, -1);
                return;
            }

            // 发球朝失分方：右侧丢分（球出左界）则朝左发球。
            ServeToward(!rightScored);
        }

        /// <summary>把球重置到场心并朝失分方发球：发球角固定、垂直分量恒定，上下方向由注入种子随机。</summary>
        private void ServeToward(bool towardRight) =>
            ServeWith((_random.Next(2) == 0 ? 1f : -1f) * _rules.ServeAngleRadians, towardRight);

        private void ServeWith(float angleRadians, bool towardRight)
        {
            _ballSpeed = _rules.BallBaseSpeed;
            _ballPosition = new Vec2(_rules.FieldWidth * 0.5f, _rules.FieldHeight * 0.5f);
            var directionX = towardRight ? 1f : -1f;
            _ballVelocity = new Vec2(directionX * MathF.Cos(angleRadians), MathF.Sin(angleRadians)) * _ballSpeed;
            Log(PongEventType.Served, towardRight ? 1 : -1);
        }

        private void Log(PongEventType type, int value)
        {
            _events.Add(new PongEventLog(StepIndex, type, value));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
