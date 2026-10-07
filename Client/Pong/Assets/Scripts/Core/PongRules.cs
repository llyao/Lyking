using System;

namespace Lyking.Pong.Core
{
    /// <summary>
    /// 乒乓规则常量：场地尺寸、挡板尺寸与速度、球的大小与速度节奏、AI 限速与死区、胜利分。
    /// 球速每次挡板反弹按 <see cref="BallSpeedUpPerHit"/> 提升并被 <see cref="MaxBallSpeed"/> 夹住，由 Core 提供给表现层驱动。
    /// </summary>
    public sealed class PongRules
    {
        public static readonly PongRules Default = new PongRules(
            fieldWidth: 24f,
            fieldHeight: 14f,
            paddleWidth: 0.7f,
            paddleHeight: 2.8f,
            paddleSpeed: 10f,
            paddleOffsetX: 1f,
            aiPaddleSpeed: 8f,
            aiDeadZone: 0.35f,
            ballRadius: 0.35f,
            ballBaseSpeed: 9f,
            ballSpeedUpPerHit: 1.05f,
            maxBallSpeed: 18f,
            serveAngleRadians: 0.35f,
            winScore: 7);

        public PongRules(
            float fieldWidth,
            float fieldHeight,
            float paddleWidth,
            float paddleHeight,
            float paddleSpeed,
            float paddleOffsetX,
            float aiPaddleSpeed,
            float aiDeadZone,
            float ballRadius,
            float ballBaseSpeed,
            float ballSpeedUpPerHit,
            float maxBallSpeed,
            float serveAngleRadians,
            int winScore)
        {
            if (fieldWidth <= 0f || fieldHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldWidth), fieldWidth, "场地尺寸必须为正。");
            if (paddleWidth <= 0f || paddleWidth > fieldWidth) throw new ArgumentOutOfRangeException(nameof(paddleWidth), paddleWidth, "挡板宽度必须为正且不能超出场地。");
            if (paddleHeight <= 0f || paddleHeight >= fieldHeight) throw new ArgumentOutOfRangeException(nameof(paddleHeight), paddleHeight, "挡板高度必须为正且小于场地高度。");
            if (paddleSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(paddleSpeed), paddleSpeed, "挡板速度必须为正。");
            if (paddleOffsetX < paddleWidth * 0.5f || paddleOffsetX > fieldWidth - paddleWidth * 0.5f) throw new ArgumentOutOfRangeException(nameof(paddleOffsetX), paddleOffsetX, "挡板必须完整落在场地内。");
            if (aiPaddleSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(aiPaddleSpeed), aiPaddleSpeed, "AI 挡板速度必须为正。");
            if (aiDeadZone < 0f) throw new ArgumentOutOfRangeException(nameof(aiDeadZone), aiDeadZone, "AI 死区不能为负。");
            if (ballRadius <= 0f || ballRadius * 2f > fieldWidth || ballRadius * 2f > fieldHeight) throw new ArgumentOutOfRangeException(nameof(ballRadius), ballRadius, "球半径必须为正且小于场地尺寸。");
            if (ballBaseSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(ballBaseSpeed), ballBaseSpeed, "球初速必须为正。");
            if (ballSpeedUpPerHit < 1f) throw new ArgumentOutOfRangeException(nameof(ballSpeedUpPerHit), ballSpeedUpPerHit, "每次反弹加速比例不能小于 1。");
            if (maxBallSpeed < ballBaseSpeed) throw new ArgumentOutOfRangeException(nameof(maxBallSpeed), maxBallSpeed, "球速上限不能低于初速。");
            if (serveAngleRadians <= 0f || serveAngleRadians >= MathF.PI / 2f) throw new ArgumentOutOfRangeException(nameof(serveAngleRadians), serveAngleRadians, "发球角必须大于 0 且小于 90 度。");
            if (winScore < 1) throw new ArgumentOutOfRangeException(nameof(winScore), winScore, "胜利分至少 1。");

            FieldWidth = fieldWidth;
            FieldHeight = fieldHeight;
            PaddleWidth = paddleWidth;
            PaddleHeight = paddleHeight;
            PaddleSpeed = paddleSpeed;
            PaddleOffsetX = paddleOffsetX;
            AiPaddleSpeed = aiPaddleSpeed;
            AiDeadZone = aiDeadZone;
            BallRadius = ballRadius;
            BallBaseSpeed = ballBaseSpeed;
            BallSpeedUpPerHit = ballSpeedUpPerHit;
            MaxBallSpeed = maxBallSpeed;
            ServeAngleRadians = serveAngleRadians;
            WinScore = winScore;
        }

        public float FieldWidth { get; }

        public float FieldHeight { get; }

        public float PaddleWidth { get; }

        public float PaddleHeight { get; }

        /// <summary>玩家左挡板的移动速度（单位/秒）。</summary>
        public float PaddleSpeed { get; }

        /// <summary>挡板中心到本侧边墙的距离，左右挡板对称放置。</summary>
        public float PaddleOffsetX { get; }

        /// <summary>AI 右挡板的移动速度（单位/秒），低于极限球速的垂直分量以保证可被击败。</summary>
        public float AiPaddleSpeed { get; }

        /// <summary>AI 追踪目标差小于该值时停止移动，避免贴球抖动。</summary>
        public float AiDeadZone { get; }

        public float BallRadius { get; }

        /// <summary>每次发球后的球初速。</summary>
        public float BallBaseSpeed { get; }

        /// <summary>每次挡板反弹的球速提升比例。</summary>
        public float BallSpeedUpPerHit { get; }

        public float MaxBallSpeed { get; }

        /// <summary>发球仰角的固定大小（弧度，相对水平方向），上下符号随机。</summary>
        public float ServeAngleRadians { get; }

        public int WinScore { get; }
    }
}
