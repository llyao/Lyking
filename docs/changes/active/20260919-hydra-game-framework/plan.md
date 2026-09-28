# 基于 Hydra 的通用游戏框架实施计划

> **面向执行者：** 每个任务完成后先运行任务内的验证，再更新 `tasks.md` 和 `evidence.md`。设计未从 `draft` 进入 `ready` 前不得编写实现代码。

**目标：** 在 Lyking 建立一套与具体玩法、平台和部署方式解耦的通用游戏内核，再以 Hydra 的 World/Simulation 能力和 GameFrameX 的在线基础设施作为可选适配进行验证。

**架构：** 核心由 `GameInstance`、`SimulationState`、`Command`、`Event`、`Projection`、`Module`、`HostAdapter`、`ISimulationClock`、`ISnapshot`、`IStateHasher` 和 `IReplayStream` 组成；`PluginAbstractions` 和 `PluginHost` 负责受约束的插件发现、依赖排序、生命周期和诊断。Local、Authoritative、GameFrameX、HydraOnline 是运行配置，不进入核心对象模型。命令、事件和快照使用各自的元数据合同，只有 mutation boundary 强制幂等键。

**技术基线：** C#/.NET 10 仅作为工具和测试的暂定基线；核心运行时采用已验证的最小 API 基线，Unity 版本确认后再建立适配工程；二进制内容采用有版本的生成契约；网络与本地 transport 使用同一套命令接口；不在第一阶段引入完整 Hydra 基础设施。

---

## 文件与职责地图

| 阶段 | 新建或修改路径 | 职责 |
|---|---|---|
| 契约 | `Share/GameFramework.Contracts/` | 通用 ID、版本、命令、事件、投影和存档合同 |
| 内核 | `Share/GameFramework.Kernel/` | GameInstance、SimulationState、Module、帧和 owner 运行内核 |
| 插件 | `Share/GameFramework.PluginAbstractions/`、`Share/GameFramework.PluginHost/` | 插件描述、能力、依赖 DAG、生命周期和 Health |
| 模拟 | `Share/GameFramework.Simulation/` | 通用实体、组件、系统、状态机和可重放逻辑 |
| 运行配置 | `Share/GameFramework.Profiles/` | Local、Authoritative、HydraOnline 组合配置 |
| 样例与特性 | `Share/GameFramework.Samples/`、`Share/GameFramework.Features/` | MinimalAction、TurnBased 和可选玩法模块，不进入核心验收 |
| 服务端 | `Server/GameFramework/`、`Server/GameFrameXAdapter/` | Server Host、权威适配、GameFrameX Actor/网络/持久化 adapter |
| 客户端 | `Client/GameFramework/` | Local Host、Projection、Session 和表现 adapter |
| Unity | `Client/Unity/Assets/CLR/GameFramework/` | View、输入、资源、场景和表现适配 |
| 工具 | `Tool/GameFramework/` | schema、编译器、manifest、快照和诊断工具 |
| 测试 | 各模块 `Tests/` | 契约、状态机、顺序、幂等、生命周期和垂直链路测试 |
| 变更记录 | `docs/changes/active/20260919-hydra-game-framework/` | 设计、计划、任务、审查、证据和状态 |

## Task 0：确认目标和建立受控变更

**文件：**

- 修改：`docs/changes/active/20260919-hydra-game-framework/change.md`
- 修改：`docs/changes/active/20260919-hydra-game-framework/design.md`
- 修改：`docs/changes/active/20260919-hydra-game-framework/tasks.md`

- [ ] **步骤 1：确认四个架构选择。** 记录 LocalProfile 首切片、两个样例、Unity 版本和 ECS 策略；没有确认的选择保留为 `draft`。Hydra 三界只作为后续 profile，不作为首切片前置条件。
- [ ] **步骤 2：运行路由检查。**

运行：

```powershell
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "落地基于Hydra的通用游戏框架" --path Client --path Server --path Share --path Tool --path docs
```

预期：输出 `primaryArea=Client`，相关领域包含 `Server`、`Share`、`Tool`，风险包含 `shared-contract` 和 `multi-area`，建议档位为 `controlled`。

