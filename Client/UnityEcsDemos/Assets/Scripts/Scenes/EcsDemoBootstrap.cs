using UnityEngine;
using Lyking.UnityEcsDemos.Benchmark;

namespace Lyking.UnityEcsDemos.Scenes
{
    /// <summary>
    /// 场景唯一的运行时装配入口。它只选择后端并连接面板，不包含实体更新逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcsDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private DemoBackendKind backendKind = DemoBackendKind.GameObjectBaseline;
        [SerializeField] private BenchmarkRunner benchmarkRunner;
        [SerializeField] private BenchmarkPanel benchmarkPanel;
        [SerializeField] private BenchmarkConfig benchmarkConfig = new BenchmarkConfig();
        [SerializeField] private bool startOnAwake = true;

        public DemoBackendKind BackendKind => backendKind;
        public BenchmarkRunner Runner => benchmarkRunner;

        private void Awake()
        {
            benchmarkRunner ??= GetComponent<BenchmarkRunner>();
            benchmarkRunner ??= FindObjectOfType<BenchmarkRunner>();
            benchmarkRunner ??= gameObject.AddComponent<BenchmarkRunner>();
            benchmarkRunner.SetAutoStart(false);
            benchmarkPanel ??= FindObjectOfType<BenchmarkPanel>();
            benchmarkPanel ??= gameObject.AddComponent<BenchmarkPanel>();

            if (benchmarkRunner is null)
            {
                Debug.LogError("场景缺少 BenchmarkRunner，无法启动 ECS 对比。", this);
                return;
            }

            try
            {
                benchmarkRunner.Configure(SceneBackendSelector.Create(backendKind), benchmarkConfig);
                benchmarkPanel?.Bind(benchmarkRunner);
                if (startOnAwake)
                {
                    benchmarkRunner.StartBenchmark();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            benchmarkRunner?.StopBenchmark();
        }
    }
}
