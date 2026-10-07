# 验证证据

## 2026-10-04

### 路由与范围

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "新增 Unity DOTS、Entitas、Hydra VetBrain ECS 与 Mass 对比 Demo" --path Client/UnityEcsDemos`
- 退出码：`0`；主领域为 `Client`，建议档位为 `standard`。
- 用户已确认“可运行骨架 + 可选真实包接入”的交付形态；本版将 Entitas、VetBrain、Mass 明确为适配占位，DOTS Entities/Jobs 保留可选真实分支。

### 工作流与结构

- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-unity-ecs-demos`：退出码 `0`，`issues=[]`、`isValid=true`。
- `git diff --check`：退出码 `0`，没有空白错误。
- 六个 `.unity` 场景、六个 `.meta`、Build Settings 路径和 GUID 一致，结构检查退出码 `0`。
- 已新增无独立程序集的 `Assets/Tests/EditMode/Editor/BenchmarkCoreTests.cs`，覆盖边界反弹、颜色刷新、统计窗口和配置校验；因 Unity 许可证缺失，测试尚未在编辑器中执行。
- 最终范围文本行尾空白扫描退出码 `0`；`validate` 在测试资产路径修正后再次返回 `issues=[]`、`isValid=true`。
- PowerShell `Add-Type` 编译 `BenchmarkConfig`、`BenchmarkEntity`、`BenchmarkMetrics`、`ISimulationBackend` 并断言配置校验、边界反弹和统计峰值，退出码 `0`。

### 兼容基准

命令：`Client/UnityEcsDemos/Docs/run-compat-benchmark.ps1`，预热 30 帧、采样 120 帧。

| 后端 | 1,000 实体 | 10,000 实体 | 50,000 实体 |
| --- | ---: | ---: | ---: |
| GameObject API 基线 | 0.033 ms | 0.242 ms | 0.923 ms |
| DOTS Entities 兼容 | 0.021 ms | 0.201 ms | 0.905 ms |
| DOTS Jobs 兼容 | 0.020 ms | 0.195 ms | 0.992 ms |
| Entitas 风格兼容 | 0.020 ms | 0.196 ms | 0.905 ms |
| Hydra VetBrain 兼容 | 0.020 ms | 0.212 ms | 0.961 ms |
| Unity Mass 兼容 | 0.020 ms | 0.301 ms | 0.908 ms |

以上数字来自托管数组兼容实现，仅证明统一模型可以运行和随实体数扩展；不能作为 DOTS、Entitas、VetBrain、Mass 或逐对象 MonoBehaviour 的真实性能结论。

### Unity 环境缺口

- 主机可执行文件：`C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe`，版本为 `6000.4.0f1`，不是项目锁定的 `2022.3.40f1`。
- 结构烟测命令使用 `-batchmode -nographics -quit -projectPath Client/UnityEcsDemos -executeMethod Lyking.UnityEcsDemos.Editor.EcsDemoBatchSmoke.RunStructure`。
- 退出码：`198`；关键输出：`No valid Unity Editor license found. Please activate your license.`
- 因许可证和补丁版本不满足，未记录目标版本的导入、编辑模式、播放模式或六场景无头烟测通过结果；新增的 EditMode 测试资产也只能待目标环境执行。

### 当前状态

变更保持 `active`。需要 Unity 2022.3.40f1（或明确批准的固定替代版本）和有效许可证后，重新执行导入、编辑器/播放模式和 DOTS 真实分支验证；取得 Entitas、VetBrain、Mass 的实际包或程序集后再单独记录真实性能。