- [ ] **步骤 3：将变更状态从 `draft` 改为 `ready`。** 只在用户确认互斥选择、范围和非目标后进行。

## Task 1：建立共享身份与版本契约

**文件：**

- 新建：`Share/GameFramework.Contracts/GameFramework.Contracts.csproj`
- 新建：`Share/GameFramework.Contracts/Identity/GameIdentity.cs`
- 新建：`Share/GameFramework.Contracts/Identity/InstanceId.cs`
- 新建：`Share/GameFramework.Contracts/Identity/SubjectId.cs`
- 新建：`Share/GameFramework.Contracts/Identity/RuntimeGeneration.cs`
- 新建：`Share/GameFramework.Contracts/Protocol/CommandMetadata.cs`
- 新建：`Share/GameFramework.Contracts/Protocol/EventMetadata.cs`
- 新建：`Share/GameFramework.Contracts/Protocol/SnapshotMetadata.cs`
- 新建：`Share/GameFramework.Contracts/Protocol/OperationReceipt.cs`
- 新建：`Share/GameFramework.Contracts/Serialization/SchemaVersion.cs`
- 新建：`Share/GameFramework.Contracts/Validation/DependencyRules.cs`
- 新建：`Share/GameFramework.Contracts.Tests/IdentityContractTests.cs`
- 新建：`Share/GameFramework.Contracts.Tests/ProtocolContractTests.cs`
- 新建：`Share/GameFramework.Contracts.Tests/DependencyRulesTests.cs`

- [ ] **步骤 1：定义不可互换的通用 ID。** `InstanceId`、`SubjectId`、`EntityId`、`CommandId`、`EventSequence`、`ContentRevision` 和 `RuntimeGeneration` 使用独立类型；`CharacterId`、`WorldSessionKey`、`NetId` 只在 profile 或 adapter 中定义。
- [ ] **步骤 2：拆分消息元数据。** `CommandMetadata` 只描述意图来源和输入序列；`EventMetadata` 只描述事实顺序和逻辑时间；`SnapshotMetadata` 只描述恢复基线、内容版本和状态哈希。只有 mutation boundary 才要求 `OperationId`。
- [ ] **步骤 3：为新增字段建立追加规则。** 序列化字段只能追加；未知字段可安全忽略；缺少当前边界必需 identity 时返回结构化拒绝。
- [ ] **步骤 4：写失败测试和编译门禁。** 覆盖 ID 互换不能编译、核心禁止引用 Unity/网络/数据库/Hydra、旧 generation 被拒绝、重复 operation 返回同一 receipt、未知 schema 被拒绝。
- [ ] **步骤 5：运行测试。**

运行：

```powershell
dotnet test Share/GameFramework.Contracts.Tests/GameFramework.Contracts.Tests.csproj
```

预期：所有契约、版本、元数据边界、依赖规则和幂等测试通过。

## Task 2：建立通用 Simulation 内核

**文件：**

- 新建：`Share/GameFramework.Kernel/GameFramework.Kernel.csproj`
- 新建：`Share/GameFramework.Kernel/GameInstance.cs`
- 新建：`Share/GameFramework.Kernel/SimulationState.cs`
- 新建：`Share/GameFramework.Kernel/SimulationFrame.cs`
- 新建：`Share/GameFramework.Kernel/Module.cs`
- 新建：`Share/GameFramework.Kernel/HostAdapter.cs`
- 新建：`Share/GameFramework.Kernel/Authority/IAuthorityModel.cs`
- 新建：`Share/GameFramework.Kernel/Time/ISimulationClock.cs`
- 新建：`Share/GameFramework.Kernel/Random/IRandomSource.cs`
- 新建：`Share/GameFramework.Kernel/Snapshot/ISimulationSnapshot.cs`
- 新建：`Share/GameFramework.Kernel/Snapshot/IStateHasher.cs`
- 新建：`Share/GameFramework.Kernel/Replay/IReplayStream.cs`
- 新建：`Share/GameFramework.Simulation/GameFramework.Simulation.csproj`
- 新建：`Share/GameFramework.Simulation/Entity/EntityIdAllocator.cs`
- 新建：`Share/GameFramework.Simulation/Entity/ComponentStore.cs`
- 新建：`Share/GameFramework.Simulation/Execution/SimulationOwner.cs`
- 新建：`Share/GameFramework.Simulation/Events/OrderedEventQueue.cs`
- 新建：`Share/GameFramework.Simulation/Commands/OwnerCommandQueue.cs`
- 新建：`Share/GameFramework.Simulation/Lifecycle/SimulationStateMachine.cs`
- 新建：`Share/GameFramework.Simulation.Tests/SimulationTests.cs`
- 新建：`Share/GameFramework.Simulation.Tests/OrderedQueueTests.cs`
- 新建：`Share/GameFramework.Simulation.Tests/StateMachineTests.cs`

