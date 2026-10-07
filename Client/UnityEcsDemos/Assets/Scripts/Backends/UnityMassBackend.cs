using Lyking.UnityEcsDemos.Benchmark;

namespace Lyking.UnityEcsDemos.Backends
{
    /// <summary>
    /// Unity Mass Entity 的可选适配。Mass 包与 Unity 版本绑定，默认不参与项目编译。
    /// </summary>
    public sealed class UnityMassBackend : ArraySimulationBackend
    {
        public UnityMassBackend()
            : base("Unity Mass", GetPackageStatus(), false)
        {
        }

        public override void Step(float deltaTime)
        {
            // 真实接入应在 Mass Entity Query 中完成批量更新，并由表示层负责 GameObject 映射。
            base.Step(deltaTime);
        }

        private static string GetPackageStatus()
        {
#if LYKING_MASS_PRESENT
            return "已开启 Mass 适配开关，但真实 API 尚未接入（当前为兼容实现）";
#else
            return "内置兼容实现（未安装 Unity Mass，当前无真实引用）";
#endif
        }
    }
}
