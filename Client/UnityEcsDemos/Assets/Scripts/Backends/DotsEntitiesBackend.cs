using Lyking.UnityEcsDemos.Benchmark;

#if UNITY_ENTITIES
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;
#endif

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// Unity Entities 数据导向后端。没有 Entities 包时保持可运行的确定性兼容实现。
    /// </summary>
    public sealed class DotsEntitiesBackend : ArraySimulationBackend
    {
#if UNITY_ENTITIES
        private World _world;
        private EntityQuery _query;

        private struct PositionComponent : IComponentData
        {
            public float2 Value;
        }

        private struct VelocityComponent : IComponentData
        {
            public float2 Value;
        }

        private struct ColorPhaseComponent : IComponentData
        {
            public byte Value;
        }
#endif

        public DotsEntitiesBackend()
            : base(
                "DOTS Entities",
#if UNITY_ENTITIES
                "Unity Entities 包分支（组件查询适配）",
                true)
#else
                "内置兼容实现（未安装 Unity Entities）",
                false)
#endif
        {
        }

        public override void Initialize(BenchmarkConfig config)
        {
            base.Initialize(config);
#if UNITY_ENTITIES
            _world = new World("Lyking DOTS Entities");
            EntityManager entityManager = _world.EntityManager;
            _query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<PositionComponent>(),
                ComponentType.ReadWrite<VelocityComponent>(),
                ComponentType.ReadWrite<ColorPhaseComponent>());
            EntityArchetype archetype = entityManager.CreateArchetype(
                typeof(PositionComponent),
                typeof(VelocityComponent),
                typeof(ColorPhaseComponent));
            NativeArray<Entity> entities = entityManager.CreateEntity(archetype, EntityCount, Allocator.Temp);
            for (int index = 0; index < entities.Length; index++)
            {
                BenchmarkEntity source = MutableEntities[index];
                entityManager.SetComponentData(entities[index], new PositionComponent
                {
                    Value = new float2(source.PositionX, source.PositionY)
                });
                entityManager.SetComponentData(entities[index], new VelocityComponent
                {
                    Value = new float2(source.VelocityX, source.VelocityY)
                });
                entityManager.SetComponentData(entities[index], new ColorPhaseComponent
                {
                    Value = source.ColorPhase
                });
            }

            entities.Dispose();
#endif
        }

        public override void Step(float deltaTime)
        {
#if UNITY_ENTITIES
            EnsureInitialized();
            new IntegrateJob
            {
                DeltaTime = deltaTime,
                MinX = Bounds.MinX,
                MaxX = Bounds.MaxX,
                MinY = Bounds.MinY,
                MaxY = Bounds.MaxY,
                RefreshColor = Config.RefreshColor
            }.ScheduleParallel(_query).Complete();
#else
            base.Step(deltaTime);
#endif
        }

        public override void Dispose()
        {
#if UNITY_ENTITIES
            if (_world != null)
            {
                _world.Dispose();
                _world = null;
            }
#endif
            base.Dispose();
        }

#if UNITY_ENTITIES
        private partial struct IntegrateJob : IJobEntity
        {
            public float DeltaTime;
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
            public bool RefreshColor;

            private void Execute(ref PositionComponent position, ref VelocityComponent velocity, ref ColorPhaseComponent colorPhase)
            {
                float2 nextPosition = position.Value + velocity.Value * DeltaTime;
                float2 nextVelocity = velocity.Value;

                if (nextPosition.x < MinX)
                {
                    nextPosition.x = MinX;
                    nextVelocity.x = math.abs(nextVelocity.x);
                }
                else if (nextPosition.x > MaxX)
                {
                    nextPosition.x = MaxX;
                    nextVelocity.x = -math.abs(nextVelocity.x);
                }

                if (nextPosition.y < MinY)
                {
                    nextPosition.y = MinY;
                    nextVelocity.y = math.abs(nextVelocity.y);
                }
                else if (nextPosition.y > MaxY)
                {
                    nextPosition.y = MaxY;
                    nextVelocity.y = -math.abs(nextVelocity.y);
                }

                position.Value = nextPosition;
                velocity.Value = nextVelocity;
                if (RefreshColor)
                {
                    colorPhase.Value++;
                }
            }
        }
#endif
    }
}