- [ ] **步骤 1：固定执行阶段而非固定时间模型。** `Input → Command → Module Systems → Projection → Output` 顺序由 `GameInstance.Step()` 拥有；步长、回合号和推进策略由 `ISimulationClock` 决定，业务模块不能读取宿主墙钟或自行推进时间。
- [ ] **步骤 2：实现 owner 串行。** 同一 owner 的 command/event 按 FIFO 消费；跨线程只入队；队列满时返回可观察失败，不静默丢弃 terminal message。
- [ ] **步骤 3：实现状态、随机和快照。** 状态容器只负责实体、组件和变更标记；快照必须可恢复，随机数必须可记录，状态哈希必须独立于宿主容器顺序。
- [ ] **步骤 4：实现可扩展生命周期。** 基础状态提供 `Opening`、`Active`、`Closing`、`Closed`，profile 可以注册 `Paused`、`Replaying`、`Migrating` 等扩展状态；非法迁移和关闭失败保持可诊断状态。
- [ ] **步骤 5：实现回放和权威策略。** 至少支持 local、server-authoritative、turn 三种 policy；rollback/lockstep 可以通过 profile 接入，不把任何一种同步模型写死在核心。
- [ ] **步骤 6：测试顺序、确定性和失败语义。** 覆盖事件先后、重复 command、过期 generation、关闭后输入拒绝、queue overflow、snapshot restore、跨 host state hash 和 replay divergence。
- [ ] **步骤 7：运行测试。**

运行：

```powershell
dotnet test Share/GameFramework.Simulation.Tests/GameFramework.Simulation.Tests.csproj
```

预期：所有运行内核测试通过，且没有 Unity、网络、数据库、Hydra 或具体 ECS 依赖。

## Task 3：实现通用 Projection 与运行配置

**文件：**

- 新建：`Share/GameFramework.Simulation/Projection/ProjectionRoute.cs`
- 新建：`Share/GameFramework.Simulation/Projection/ProjectionSnapshot.cs`
- 新建：`Share/GameFramework.Simulation/Projection/ProjectionDelta.cs`
- 新建：`Share/GameFramework.Simulation/Projection/ProjectionApplier.cs`
- 新建：`Share/GameFramework.Profiles/LocalProfile.cs`
- 新建：`Share/GameFramework.Profiles/AuthoritativeProfile.cs`
- 新建：`Share/GameFramework.Profiles/HydraOnlineProfile.cs`
- 新建：`Share/GameFramework.Profiles/ProfileCompatibilityMatrix.md`
- 新建：`Share/GameFramework.Simulation.Tests/ProjectionTests.cs`
- 新建：`Share/GameFramework.Profiles.Tests/ProfileCompatibilityTests.cs`
- 新建：`Client/GameFramework/Presentation/PresentationSession.cs`（可选 adapter）
- 新建：`Client/GameFramework/Presentation/Presenter.cs`（可选 adapter）
- 新建：`Client/GameFramework/Tests/PresentationSessionTests.cs`（仅验证 adapter）

