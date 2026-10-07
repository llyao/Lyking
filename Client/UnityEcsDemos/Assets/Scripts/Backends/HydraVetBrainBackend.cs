using Lyking.UnityEcsDemos.Benchmark;

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// Hydra VetBrain ECS 的适配边界示例，不把 Hydra 私有程序集当作 Unity 项目必需依赖。
    /// </summary>
    public sealed class HydraVetBrainBackend : ArraySimulationBackend
    {
        public HydraVetBrainBackend()
            : base("Hydra VetBrain", GetPackageStatus(), false)
        {
        }

        public override void Step(float deltaTime)
        {
            // 真实接入应把 World、Aspect 和 Grouping 绑定到共享模拟；兼容模式保持本地数组。
            base.Step(deltaTime);
        }

        private static string GetPackageStatus()
        {
#if LYKING_VETBRAIN_PRESENT
            return "已开启 VetBrain 适配开关，但真实程序集尚未接入（当前为兼容实现）";
#else
            return "内置兼容实现（未安装 VetBrain 程序集，当前无真实引用）";
#endif
        }
    }
}
