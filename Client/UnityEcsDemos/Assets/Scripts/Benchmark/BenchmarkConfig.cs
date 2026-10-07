using System;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 所有后端共用的基准参数。字段保持公开，便于 Unity Inspector 序列化，也便于纯 C# 测试直接构造。
    /// </summary>
    [Serializable]
    public sealed class BenchmarkConfig
    {
        public int entityCount = 10000;
        public int randomSeed = 20261003;
        public float fixedStep = 1f / 60f;
        public float minX = -50f;
        public float maxX = 50f;
        public float minY = -28f;
        public float maxY = 28f;
        public float minSpeed = 1f;
        public float maxSpeed = 5f;
        public int sampleWindow = 120;
        public int warmupFrames = 30;
        public bool refreshColor;

        public int EntityCount => entityCount;
        public int RandomSeed => randomSeed;
        public float FixedStep => fixedStep;
        public BenchmarkBounds Bounds => new BenchmarkBounds(minX, maxX, minY, maxY);
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public int SampleWindow => sampleWindow;
        public int WarmupFrames => warmupFrames;
        public bool RefreshColor => refreshColor;

        public BenchmarkConfig Clone()
        {
            return new BenchmarkConfig
            {
                entityCount = entityCount,
                randomSeed = randomSeed,
                fixedStep = fixedStep,
                minX = minX,
                maxX = maxX,
                minY = minY,
                maxY = maxY,
                minSpeed = minSpeed,
                maxSpeed = maxSpeed,
                sampleWindow = sampleWindow,
                warmupFrames = warmupFrames,
                refreshColor = refreshColor
            };
        }

        public void Validate()
        {
            if (entityCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityCount), "实体数量必须是非负数。");
            }

            if (!IsFinite(fixedStep) || fixedStep <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStep), "固定步长必须是正的有限数值。");
            }

            if (!IsFinite(minX) || !IsFinite(maxX) || !IsFinite(minY) || !IsFinite(maxY)
                || minX >= maxX || minY >= maxY)
            {
                throw new ArgumentOutOfRangeException(nameof(minX), "边界必须是有限数值，且最小值小于最大值。");
            }

            if (!IsFinite(minSpeed) || !IsFinite(maxSpeed) || minSpeed < 0f || minSpeed > maxSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), "速度范围必须是有限数值，且最小速度不大于最大速度。");
            }

            if (sampleWindow <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleWindow), "采样窗口必须是正数。");
            }

            if (warmupFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupFrames), "预热帧数不能为负数。");
            }
        }

        public static BenchmarkConfig CreateDefault()
        {
            return new BenchmarkConfig();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
