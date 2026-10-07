using Lyking.UnityEcsDemos.Benchmark;

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// Entitas 风格的 Context、Group 和系统链适配边界。
    /// </summary>
    public sealed class EntitasStyleBackend : ArraySimulationBackend
    {
        private const string BackendLabel = "Entitas 风格";

        public EntitasStyleBackend()
            : base(BackendLabel, GetPackageStatus(), false)
        {
        }

        public override void Step(float deltaTime)
        {
            // 真实 Entitas 接入点是 Context -> Group -> 积分系统；兼容模式直接运行同一实体数据。
            base.Step(deltaTime);
        }

        private static string GetPackageStatus()
        {
#if LYKING_ENTITAS_PRESENT
            return "已开启 Entitas 适配开关，但真实 API 尚未接入（当前为兼容实现）";
#else
            return "内置兼容实现（未安装 Entitas，当前无真实包引用）";
#endif
        }
    }
}
