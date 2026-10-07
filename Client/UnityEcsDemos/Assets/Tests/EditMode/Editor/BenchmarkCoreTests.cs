using System;
using Lyking.UnityEcsDemos.Benchmark;
using NUnit.Framework;

namespace Lyking.UnityEcsDemos.Tests.EditMode
{
    public sealed class BenchmarkCoreTests
    {
        [Test]
        public void 实体越过右上边界时位置钳制并反弹()
        {
            var entity = new BenchmarkEntity(0f, 0f, 2f, 3f);

            entity.Integrate(1f, new BenchmarkBounds(-1f, 1f, -1f, 1f), true);

            Assert.That(entity.PositionX, Is.EqualTo(1f));
            Assert.That(entity.PositionY, Is.EqualTo(1f));
            Assert.That(entity.VelocityX, Is.EqualTo(-2f));
            Assert.That(entity.VelocityY, Is.EqualTo(-3f));
            Assert.That(entity.ColorPhase, Is.EqualTo((byte)1));
        }

        [Test]
        public void 实体越过左下边界时位置钳制并反弹()
        {
            var entity = new BenchmarkEntity(0f, 0f, -2f, -3f);

            entity.Integrate(1f, new BenchmarkBounds(-1f, 1f, -1f, 1f));

            Assert.That(entity.PositionX, Is.EqualTo(-1f));
            Assert.That(entity.PositionY, Is.EqualTo(-1f));
            Assert.That(entity.VelocityX, Is.EqualTo(2f));
            Assert.That(entity.VelocityY, Is.EqualTo(3f));
        }

        [Test]
        public void 采样窗口滚动后帧耗时平均值和峰值正确()
        {
            var metrics = new BenchmarkMetrics(entityCount: 10, sampleWindow: 2);

            RecordFrame(metrics, 10d);
            RecordFrame(metrics, 20d);
            RecordFrame(metrics, 30d);

            Assert.That(metrics.SampleCount, Is.EqualTo(2));
            Assert.That(metrics.AverageFrameMilliseconds, Is.EqualTo(25d).Within(0.0001d));
            Assert.That(metrics.PeakFrameMilliseconds, Is.EqualTo(30d).Within(0.0001d));
            Assert.That(metrics.LastFrameMilliseconds, Is.EqualTo(30d).Within(0.0001d));

            BenchmarkMetricsSnapshot snapshot = metrics.GetSnapshot();
            Assert.That(snapshot.EntityCount, Is.EqualTo(10));
            Assert.That(snapshot.SampleWindow, Is.EqualTo(2));
            Assert.That(snapshot.AverageFramesPerSecond, Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void 默认配置通过校验()
        {
            Assert.DoesNotThrow(() => BenchmarkConfig.CreateDefault().Validate());
        }

        [Test]
        public void 配置拒绝非法实体数量和固定步长()
        {
            AssertInvalid(config => config.entityCount = -1);
            AssertInvalid(config => config.fixedStep = 0f);
            AssertInvalid(config => config.fixedStep = float.NaN);
        }

        [Test]
        public void 配置拒绝非法边界和速度范围()
        {
            AssertInvalid(config => config.minX = config.maxX);
            AssertInvalid(config => config.minY = config.maxY + 1f);
            AssertInvalid(config => config.minSpeed = -1f);
            AssertInvalid(config => config.minSpeed = config.maxSpeed + 1f);
        }

        [Test]
        public void 配置拒绝非法采样窗口和预热帧数()
        {
            AssertInvalid(config => config.sampleWindow = 0);
            AssertInvalid(config => config.warmupFrames = -1);
        }

        private static void RecordFrame(BenchmarkMetrics metrics, double frameMilliseconds)
        {
            metrics.MeasureUpdate(() => { });
            metrics.RecordFrame(frameMilliseconds);
        }

        private static void AssertInvalid(Action<BenchmarkConfig> configure)
        {
            var config = BenchmarkConfig.CreateDefault();
            configure(config);

            Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
        }
    }
}
