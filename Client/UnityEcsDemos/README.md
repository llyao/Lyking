# Unity ECS 对比 Demo

这是一个独立的 Unity 2022.3.40f1 项目，用同一套移动实体模拟查看不同实体更新模型的代码形状和采样方法。

## 快速打开

1. 用 Unity Hub 添加 `Client/UnityEcsDemos/`。
2. 打开 `Assets/Scenes/00_GameObjectBaseline.unity`。
3. 运行后按 `F1` 到 `F6` 可切换六个后端场景；也可以直接在 Project 窗口打开对应场景。
4. 在 Inspector 选中场景里的 `EcsDemoBootstrap`，调整其中的 `benchmarkConfig`（实体数量、预热帧数和采样窗口）。运行时它会创建并配置 `BenchmarkRunner`。

默认不需要 DOTS、Entitas、Hydra VetBrain 或 Mass 包。未安装包时，相关场景会运行同一套兼容后端并在面板显示“兼容实现”。

## 场景清单

| 场景 | 重点 |
| --- | --- |
| `00_GameObjectBaseline` | GameObject API 形状基线；默认不创建 10,000 个真实对象 |
| `01_DotsEntities` | Unity Entities 的组件数据更新接口 |
| `02_DotsJobs` | 显式并行 Job 的更新路径 |
| `03_EntitasStyle` | Context、Group、系统链的组织方式 |
| `04_HydraVetBrain` | World、Aspect、Grouping 的适配边界 |
| `05_UnityMass` | Mass Entity 与大规模实体查询的入口 |

## 性能口径

面板显示实体数、最近采样窗口的平均/峰值更新耗时、主线程帧耗时、当前线程 GC 分配和后端状态。比较时请固定 Unity 补丁版本、实体数量、随机种子、分辨率、质量设置和构建模式。当前 GameObject、Entitas、VetBrain 和 Mass 场景默认都是确定性数组兼容实现；它们只用于阅读 API 形状和验证流程，不能替代真实第三方包或逐对象 MonoBehaviour 的性能结论。DOTS Entities 只有安装并启用对应包后才会进入真实分支。

详细接入步骤和适用性分析见 `Docs/benchmark-method.md`、`Docs/frameworks.md` 和 `Packages/README.md`。
