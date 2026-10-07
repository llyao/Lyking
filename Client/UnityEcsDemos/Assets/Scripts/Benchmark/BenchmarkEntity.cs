using System;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 不依赖 Unity 类型的二维边界，确保积分逻辑可以在普通 .NET 测试中运行。
    /// </summary>
    [Serializable]
    public struct BenchmarkBounds
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;

        public BenchmarkBounds(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }
    }

    /// <summary>
    /// 所有后端使用的最小实体数据。位置和速度采用结构体字段，避免兼容后端创建托管对象。
    /// </summary>
    [Serializable]
    public struct BenchmarkEntity
    {
        public float PositionX;
        public float PositionY;
        public float VelocityX;
        public float VelocityY;
        public byte ColorPhase;

        public BenchmarkEntity(float positionX, float positionY, float velocityX, float velocityY)
        {
            PositionX = positionX;
            PositionY = positionY;
            VelocityX = velocityX;
            VelocityY = velocityY;
            ColorPhase = 0;
        }

        public static BenchmarkEntity CreateRandom(Random random, BenchmarkConfig config)
        {
            if (random is null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            float x = Lerp(config.minX, config.maxX, random.NextDouble());
            float y = Lerp(config.minY, config.maxY, random.NextDouble());
            float speed = Lerp(config.minSpeed, config.maxSpeed, random.NextDouble());
            double angle = random.NextDouble() * Math.PI * 2d;
            return new BenchmarkEntity(
                x,
                y,
                (float)(Math.Cos(angle) * speed),
                (float)(Math.Sin(angle) * speed));
        }

        public void Integrate(float deltaTime, BenchmarkBounds bounds, bool refreshColor = false)
        {
            Step(ref this, deltaTime, bounds, refreshColor);
        }

        public static void Step(ref BenchmarkEntity entity, float deltaTime, BenchmarkBounds bounds, bool refreshColor = false)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "积分步长必须是非负有限数值。");
            }

            entity.PositionX += entity.VelocityX * deltaTime;
            entity.PositionY += entity.VelocityY * deltaTime;

            if (entity.PositionX < bounds.MinX)
            {
                entity.PositionX = bounds.MinX;
                entity.VelocityX = Math.Abs(entity.VelocityX);
            }
            else if (entity.PositionX > bounds.MaxX)
            {
                entity.PositionX = bounds.MaxX;
                entity.VelocityX = -Math.Abs(entity.VelocityX);
            }

            if (entity.PositionY < bounds.MinY)
            {
                entity.PositionY = bounds.MinY;
                entity.VelocityY = Math.Abs(entity.VelocityY);
            }
            else if (entity.PositionY > bounds.MaxY)
            {
                entity.PositionY = bounds.MaxY;
                entity.VelocityY = -Math.Abs(entity.VelocityY);
            }

            if (refreshColor)
            {
                entity.ColorPhase = unchecked((byte)(entity.ColorPhase + 1));
            }
        }

        private static float Lerp(float from, float to, double amount)
        {
            return from + (to - from) * (float)amount;
        }
    }
}
