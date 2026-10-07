using System;
using Lyking.UnityEcsDemos.Benchmark;
using Lyking.UnityEcsDemos.Backends;

namespace Lyking.UnityEcsDemos.Scenes
{
    /// <summary>
    /// 场景可用的六种基准后端。该枚举只属于场景装配层，避免把场景配置耦合到第三方包。
    /// </summary>
    public enum DemoBackendKind
    {
        GameObjectBaseline = 0,
        DotsEntities = 1,
        DotsJobs = 2,
        EntitasStyle = 3,
        HydraVetBrain = 4,
        UnityMass = 5
    }

    /// <summary>
    /// 将场景枚举映射到统一后端接口。后端实现可以在没有第三方包时使用兼容实现。
    /// </summary>
    public static class SceneBackendSelector
    {
        public static ISimulationBackend Create(DemoBackendKind kind)
        {
            switch (kind)
            {
                case DemoBackendKind.GameObjectBaseline:
                    return new GameObjectBackend();
                case DemoBackendKind.DotsEntities:
                    return new DotsEntitiesBackend();
                case DemoBackendKind.DotsJobs:
                    return new DotsJobsBackend();
                case DemoBackendKind.EntitasStyle:
                    return new EntitasStyleBackend();
                case DemoBackendKind.HydraVetBrain:
                    return new HydraVetBrainBackend();
                case DemoBackendKind.UnityMass:
                    return new UnityMassBackend();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的基准后端。");
            }
        }

        public static string GetSceneName(DemoBackendKind kind)
        {
            switch (kind)
            {
                case DemoBackendKind.GameObjectBaseline:
                    return "00_GameObjectBaseline";
                case DemoBackendKind.DotsEntities:
                    return "01_DotsEntities";
                case DemoBackendKind.DotsJobs:
                    return "02_DotsJobs";
                case DemoBackendKind.EntitasStyle:
                    return "03_EntitasStyle";
                case DemoBackendKind.HydraVetBrain:
                    return "04_HydraVetBrain";
                case DemoBackendKind.UnityMass:
                    return "05_UnityMass";
                default:
                    return kind.ToString();
            }
        }
    }
}