- [ ] **步骤 1：定义通用投影方向。** 只允许 `Authority→Projection` 和 `Projection→Presentation`；禁止 Presentation 反写模拟状态。
- [ ] **步骤 2：定义投影快照和增量。** 载荷包含 instance、generation、sequence 和 source entity；不使用“当前实例”猜测目标。
- [ ] **步骤 3：实现 Local 和 Authoritative 配置。** 两者调用相同的 `GameInstance.Step()`、snapshot 和 hash 合同，差异通过 authority/host adapter 注入。
- [ ] **步骤 4：实现 HydraOnline 配置。** 仅在 profile 层映射 `Meta→Live`、`Live→Show`、`RuntimeSlice` 和 Hydra identity，不污染通用内核；`ShowWorld` 不作为核心启动条件。
- [ ] **步骤 5：增加 headless 和替代前端验收。** 至少让 headless read model、无持久化 profile 和一个非 Unity presentation adapter 通过同一 projection 合同。
- [ ] **步骤 6：测试迟到和旧实例。** 覆盖 baseline 先于 delta、旧 instance drop、重复 projection 幂等、关闭实例拒绝新输入和 host detach 清理。
- [ ] **步骤 7：运行 shared/server/client 测试。**

运行：

```powershell
dotnet test Share/GameFramework.Simulation.Tests/GameFramework.Simulation.Tests.csproj
dotnet test Server/GameFramework.Tests/GameFramework.Tests.csproj
dotnet test Client/GameFramework.Tests/GameFramework.Tests.csproj
```

预期：通用投影和运行配置测试通过，HydraOnline 只是组合层，PresentationSession 是可选 adapter，服务端和客户端没有相互引用实现程序集。

## Task 3A：接入 GameFrameX 在线基础设施（可选适配）

**文件：**

- 新建：`Server/GameFrameXAdapter/GameFrameXAdapter.csproj`
- 新建：`Server/GameFrameXAdapter/Hosting/GameFrameXActorHostAdapter.cs`
- 新建：`Server/GameFrameXAdapter/Transport/GameFrameXTransportAdapter.cs`
- 新建：`Server/GameFrameXAdapter/Persistence/GameFrameXPersistenceAdapter.cs`
- 新建：`Server/GameFrameXAdapter/Hotfix/GameFrameXHotfixBridge.cs`
- 新建：`Server/GameFrameXAdapter/Tests/GameFrameXAdapterTests.cs`
- 修改：`Tool/GameFramework/Validation/DependencyRulesValidator.cs`

- [ ] **步骤 1：固定适配边界。** GameFrameX 只提供 Actor 调度、Gateway/Session、ProtoBuf 编解码、MongoDB、Hotfix、监控和部署能力；核心项目不得引用 `GameFrameX.*`、`CacheState`、`Req/Resp/Notify` 或 MongoDB 类型。
- [ ] **步骤 2：组合 Actor 与 Simulation。** 一个 GameFrameX Actor 只拥有一个 `SimulationInstance`、World Host 或低频应用状态；禁止为每个 ECS Entity 创建 Actor，禁止让 Actor 队列改变 Simulation 的确定性语义。
- [ ] **步骤 3：实现协议转换。** GameFrameX ProtoBuf/TCP/WebSocket/HTTP 只在边界转换为核心 `Command/Event/Transfer/Snapshot`；协议字段、模块号和 `Req/Resp/Notify` 命名不能渗透到核心 API。
- [ ] **步骤 4：实现持久化转换。** Account、Character、Inventory 等长期事实可以映射 MongoDB；高频位置、物理和战斗状态只能通过 snapshot、delta、checkpoint 或结算事实保存，不能每 Tick 写 MongoDB。
- [ ] **步骤 5：定义热更新门。** Hotfix 只能替换应用 handler 和非核心模块；核心契约、状态 schema、Simulation 程序集和快照格式变更必须带版本、兼容窗口、回放验证和回滚路径。
- [ ] **步骤 6：验证无适配运行。** `MinimalAction` 和 `TurnBased` 必须在没有 MongoDB、GameFrameX 网络和 Hotfix 的 Headless 模式下通过；接入 GameFrameX 后状态哈希和回放结果保持一致。

## Task 3B：建立可插拔插件合同和宿主

