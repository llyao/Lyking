using System;
using System.Collections.Generic;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 六个场景共用的后端生命周期。包状态必须明确区分兼容实现和真实包接入。
    /// </summary>
    public interface ISimulationBackend : IDisposable
    {
        string BackendName { get; }
        string PackageStatus { get; }
        string Name { get; }
        string Status { get; }
        bool UsesRealPackage { get; }
        bool IsInitialized { get; }
        int EntityCount { get; }
        IReadOnlyList<BenchmarkEntity> Entities { get; }
        void Initialize(BenchmarkConfig config);
        void Step(float deltaTime);
    }

    /// <summary>
    /// 无第三方包时的确定性数组实现。真实后端可以复用配置和结果形状，但不应把本类耗时当成包性能证据。
    /// </summary>
    public abstract class ArraySimulationBackend : ISimulationBackend
    {
        private BenchmarkEntity[] _entities = Array.Empty<BenchmarkEntity>();
        private BenchmarkConfig _config = BenchmarkConfig.CreateDefault();
        private bool _initialized;

        protected ArraySimulationBackend(string backendName, string packageStatus, bool usesRealPackage)
        {
            BackendName = backendName;
            PackageStatus = packageStatus;
            UsesRealPackage = usesRealPackage;
        }

        public string BackendName { get; }
        public string PackageStatus { get; protected set; }
        public string Name => BackendName;
        public string Status => PackageStatus;
        public bool UsesRealPackage { get; protected set; }
        public bool IsInitialized => _initialized;
        public int EntityCount => _entities.Length;
        public IReadOnlyList<BenchmarkEntity> Entities => _entities;
        protected BenchmarkConfig Config => _config;
        protected BenchmarkEntity[] MutableEntities => _entities;
        protected BenchmarkBounds Bounds => _config.Bounds;

        public virtual void Initialize(BenchmarkConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Validate();
            Dispose();
            _config = config.Clone();
            _entities = new BenchmarkEntity[_config.EntityCount];
            Random random = new Random(_config.RandomSeed);
            for (int index = 0; index < _entities.Length; index++)
            {
                _entities[index] = BenchmarkEntity.CreateRandom(random, _config);
            }

            _initialized = true;
        }

        public virtual void Step(float deltaTime)
        {
            EnsureInitialized();
            for (int index = 0; index < _entities.Length; index++)
            {
                BenchmarkEntity.Step(ref _entities[index], deltaTime, Bounds, _config.RefreshColor);
            }
        }

        public virtual void Dispose()
        {
            _entities = Array.Empty<BenchmarkEntity>();
            _initialized = false;
        }

        protected void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(BackendName + " 尚未初始化。");
            }
        }
    }
}
