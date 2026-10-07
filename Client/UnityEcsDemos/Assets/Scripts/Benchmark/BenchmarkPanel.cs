using UnityEngine;

namespace Lyking.UnityEcsDemos.Benchmark
{
    /// <summary>
    /// 使用 Unity 内置 GUI，避免基准示例额外依赖 UI 包。面板明确显示兼容或真实包状态。
    /// </summary>
    public sealed class BenchmarkPanel : MonoBehaviour
    {
        [SerializeField] private BenchmarkRunner runner;
        [SerializeField] private Rect area = new Rect(20f, 20f, 460f, 230f);
        [SerializeField] private bool showInstructions = true;

        public void Bind(BenchmarkRunner benchmarkRunner)
        {
            runner = benchmarkRunner;
        }

        private void Awake()
        {
            runner ??= FindObjectOfType<BenchmarkRunner>();
        }

        private void OnGUI()
        {
            if (runner is null || runner.Backend is null || runner.Metrics is null)
            {
                return;
            }

            ISimulationBackend backend = runner.Backend;
            BenchmarkMetricsSnapshot metrics = runner.Metrics.GetSnapshot();
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Unity ECS 基准面板");
            GUILayout.Label("后端: " + backend.BackendName);
            GUILayout.Label("状态: " + backend.PackageStatus);
            GUILayout.Label("实体数: " + metrics.EntityCount);
            GUILayout.Label(string.Format("更新: 平均 {0:0.000} ms    峰值 {1:0.000} ms", metrics.AverageUpdateMilliseconds, metrics.PeakUpdateMilliseconds));
            GUILayout.Label(string.Format("帧耗时: 平均 {0:0.000} ms    峰值 {1:0.000} ms", metrics.AverageFrameMilliseconds, metrics.PeakFrameMilliseconds));
            GUILayout.Label(string.Format("更新 GC: {0} B    帧 GC: {1} B", metrics.LastUpdateGcAllocatedBytes, metrics.LastFrameGcAllocatedBytes));
            GUILayout.Label(string.Format("采样: {0}/{1}", metrics.SampleCount, metrics.SampleWindow));
            if (showInstructions)
            {
                GUILayout.Label("兼容状态只说明 API 形状和可运行性，不代表第三方包性能。");
            }

            GUILayout.EndArea();
        }
    }
}