> 2026-09-26 进展：模块管理中间件第一版已落地（用户确认 VContainer 物化，见 `design.md` 3.9 节）。已完成：稳定插件合同（`ModuleDescriptor`/`IGameModule`）、依赖 DAG 校验（环、缺失、版本、重复 Id、能力冲突、命名空间冲突、重复服务）、贡献中间表示 + VContainer 物化器、生命周期宿主（依赖正序启动、必需失败逆序回滚、可选降级、Drain、逆序停止、Health）、22 个 headless 测试。未完成：文件 manifest 加载、稳定 TypeId 注册表、热更边界、插件缺失降级策略的 manifest 层面表达——归属本任务后续步骤与运行内核阶段。

**文件：**

- 新建：`Share/GameFramework.PluginAbstractions/GameFramework.PluginAbstractions.csproj`
- 新建：`Share/GameFramework.PluginAbstractions/PluginDescriptor.cs`
- 新建：`Share/GameFramework.PluginAbstractions/PluginCapabilities.cs`
- 新建：`Share/GameFramework.PluginAbstractions/IGamePlugin.cs`
- 新建：`Share/GameFramework.PluginAbstractions/PluginRegistry.cs`
- 新建：`Share/GameFramework.PluginHost/GameFramework.PluginHost.csproj`
- 新建：`Share/GameFramework.PluginHost/PluginManifestLoader.cs`
- 新建：`Share/GameFramework.PluginHost/PluginDependencyResolver.cs`
- 新建：`Share/GameFramework.PluginHost/PluginLifecycleHost.cs`
- 新建：`Share/GameFramework.PluginHost/PluginHealthRegistry.cs`
- 新建：`Share/GameFramework.PluginHost.Tests/PluginHostTests.cs`
- 新建：`Share/GameFramework.PluginHost.Tests/PluginCompatibilityTests.cs`

- [ ] **步骤 1：定义稳定插件合同。** `PluginId`、`ApiVersion`、`Capabilities`、`Dependencies`、`StateNamespace`、注册入口和生命周期入口属于公共合同；合同不得引用平台和产品类型。
- [ ] **步骤 2：实现 manifest 和依赖 DAG。** 读取插件版本、能力、依赖、可热更声明和禁止引用规则；依赖缺失、版本不兼容、循环依赖和重复 `PluginId` 必须在启动前失败。
- [ ] **步骤 3：实现注册隔离。** 插件只能注册命令、事件、系统、Projection、编解码和 capability port，不能修改核心帧循环、全局时钟、全局随机源或其他插件状态命名空间。
- [ ] **步骤 4：实现生命周期和 Health。** 按拓扑顺序启动，按逆拓扑顺序 Drain/Stop；必需插件失败则拒绝启动，可选插件进入显式降级状态。
- [ ] **步骤 5：实现版本和热更边界。** Core、Contracts、Snapshot schema 和 Simulation 基础模块不能任意热卸载；只有声明为可热更的 Feature/Tool 插件可以在安全点替换，并且必须有版本回退。
- [ ] **步骤 6：测试插件合同。** 覆盖依赖排序、循环依赖、缺失可选插件、状态命名空间冲突、稳定 TypeId、启动失败清理、Drain 顺序和热更回退。

## Task 4：用两个非 MMO 样例验证通用性

**文件：**

- 新建：`Share/GameFramework.Samples/MinimalAction/MinimalActionModule.cs`
- 新建：`Share/GameFramework.Samples/MinimalAction/MinimalActionTests.cs`
- 新建：`Share/GameFramework.Samples/TurnBased/TurnBasedModule.cs`
- 新建：`Share/GameFramework.Samples/TurnBased/TurnBasedTests.cs`
- 新建：`Share/GameFramework.Samples/SampleReplayTests.cs`
- 新建：`Share/GameFramework.Features/Combat/`（仅在用户确认需要时建立）

