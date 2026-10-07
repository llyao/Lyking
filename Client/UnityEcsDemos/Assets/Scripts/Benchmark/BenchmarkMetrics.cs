using System;
using System.Diagnostics;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 可复制的统计快照，面板不需要持有 Stopwatch 或内部数组。
    /// </summary>
    public struct BenchmarkMetricsSnapshot
    {
        public int EntityCount;
        public int SampleCount;
        public int SampleWindow;
        public double AverageUpdateMilliseconds;
        public double LastUpdateMilliseconds;
        public double PeakUpdateMilliseconds;
        public double AverageFrameMilliseconds;
        public double LastFrameMilliseconds;
        public double PeakFrameMilliseconds;
        public long LastUpdateGcAllocatedBytes;
        public long LastFrameGcAllocatedBytes;
        public long TotalUpdateGcAllocatedBytes;

        public float AverageFramesPerSecond
        {
            get
            {
                return AverageFrameMilliseconds <= 0d
                    ? 0f
                    : (float)(1000d / AverageFrameMilliseconds);
            }
        }
    }

    /// <summary>
    /// 只测量后端更新区段；帧耗时由运行器单独记录，避免将渲染时间混入更新耗时。
    /// </summary>
    public sealed class BenchmarkMetrics
    {
        private Stopwatch _updateStopwatch = new Stopwatch();
        private long[] _updateGcSamples = Array.Empty<long>();
        private double[] _updateTimeSamples = Array.Empty<double>();
        private double[] _frameTimeSamples = Array.Empty<double>();
        private int _sampleIndex;
        private int _sampleCount;
        private double _updateTimeTotal;
        private double _frameTimeTotal;
        private long _updateGcTotal;
        private long _lastUpdateGc;
        private long _lastFrameGc;
        private long _updateGcBefore;
        private bool _updateInProgress;

        public int EntityCount { get; private set; }
        public int SampleWindow { get; private set; }
        public int SampleCount => _sampleCount;
        public double AverageUpdateMilliseconds => _sampleCount == 0 ? 0d : _updateTimeTotal / _sampleCount;
        public double LastUpdateMilliseconds { get; private set; }
        public double PeakUpdateMilliseconds => GetPeak(_updateTimeSamples);
        public double AverageFrameMilliseconds => _sampleCount == 0 ? 0d : _frameTimeTotal / _sampleCount;
        public double LastFrameMilliseconds { get; private set; }
        public double PeakFrameMilliseconds => GetPeak(_frameTimeSamples);
        public long LastUpdateGcAllocatedBytes => _lastUpdateGc;
        public long LastFrameGcAllocatedBytes => _lastFrameGc;
        public long TotalUpdateGcAllocatedBytes => _updateGcTotal;

        public BenchmarkMetrics(int entityCount = 0, int sampleWindow = 120)
        {
            Reset(entityCount, sampleWindow);
        }

        public void Reset(int entityCount, int sampleWindow)
        {
            if (entityCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityCount));
            }

            if (sampleWindow <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleWindow));
            }

            EntityCount = entityCount;
            SampleWindow = sampleWindow;
            _updateGcSamples = new long[sampleWindow];
            _updateTimeSamples = new double[sampleWindow];
            _frameTimeSamples = new double[sampleWindow];
            _sampleIndex = 0;
            _sampleCount = 0;
            _updateTimeTotal = 0d;
            _frameTimeTotal = 0d;
            _updateGcTotal = 0;
            _lastUpdateGc = 0;
            _lastFrameGc = 0;
            LastUpdateMilliseconds = 0d;
            LastFrameMilliseconds = 0d;
            _updateInProgress = false;
        }

        public void BeginUpdate()
        {
            if (_updateInProgress)
            {
                throw new InvalidOperationException("上一个后端更新尚未结束。");
            }

            _updateGcBefore = GetAllocatedBytes();
            _updateStopwatch.Restart();
            _updateInProgress = true;
        }

        public void MeasureUpdate(Action update)
        {
            if (update is null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            BeginUpdate();
            try
            {
                update();
            }
            finally
            {
                EndUpdate();
            }
        }

        public void EndUpdate()
        {
            if (!_updateInProgress)
            {
                throw new InvalidOperationException("没有正在进行的后端更新。");
            }

            _updateStopwatch.Stop();
            LastUpdateMilliseconds = _updateStopwatch.Elapsed.TotalMilliseconds;
            _lastUpdateGc = Math.Max(0L, GetAllocatedBytes() - _updateGcBefore);
            _updateGcTotal += _lastUpdateGc;
            _updateInProgress = false;
            AddSample(LastUpdateMilliseconds, _lastUpdateGc, LastFrameMilliseconds);
        }

        public void RecordFrame(double frameMilliseconds, long allocatedBytes = 0)
        {
            if (double.IsNaN(frameMilliseconds) || double.IsInfinity(frameMilliseconds) || frameMilliseconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(frameMilliseconds));
            }

            _lastFrameGc = Math.Max(0L, allocatedBytes);
            LastFrameMilliseconds = frameMilliseconds;
            if (_sampleCount == 0)
            {
                // 固定步长尚未触发时只记录最近帧，避免把零更新帧算进后端平均耗时。
                return;
            }

            int latest = (_sampleIndex - 1 + SampleWindow) % SampleWindow;
            _frameTimeTotal -= _frameTimeSamples[latest];
            _frameTimeSamples[latest] = frameMilliseconds;
            _frameTimeTotal += frameMilliseconds;
        }

        public BenchmarkMetricsSnapshot GetSnapshot()
        {
            return new BenchmarkMetricsSnapshot
            {
                EntityCount = EntityCount,
                SampleCount = _sampleCount,
                SampleWindow = SampleWindow,
                AverageUpdateMilliseconds = AverageUpdateMilliseconds,
                LastUpdateMilliseconds = LastUpdateMilliseconds,
                PeakUpdateMilliseconds = PeakUpdateMilliseconds,
                AverageFrameMilliseconds = AverageFrameMilliseconds,
                LastFrameMilliseconds = LastFrameMilliseconds,
                PeakFrameMilliseconds = PeakFrameMilliseconds,
                LastUpdateGcAllocatedBytes = _lastUpdateGc,
                LastFrameGcAllocatedBytes = _lastFrameGc,
                TotalUpdateGcAllocatedBytes = _updateGcTotal
            };
        }

        private void AddSample(double updateMilliseconds, long gcBytes, double frameMilliseconds)
        {
            if (_sampleCount < SampleWindow)
            {
                _sampleCount++;
            }
            else
            {
                _updateTimeTotal -= _updateTimeSamples[_sampleIndex];
                _frameTimeTotal -= _frameTimeSamples[_sampleIndex];
            }

            _updateTimeSamples[_sampleIndex] = updateMilliseconds;
            _updateGcSamples[_sampleIndex] = gcBytes;
            _frameTimeSamples[_sampleIndex] = frameMilliseconds;
            _updateTimeTotal += updateMilliseconds;
            _frameTimeTotal += frameMilliseconds;
            _sampleIndex = (_sampleIndex + 1) % SampleWindow;
        }

        private static long GetAllocatedBytes()
        {
            try
            {
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch (NotSupportedException)
            {
                return 0L;
            }
        }

        private double GetPeak(double[] samples)
        {
            double peak = 0d;
            for (int index = 0; index < _sampleCount; index++)
            {
                if (samples[index] > peak)
                {
                    peak = samples[index];
                }
            }

            return peak;
        }
    }
}
