using System;
using UnityEngine;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 场景中的单一驱动器。场景只装配后端，不复制实体积分逻辑。
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        [SerializeField] private BenchmarkConfig config = new BenchmarkConfig();
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float maxFrameDelta = 0.25f;

        private ISimulationBackend _backend;
        private float _accumulator;
        private int _warmupFramesRemaining;
        private bool _running;

        public BenchmarkConfig Config => config;
        public ISimulationBackend Backend => _backend;
        public BenchmarkMetrics Metrics { get; private set; }
        public bool IsRunning => _running;

        public void SetAutoStart(bool value)
        {
            autoStart = value;
        }

        private void Awake()
        {
            config ??= new BenchmarkConfig();
            Metrics = new BenchmarkMetrics(config.EntityCount, config.SampleWindow);
        }

        private void Start()
        {
            if (autoStart && _backend is not null)
            {
                StartBenchmark();
            }
        }

        private void Update()
        {
            if (!_running || _backend is null)
            {
                return;
            }

            RunFrame(Time.unscaledDeltaTime);
        }

        public void Configure(ISimulationBackend backend, BenchmarkConfig benchmarkConfig = null)
        {
            if (backend is null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            config = (benchmarkConfig ?? config ?? new BenchmarkConfig()).Clone();
            config.Validate();
            _backend?.Dispose();
            _backend = backend;
            Metrics ??= new BenchmarkMetrics();
            Metrics.Reset(config.EntityCount, config.SampleWindow);
            _accumulator = 0f;
            _warmupFramesRemaining = 0;
            _running = false;
        }

        public void StartBenchmark()
        {
            if (_backend is null)
            {
                throw new InvalidOperationException("尚未配置模拟后端。");
            }

            _backend.Initialize(config);
            Metrics.Reset(_backend.EntityCount, config.SampleWindow);
            _accumulator = 0f;
            _warmupFramesRemaining = config.WarmupFrames;
            _running = true;
        }

        public void StopBenchmark()
        {
            _running = false;
        }

        public void RunFrame(float frameDeltaTime)
        {
            if (!_running || _backend is null)
            {
                return;
            }

            if (float.IsNaN(frameDeltaTime) || float.IsInfinity(frameDeltaTime) || frameDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frameDeltaTime));
            }

            float clampedDelta = Mathf.Min(frameDeltaTime, Mathf.Max(0f, maxFrameDelta));
            long frameGcBefore = GetAllocatedBytes();
            _accumulator += clampedDelta;
            int steps = 0;
            while (_accumulator >= config.FixedStep && steps < 8)
            {
                if (_warmupFramesRemaining > 0)
                {
                    _backend.Step(config.FixedStep);
                    _warmupFramesRemaining--;
                }
                else
                {
                    Metrics.BeginUpdate();
                    try
                    {
                        _backend.Step(config.FixedStep);
                    }
                    finally
                    {
                        Metrics.EndUpdate();
                    }
                }
                _accumulator -= config.FixedStep;
                steps++;
            }

            long frameGc = Math.Max(0L, GetAllocatedBytes() - frameGcBefore);
            Metrics.RecordFrame(clampedDelta * 1000d, frameGc);
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

        private void OnDestroy()
        {
            _backend?.Dispose();
            _backend = null;
        }
    }
}