- [ ] **步骤 1：实现 `MinimalAction`。** 一个无账号、无存档、无网络的 action command，验证 Entity、Command、Event、Projection、variable/fixed clock 和 replay。
- [ ] **步骤 2：实现 `TurnBased`。** 一个回合行动和结算样例，验证 turn clock、不同 authority policy、snapshot restore 和回合结束事实。
- [ ] **步骤 3：验证同一核心。** 两个样例不得修改 `Kernel` 或 `Simulation` 的核心循环，不得引用 Player、Combat、Quest、MetaWorld 或 ShowWorld。
- [ ] **步骤 4：实现 LocalProfile loopback。** 两个样例都通过同一 command contract 接入 LocalProfile，再用 AuthoritativeProfile 替换 transport 和 authority。
- [ ] **步骤 5：运行垂直链路测试。** 测试配置加载、样例状态收敛、projection、重复 command、snapshot/replay、跨 host 状态哈希和 headless 输出。

## Task 5：建立配置表和 Runtime Content 生产链

**文件：**

- 新建：`Tool/GameFramework/Schema/GameTableSchema.cs`
- 新建：`Tool/GameFramework/Schema/GameTableCompiler.cs`
- 新建：`Tool/GameFramework/Content/AuthoringRecord.cs`
- 新建：`Tool/GameFramework/Content/RuntimeDeliveryRecord.cs`
- 新建：`Tool/GameFramework/Content/RuntimeContentManifest.cs`
- 新建：`Tool/GameFramework/Content/RuntimeContentCompiler.cs`
- 新建：`Tool/GameFramework/Content/CurrentProjection.cs`
- 新建：`Share/GameFramework.Simulation/Content/ITableSnapshot.cs`
- 新建：`Share/GameFramework.Simulation/Content/IRuntimeContentCatalog.cs`
- 新建：`Tool/GameFramework.Tests/TableCompilerTests.cs`
- 新建：`Tool/GameFramework.Tests/RuntimeContentCompilerTests.cs`

- [ ] **步骤 1：定义 source/schema/contract/bytes/snapshot 五层。** 运行时只读取校验后的 snapshot 和 catalog，不读编辑器 JSON；Luban 负责表格族，Hydra compiler 负责图、技能、时间线等其他内容族。
- [ ] **步骤 2：实现确定性编译。** 同一输入、工具版本和 schema 必须生成相同内容哈希；editor-only 字段不进入 delivery record。
- [ ] **步骤 3：实现统一 manifest 和 current projection。** manifest 记录 pack identity、content revision、schema revision、source hash、payload hash 和完整性摘要；Luban 与 Hydra 不能生成同一内容族；current 只是可变指针。
- [ ] **步骤 4：实现 prepare/commit。** 先完整准备并校验，再在安全点一次发布；失败不得发布半套内容。
- [ ] **步骤 5：测试旧版本和回滚。** 覆盖字段追加、未知字段、内容哈希、manifest 不一致、current 回滚和失败后再次刷新。
- [ ] **步骤 6：运行工具测试。**

运行：

```powershell
dotnet test Tool/GameFramework.Tests/GameFramework.Tests.csproj
```

预期：编译、manifest、快照切换和回滚测试通过。

## Task 6：接入可选目标模块和持久事实边界

**文件：**

- 新建：`Share/GameFramework.Features/Objective/ObjectiveContracts.cs`
- 新建：`Share/GameFramework.Features/Objective/ObjectiveRuntime.cs`
- 新建：`Server/GameFramework/Objective/MetaObjectiveService.cs`
- 新建：`Server/GameFramework/Persistence/IPersistenceAdapter.cs`
- 新建：`Server/GameFramework/Persistence/InMemoryPersistenceAdapter.cs`
- 新建：`Server/GameFramework.Tests/ObjectiveRuntimeTests.cs`
- 新建：`Server/GameFramework.Tests/PersistenceBoundaryTests.cs`

- [ ] **步骤 1：定义 ObjectiveDefinition、ObjectiveEvent、ObjectiveInstance、ObjectiveScope、ProgressRule 和 CompletionReceipt。**
- [ ] **步骤 2：禁止业务直接写进度。** 业务只能发布事实事件，目标运行时负责匹配、累加、完成和回调。
- [ ] **步骤 3：实现最小持久化 adapter。** 先把 Simulation 的可保存事实抽象成 Load、Append、Checkpoint、Generation 校验；内存实现只用于测试，MetaWorld 只作为 HydraOnline 的一种 owner 映射。
- [ ] **步骤 4：测试重复事件、旧 generation、完成回调和失败重放。** 未有真实存储实现时不得声称持久化已完成。

