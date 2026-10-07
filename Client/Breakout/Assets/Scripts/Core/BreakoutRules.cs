using System;

namespace Lyking.Breakout.Core
{
    /// <summary>
    /// 打砖块规则常量：场地尺寸、挡板、球、砖阵与计分。
    /// 球速随关卡线性提升并被 <see cref="MaxBallSpeed"/> 夹住，由 Core 提供给表现层驱动。
    /// </summary>
    public sealed class BreakoutRules
    {
        public static readonly BreakoutRules Default = new BreakoutRules(
            fieldWidth: 20f,
            fieldHeight: 15f,
            paddleWidth: 4f,
            paddleHeight: 0.6f,
            paddleSpeed: 12f,
            paddleCenterY: 1.2f,
            ballRadius: 0.35f,
            ballBaseSpeed: 8f,
            ballSpeedPerLevel: 1.2f,
            maxBallSpeed: 16f,
            minVerticalSpeed: 2.2f,
            brickColumns: 8,
            brickRows: 5,
            brickTopY: 13f,
            brickWidth: 2.2f,
            brickHeight: 0.8f,
            brickGapX: 0.15f,
            brickGapY: 0.2f,
            lives: 3,
            topRowDurability: 2,
            rowScores: new[] { 20, 15, 10, 10, 5 });

        public BreakoutRules(
            float fieldWidth,
            float fieldHeight,
            float paddleWidth,
            float paddleHeight,
            float paddleSpeed,
            float paddleCenterY,
            float ballRadius,
            float ballBaseSpeed,
            float ballSpeedPerLevel,
            float maxBallSpeed,
            float minVerticalSpeed,
            int brickColumns,
            int brickRows,
            float brickTopY,
            float brickWidth,
            float brickHeight,
            float brickGapX,
            float brickGapY,
            int lives,
            int topRowDurability,
            int[] rowScores)
        {
            if (fieldWidth <= 0f || fieldHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(fieldWidth), fieldWidth, "场地尺寸必须为正。");
            if (paddleWidth <= 0f || paddleWidth > fieldWidth) throw new ArgumentOutOfRangeException(nameof(paddleWidth), paddleWidth, "挡板宽度必须为正且不能超出场地。");
            if (paddleHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(paddleHeight), paddleHeight, "挡板高度必须为正。");
            if (paddleSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(paddleSpeed), paddleSpeed, "挡板速度必须为正。");
            if (paddleCenterY <= 0f || paddleCenterY >= fieldHeight) throw new ArgumentOutOfRangeException(nameof(paddleCenterY), paddleCenterY, "挡板高度位置必须在场地内。");
            if (paddleCenterY + paddleHeight * 0.5f + ballRadius >= fieldHeight) throw new ArgumentOutOfRangeException(nameof(paddleCenterY), paddleCenterY, "挡板与贴板球必须完整落在场地内。");
            if (ballRadius <= 0f || ballRadius * 2f > fieldWidth || ballRadius * 2f > fieldHeight) throw new ArgumentOutOfRangeException(nameof(ballRadius), ballRadius, "球半径必须为正且小于场地尺寸。");
            if (ballBaseSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(ballBaseSpeed), ballBaseSpeed, "球初速必须为正。");
            if (ballSpeedPerLevel < 0f) throw new ArgumentOutOfRangeException(nameof(ballSpeedPerLevel), ballSpeedPerLevel, "每关加速量不能为负。");
            if (maxBallSpeed < ballBaseSpeed) throw new ArgumentOutOfRangeException(nameof(maxBallSpeed), maxBallSpeed, "球速上限不能低于初速。");
            if (minVerticalSpeed <= 0f || minVerticalSpeed > ballBaseSpeed) throw new ArgumentOutOfRangeException(nameof(minVerticalSpeed), minVerticalSpeed, "最小垂直分量必须为正且不超过球初速。");
            if (brickColumns < 1) throw new ArgumentOutOfRangeException(nameof(brickColumns), brickColumns, "砖块列数至少 1。");
            if (brickRows < 1) throw new ArgumentOutOfRangeException(nameof(brickRows), brickRows, "砖块行数至少 1。");
            if (brickWidth <= 0f || brickHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(brickWidth), brickWidth, "砖块尺寸必须为正。");
            if (brickGapX < 0f || brickGapY < 0f) throw new ArgumentOutOfRangeException(nameof(brickGapX), brickGapX, "砖块间距不能为负。");
            if (brickColumns * brickWidth + (brickColumns - 1) * brickGapX > fieldWidth) throw new ArgumentOutOfRangeException(nameof(brickWidth), brickWidth, "砖阵总宽不能超出场地。");
            if (brickTopY > fieldHeight || brickTopY - (brickRows - 1) * (brickHeight + brickGapY) - brickHeight < 0f) throw new ArgumentOutOfRangeException(nameof(brickTopY), brickTopY, "砖阵必须完整落在场地内。");
            if (lives < 1) throw new ArgumentOutOfRangeException(nameof(lives), lives, "生命至少 1。");
            if (topRowDurability < 1) throw new ArgumentOutOfRangeException(nameof(topRowDurability), topRowDurability, "顶行耐久至少 1。");
            if (rowScores is null || rowScores.Length != brickRows) throw new ArgumentException("分值表长度必须等于砖块行数。", nameof(rowScores));
            if (Array.Exists(rowScores, score => score <= 0)) throw new ArgumentOutOfRangeException(nameof(rowScores), rowScores, "每砖分值必须为正。");

            FieldWidth = fieldWidth;
            FieldHeight = fieldHeight;
            PaddleWidth = paddleWidth;
            PaddleHeight = paddleHeight;
            PaddleSpeed = paddleSpeed;
            PaddleCenterY = paddleCenterY;
            BallRadius = ballRadius;
            BallBaseSpeed = ballBaseSpeed;
            BallSpeedPerLevel = ballSpeedPerLevel;
            MaxBallSpeed = maxBallSpeed;
            MinVerticalSpeed = minVerticalSpeed;
            BrickColumns = brickColumns;
            BrickRows = brickRows;
            BrickTopY = brickTopY;
            BrickWidth = brickWidth;
            BrickHeight = brickHeight;
            BrickGapX = brickGapX;
            BrickGapY = brickGapY;
            Lives = lives;
            TopRowDurability = topRowDurability;
            RowScores = (int[])rowScores.Clone();
        }

