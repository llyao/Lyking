# Unity ECS 性能对比 Demo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增一个可独立打开的 Unity 2022.3 ECS 对比项目，提供六个独立场景、统一基准统计和 DOTS/Entitas/Hydra VetBrain/Mass 的可选接入边界。

**Architecture:** 场景只装配启动器；启动器依赖统一的 `ISimulationBackend`；各后端在无第三方包时运行确定性的托管兼容实现，DOTS Entities/Jobs 安装包后可用条件编译切换真实分支，Entitas、VetBrain 和 Mass 本版保留适配占位。统一统计器记录实体数、平均/峰值更新耗时、帧耗时和 GC 分配，文档负责解释适用场景与性能证据边界。

**Tech Stack:** Unity 2022.3、C#、Unity Test Framework（可选）、Unity Entities 1.x（可选）、Entitas（可选）、Hydra VetBrain 程序集（可选）、Unity Mass（可选）。

---

### Task 1: 建立项目和变更工件

**Files:**
- Create: `Client/UnityEcsDemos/ProjectSettings/ProjectVersion.txt`
- Create: `Client/UnityEcsDemos/Packages/manifest.json`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/`
- Create: `Client/UnityEcsDemos/Assets/Scenes/`
- Create: `Client/UnityEcsDemos/README.md`
- Modify: `docs/changes/active/20261003-unity-ecs-demos/change.md`
- Modify: `docs/changes/active/20261003-unity-ecs-demos/evidence.md`

- [x] 创建 Unity 2022.3.40f1 项目最小目录，写入 Unity 内置模块和 Unity Test Framework 包清单。
- [x] 在 `change.md` 写入 `lyking-ai` 元数据、范围、非目标、验收、风险和回滚。
- [x] 在 `evidence.md` 记录已执行的路由、设计和计划文件创建动作，状态保持 `active`。
- [x] 运行 `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- check-workspace --change docs/changes/active/20261003-unity-ecs-demos`，确认范围外脏文件只作为警告。

### Task 2: 编写统一基准模型和统计器

**Files:**
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/BenchmarkConfig.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/BenchmarkEntity.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/BenchmarkMetrics.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/ISimulationBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/BenchmarkRunner.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Benchmark/BenchmarkPanel.cs`
- Create: `Client/UnityEcsDemos/Assets/Tests/EditMode/Editor/BenchmarkCoreTests.cs`

- [x] 先写纯 C# 可测试的实体积分函数，固定随机种子和边界反弹规则。
- [x] 让 `ISimulationBackend` 暴露后端名称、包状态、实体数、初始化、单步更新和释放方法。
- [x] 让 `BenchmarkMetrics` 使用 `Stopwatch` 和 `GC.GetAllocatedBytesForCurrentThread` 采样，并记录最近窗口平均值和峰值。
- [x] 运行兼容基准和源代码编译检查，确认零实体、边界反弹、固定步长和统计窗口行为。

### Task 3: 实现基线与兼容后端

**Files:**
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/GameObjectBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/DotsEntitiesBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/DotsJobsBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/EntitasStyleBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/HydraVetBrainBackend.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Backends/UnityMassBackend.cs`

- [x] 先实现基线后端，保证每个场景无第三方包即可运行。
- [x] 为 DOTS 后端加 `#if UNITY_ENTITIES` 分支，真实分支只引用 Entities 类型，兼容分支复用统一数组实现。
- [x] 为 Entitas、VetBrain 和 Mass 后端分别记录其 Context/Group、World/Aspect/Grouping、Entity/Query 的适配边界；本版明确标记为占位。
- [x] 各后端都使用同一随机种子和同一实体数量，避免比较时混入初始化差异。

### Task 4: 创建启动器和六个场景

**Files:**
- Create: `Client/UnityEcsDemos/Assets/Scripts/Scenes/EcsDemoBootstrap.cs`
- Create: `Client/UnityEcsDemos/Assets/Scripts/Scenes/CameraRig.cs`
- Create: `Client/UnityEcsDemos/Assets/Scenes/SceneBackendSelector.cs`
- Create: `Client/UnityEcsDemos/Assets/Scenes/SceneBackendSelector.cs.meta`
- Create: `Client/UnityEcsDemos/Assets/Scenes/00_GameObjectBaseline.unity`
- Create: `Client/UnityEcsDemos/Assets/Scenes/01_DotsEntities.unity`
- Create: `Client/UnityEcsDemos/Assets/Scenes/02_DotsJobs.unity`
- Create: `Client/UnityEcsDemos/Assets/Scenes/03_EntitasStyle.unity`
- Create: `Client/UnityEcsDemos/Assets/Scenes/04_HydraVetBrain.unity`
- Create: `Client/UnityEcsDemos/Assets/Scenes/05_UnityMass.unity`

- [x] 编写启动器，根据场景配置创建对应后端、面板和相机。
- [x] 每个场景只包含启动器和相机，面板运行时创建，禁止复制后端业务逻辑。
- [x] 为所有 Unity 资产生成同名 `.meta`，确保导入时 GUID 稳定。
- [x] 更新 Build Settings 场景列表，保证六个场景可逐个打开。

### Task 5: 编写框架接入与适用性文档

**Files:**
- Create: `Client/UnityEcsDemos/Docs/benchmark-method.md`
- Create: `Client/UnityEcsDemos/Docs/frameworks.md`
- Create: `Client/UnityEcsDemos/Packages/README.md`
- Modify: `Client/UnityEcsDemos/README.md`

- [x] 记录 Unity Entities、Entitas、Hydra VetBrain 和 Mass 的包/程序集接入位置、编译宏和当前未验证项。
- [x] 给出少量实体、同构大群体、跨 World 模拟、空间查询、复杂单体行为、编辑器工作流等适用/不适用情景。
- [x] 提供性能记录表，明确兼容后端结果不能代表第三方包结果。
- [x] 说明 Mass 可以在 Unity 中试用，但依赖具体 Unity 版本和包来源，不能与 DOTS Entities 的 API 直接互换。

### Task 6: 验证和证据

**Files:**
- Modify: `docs/changes/active/20261003-unity-ecs-demos/evidence.md`

- [ ] 运行 `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20261003-unity-ecs-demos`。
- [ ] 运行 `git diff --check`。
- [ ] 若 Unity 可执行文件可用，运行导入检查、编辑模式测试、运行模式测试和无头场景启动；记录实际命令、退出码、结果文件和警告。
- [ ] 没有真实包或固定编辑器证据时保持 `active`，不能把兼容后端的帧耗时写成 DOTS/Entitas/VetBrain/Mass 的结论。