## Task 7：建立 Unity 表现和资源适配层

**前置：** Task 0 已确认 Unity 版本和平台。

**文件：**

- 新建：`Client/Unity/Assets/CLR/GameFramework/Runtime/WorldRuntimeHost.cs`
- 新建：`Client/Unity/Assets/CLR/GameFramework/Runtime/ShowWorldPresenter.cs`
- 新建：`Client/Unity/Assets/CLR/GameFramework/Runtime/ShowWorldBinder.cs`
- 新建：`Client/Unity/Assets/CLR/GameFramework/Runtime/InputCommandAdapter.cs`
- 新建：`Client/Unity/Assets/CLR/GameFramework/Runtime/ResourceCatalogAdapter.cs`
- 新建：`Client/Unity/Assets/Tests/Runtime/GameFrameworkUnityTests.cs`

- [ ] **步骤 1：建立 Unity Host。** Host 只负责 attach/detach、Unity 帧和 View/Actor 投影，不拥有 ShowWorld 业务系统。
- [ ] **步骤 2：接入 Presenter/Binder。** Presenter 只读 ShowWorld read model；Binder 负责 View 实例的创建、更新和释放。
- [ ] **步骤 3：接入输入和资源。** 输入转为 command，资源加载通过可替换 adapter，缺资源只在表现边界降级并保留诊断。
- [ ] **步骤 4：运行 Unity 编辑模式和运行模式测试。** 覆盖登录、Live slice 切换、登出清理、玩家可见可操作和旧实体不残留。

## Task 8：建立可替换 Server Host 和网络入口

**文件：**

- 新建：`Server/GameFramework/Hosting/ServerWorldHost.cs`
- 新建：`Server/GameFramework/Hosting/ServerCommandIngress.cs`
- 新建：`Server/GameFramework/Hosting/ServerProjectionEgress.cs`
- 新建：`Server/GameFramework/Hosting/ServerReadiness.cs`
- 新建：`Server/GameFramework/Tests/ServerHostLifecycleTests.cs`
- 新建：`Share/GameFramework.Contracts/Transport/TransportReceipt.cs`

- [ ] **步骤 1：先实现进程内 host。** Server Host 只负责启动 shared runtime、输入门、输出门和健康状态。
- [ ] **步骤 2：实现 transport adapter 接口。** 本地 loopback、WebSocket/HTTP2 或其他协议都必须实现同一 command/event/receipt contract。
- [ ] **步骤 3：实现 readiness、drain 和 stop。** `Serving → Draining → Stopped`；drain 后拒绝新会话，已有 owner 继续收敛并生成保存 barrier。
- [ ] **步骤 4：测试命令鉴权、重复进入、旧 WorldSessionKey、drain 期间输入和 stop 前保存证明。**

## Task 9：工具门禁、文档索引和观测

**文件：**

- 新建：`Tool/GameFramework/Validation/GameFrameworkValidator.cs`
- 新建：`Tool/GameFramework/Validation/ContentFreshnessValidator.cs`
- 新建：`Tool/GameFramework/Validation/DependencyRulesValidator.cs`
- 新建：`Tool/GameFramework/Diagnostics/RunArtifactWriter.cs`
- 修改：`Tool/AiWorkflow/Routing/Router.cs`
- 新建：`Tool/AiWorkflow/Tests/GameFrameworkRouteTests.cs`
- 修改：`docs/changes/active/20260919-hydra-game-framework/evidence.md`

- [ ] **步骤 1：把游戏框架路径加入 route 候选。** 识别 `Client/GameFramework`、`Server/GameFramework`、`Share/GameFramework`、`Tool/GameFramework` 和 `Share/GameFramework.Samples`。
- [ ] **步骤 2：实现依赖规则检查。** 读取项目引用和命名空间，拒绝 Core 对 Unity、网络、数据库、Hydra、Client、Server、Tool 和 Samples 的反向引用。
- [ ] **步骤 3：实现生成物新鲜度检查。** 检查 source digest、schema revision、content revision、manifest 和 current projection，不用修改时间推断成功。
- [ ] **步骤 4：输出结构化 run artifact。** 记录命令、版本、输入摘要、退出码、产物摘要和失败原因。
- [ ] **步骤 5：运行 AiWorkflow 测试。**

