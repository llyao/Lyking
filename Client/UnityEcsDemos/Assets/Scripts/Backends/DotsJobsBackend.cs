using Lyking.UnityEcsDemos.Benchmark;

#if UNITY_ENTITIES
using Unity.Collections;
using Unity.Jobs;
#endif

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// 显式并行 Job 后端。无可选包时复用统一数组实现，保证场景可以直接运行。
    /// </summary>
    public sealed class DotsJobsBackend : ArraySimulationBackend
    {
#if UNITY_ENTITIES
        private NativeArray<BenchmarkEntity> _nativeEntities;
        private bool _nativeAllocated;
#endif

        public DotsJobsBackend()
            : base(
                "DOTS Jobs",
#if UNITY_ENTITIES
                "Unity Jobs/Collections 包分支（IJobParallelFor）",
                true)
#else
                "内置兼容实现（未安装 Unity Jobs 依赖）",
                false)
#endif
        {
        }

        public override void Initialize(BenchmarkConfig config)
        {
            base.Initialize(config);
#if UNITY_ENTITIES
            _nativeEntities = new NativeArray<BenchmarkEntity>(EntityCount, Allocator.Persistent);
            for (int index = 0; index < EntityCount; index++)
            {
                _nativeEntities[index] = MutableEntities[index];
            }
            _nativeAllocated = true;
#endif
        }

        public override void Step(float deltaTime)
        {
#if UNITY_ENTITIES
            EnsureInitialized();
            if (!_nativeAllocated)
            {
                base.Step(deltaTime);
                return;
            }

            for (int index = 0; index < EntityCount; index++)
            {
                _nativeEntities[index] = MutableEntities[index];
            }

            IntegrateJob job = new IntegrateJob
            {
                Entities = _nativeEntities,
                DeltaTime = deltaTime,
                Bounds = Bounds,
                RefreshColor = Config.RefreshColor
            };
            job.Schedule(EntityCount, 128).Complete();

            for (int index = 0; index < EntityCount; index++)
            {
                MutableEntities[index] = _nativeEntities[index];
            }
#else
            base.Step(deltaTime);
#endif
        }

        public override void Dispose()
        {
#if UNITY_ENTITIES
            if (_nativeAllocated)
            {
                _nativeEntities.Dispose();
                _nativeAllocated = false;
            }
#endif
            base.Dispose();
        }

#if UNITY_ENTITIES
        private struct IntegrateJob : IJobParallelFor
        {
            public NativeArray<BenchmarkEntity> Entities;
            public float DeltaTime;
            public BenchmarkBounds Bounds;
            public bool RefreshColor;

            public void Execute(int index)
            {
                BenchmarkEntity entity = Entities[index];
                BenchmarkEntity.Step(ref entity, DeltaTime, Bounds, RefreshColor);
                Entities[index] = entity;
            }
        }
#endif
    }
}