        public float FieldWidth { get; }

        public float FieldHeight { get; }

        public float PaddleWidth { get; }

        public float PaddleHeight { get; }

        public float PaddleSpeed { get; }

        public float PaddleCenterY { get; }

        public float BallRadius { get; }

        public float BallBaseSpeed { get; }

        public float BallSpeedPerLevel { get; }

        public float MaxBallSpeed { get; }

        public float MinVerticalSpeed { get; }

        public int BrickColumns { get; }

        public int BrickRows { get; }

        public float BrickTopY { get; }

        public float BrickWidth { get; }

        public float BrickHeight { get; }

        public float BrickGapX { get; }

        public float BrickGapY { get; }

        public int Lives { get; }

        public int TopRowDurability { get; }

        public int[] RowScores { get; }

        /// <summary>第 <paramref name="level"/> 关（从 1 起）的球速，确定性纯函数。</summary>
        public float BallSpeedForLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "关卡从 1 开始。");
            return Math.Min(MaxBallSpeed, BallBaseSpeed + (level - 1) * BallSpeedPerLevel);
        }

        /// <summary>指定行的耐久：顶行需多次击破，其余一行一破。</summary>
        public int DurabilityForBrick(int row)
        {
            if (row < 0 || row >= BrickRows) throw new ArgumentOutOfRangeException(nameof(row), row, "砖块行号越界。");
            return row == 0 ? TopRowDurability : 1;
        }

        /// <summary>指定行的击破分值：顶行最高，向下递减。</summary>
        public int ScoreForBrick(int row)
        {
            if (row < 0 || row >= BrickRows) throw new ArgumentOutOfRangeException(nameof(row), row, "砖块行号越界。");
            return RowScores[row];
        }

        /// <summary>指定行列砖块的包围盒：砖阵水平居中、自 <see cref="BrickTopY"/> 向下排列。</summary>
        public Aabb BrickRect(int column, int row)
        {
            if (column < 0 || column >= BrickColumns) throw new ArgumentOutOfRangeException(nameof(column), column, "砖块列号越界。");
            if (row < 0 || row >= BrickRows) throw new ArgumentOutOfRangeException(nameof(row), row, "砖块行号越界。");

            float totalWidth = BrickColumns * BrickWidth + (BrickColumns - 1) * BrickGapX;
            float left = (FieldWidth - totalWidth) * 0.5f + column * (BrickWidth + BrickGapX);
            float top = BrickTopY - row * (BrickHeight + BrickGapY);
            return new Aabb(new Vec2(left, top - BrickHeight), new Vec2(left + BrickWidth, top));
        }
    }
}