运行：

```powershell
dotnet test Tool/AiWorkflow/Tests/Lyking.AiWorkflow.Tests.csproj
```

预期：原有工具测试与新增路由、依赖门禁和生成物检查同时通过。

## Task 10：受控目标验收、独立审查和状态转换

**文件：**

- 修改：`docs/changes/active/20260919-hydra-game-framework/tasks.md`
- 修改：`docs/changes/active/20260919-hydra-game-framework/evidence.md`
- 修改：`docs/changes/active/20260919-hydra-game-framework/review.md`
- 修改：`docs/changes/active/20260919-hydra-game-framework/change.md`

- [ ] **步骤 1：运行共享契约、运行时、样例、服务端、客户端和工具测试。** 保存日期、命令、退出码、通过/失败数量和关键输出。
- [ ] **步骤 2：运行 headless 垂直链路。** 至少验证 `MinimalAction` 和 `TurnBased` 的创建、step/turn、命令、projection、snapshot、replay、重复操作和关闭恢复，不要求玩家、ShowWorld 或持久化。
- [ ] **步骤 3：运行跨 profile 兼容矩阵。** 用相同初始快照、时钟、随机种子和命令序列比较 LocalProfile 与 AuthoritativeProfile 的状态哈希；HydraOnline 另验 Meta/Live/Show 映射。
- [ ] **步骤 4：运行依赖和替代前端门禁。** 证明核心无 Unity、网络、数据库和 Hydra 引用，并让至少一个非 Unity presentation adapter 消费 projection。
- [ ] **步骤 5：由不同执行上下文完成只读审查。** 检查所有权、过期身份、失败清理、协议兼容、生成物来源、Profile 边界和测试充分性。
- [ ] **步骤 6：处理所有阻塞发现。** 高严重度发现未关闭前保持 `active`；工具或环境缺口要写入证据，不以结构检查代替真实验证。
- [ ] **步骤 7：仅在验收证据齐全后转换 `active:verified`。** 之后再依据用户授权决定提交、推送或合并。

## 阶段门

| 阶段 | 进入条件 | 退出条件 |
|---|---|---|
| 设计 | 路由和 Hydra 分析完成 | 用户确认四个互斥选择，变更为 `ready` |
| 契约 | 设计为 `ready` | 通用 ID、分层元数据、版本、依赖规则和兼容测试通过 |
| 通用内核 | 契约可用 | owner、FIFO、可选时钟、authority、快照、哈希、回放和生命周期测试通过 |
| 通用样例 | 通用内核可用 | `MinimalAction` 与 `TurnBased` 通过同一核心和 LocalProfile headless 验收 |
| 插件宿主 | 通用内核可用 | manifest、依赖 DAG、生命周期、Health、降级和卸载测试通过 |
| Profile 兼容 | 通用样例可用 | Local/Authoritative 状态哈希一致，替代 presentation adapter 通过 |
| Hydra 配置 | Profile 兼容可用 | HydraOnline 的 Meta/Live/Show 映射测试通过，且不污染核心依赖 |
| 内容工具 | 玩法契约稳定 | 编译、manifest、快照和回滚测试通过 |
| Unity/Server | 垂直链路稳定 | 真实 host、表现和入口测试通过 |
| 交付验收 | 所有阶段完成 | 独立审查通过、证据新鲜、状态可转 `verified` |

## 回滚顺序

1. 关闭新功能 gate，停止新内容和新 route 进入。
2. 恢复上一个兼容的代码与生成物版本。
3. 回退 current projection 和 manifest；不直接删除 source 或持久数据。
4. 对未完成的 Simulation/Presentation 运行实例执行确定性 drain 和 cleanup。
5. 只有经过显式数据迁移设计和授权，才回滚或迁移持久化 schema。
