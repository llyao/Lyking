# 基于 Hydra 的通用游戏框架初始审查

## 发现

### 高：目标范围过大，不能直接进入实现

通用游戏框架同时涉及 Client、Server、Share、Tool、协议、运行时、内容、Unity 和后续存档。若直接照搬 Hydra 的全量目录，会把 ECS、网络、数据库、部署和玩法问题混在一次变更中，无法建立可靠的失败归因。方案已拆为共享契约、运行内核、三界投影、最小玩法、内容工具、Unity/Server 适配和交付验收七个阶段；未通过前一阶段门不得进入下一阶段。

### 高：原方案把 Hydra 的在线三界误放进通用内核

`MetaWorld`、`LiveWorld`、`ShowWorld` 是 Hydra 的在线 MMO 运行配置，不是所有游戏都需要的根抽象。若核心直接使用这些词，单机、回放、关卡编辑器、模拟测试和其他网络模型都会被迫适配角色、World Entry、RuntimeSlice 等产品语义。已调整为 `GameInstance`、`SimulationState`、`Command`、`Event`、`Projection`、`Module`、`HostAdapter` 七个通用核心对象；Hydra 三界下沉为 `HydraOnlineProfile`。

### 高：Hydra 路径与用户输入不一致

`E:\hydra\_git2` 不存在，当前可访问的是 `E:\hydra_git2\hydra`。路径差异可能对应不同版本或不同工作树。实现前必须重新确认源仓库身份、分支、提交和是否需要同时分析 `hydra-unity`。

### 高：Lyking 当前没有可执行游戏工程

当前仓库只有领域上下文、README 和 AiWorkflow 工具，没有 Unity 工程、服务端运行项目、共享协议程序集或游戏测试入口。因此本轮只能产出设计与计划，不能声称框架已落地或已通过运行时验证。

### 中：Hydra 文档存在目标架构与实际能力的边界

Hydra 的 MMO 框架、Character Save、容量和部分 World 合同明确写着“目标架构”或“未开放”。Lyking 计划必须把“设计合同”“代码行为”“验证证据”分开，不能把文档中的容量、SLO 或部署状态直接复制为验收结论。

### 中：生成代码、Unity canonical source 和内容产物容易漂移

Hydra 依赖表生成、Runtime Content、Mirror/TypeId 生成器、Unity 托管程序集和资源投影。Lyking 需要把 canonical source、producer、generated artifact 和 consumer 固定下来，门禁检查摘要和 revision，禁止手改生成物。

### 中：第一版持久化与网络选型尚未确认

Hydra 的 Character Save 使用 owner generation、Load Image、Append、Checkpoint、Save Barrier 和旧 fence 拒绝。Lyking 尚无同等基础设施。第一版应使用持久化 adapter 和内存实现验证接口，待产品需求明确后再选择数据库、缓存和传输实现。

### 中：原计划的“最小玩法”仍以 LocalLive/ShowWorld 命名

如果第一条验收链路依赖 `LocalLiveHost`、`ShowWorld` 或 `MetaWorld`，它验证的是 Hydra profile，而不是通用内核。计划已改为先验证 `LocalProfile` 的 Simulation → Projection → Presentation → Command 闭环，再验证 HydraOnline 的映射。

### 低：首个玩法切片仍有真实互斥选择

`MinimalAction` 和 `TurnBased` 会产生不同的时钟、状态收敛和回放验证范围。本计划将它们作为两个非 MMO 样例，但仍需用户确认是否作为首阶段验收切片后才能进入 `ready`。

## 开放问题

1. 使用哪一个 Hydra 工作树作为长期参考，是否需要锁定提交。
2. Lyking 首版是否接受“通用 LocalProfile 先行，AuthoritativeProfile 次之，HydraOnlineProfile 后置”的阶段顺序。
3. Unity 版本、目标平台、渲染管线和是否需要 IL2CPP/AOT。
4. 是否允许引入 VetBrain 或其他 ECS，还是先保持自有最小运行内核。
5. 首版持久化、网络传输和内容编辑器的外部依赖边界。
6. 首个玩法切片的验收截图、运行条件和性能阈值。

## 结论

当前方案可以作为受控目标的设计草稿，分析证据和阶段计划完整；由于上述互斥选择尚未确认，不能进入代码实施，也不能把变更状态标为 `verified`。

## 通用性复审追加

针对“框架必须服务不同类型游戏，而不是复刻 Hydra MMO”的要求，已完成以下方向修正：

1. 核心依赖改为 `Contracts → Kernel → Simulation → Profiles → Adapters` 单向 DAG，并把 Unity、网络、数据库、Hydra 和具体 ECS 加入禁止引用门禁。
2. 核心补充 `ISimulationClock`、`IRandomSource`、`IAuthorityModel`、`ISimulationSnapshot`、`IStateHasher` 和 `IReplayStream`，同时允许 fixed、variable、turn、lockstep 或 rollback 由 profile 选择。
3. 将 `CommandMetadata`、`EventMetadata`、`SnapshotMetadata` 分开，`WorldSessionKey`、`OwnerGeneration` 和网络身份只留在需要它们的 profile 边界。
4. 将 `ShowWorld` 降级为可选 `PresentationSession`；Headless、Web、Godot、Unity 和编辑器都可以只读消费 Projection。
5. 将 Player、Combat、Objective 等产品语义移到 Features/Samples，首阶段要求 `MinimalAction` 和 `TurnBased` 两个样例共享同一内核。
6. 增加 profile 兼容矩阵、跨 host 状态哈希、snapshot restore、replay divergence、headless、无持久化和替代前端验收项。

这些修正解决了通用性方面的高优先级问题，但不等于代码能力已实现。变更仍保持 `draft`，待用户确认阶段选择、Unity/AOT 基线和 ECS 策略后才能进入 `ready`。

## GameFrameX 组合复审追加

对 GameFrameX `main` 提交 `dbedb9f6d685e11b02f42b02141a69dadb10f655` 的只读分析结论如下：

1. GameFrameX 适合提供 Actor 调度、Gateway/Session、ProtoBuf/TCP/WebSocket/HTTP、MongoDB、Hotfix、Luban、监控、Docker 和多端客户端基础设施。
2. Hydra 适合提供 World/ECS 权威模拟、Command/Transfer、Projection、快照/回放、RuntimeContent、owner/generation 和复杂在线世界边界。
3. 两者可以组合，但不能把 Actor、`CacheState`、MongoDB、`Req/Resp/Notify` 或 `Player` 类型泄漏到通用核心；一个 Actor 可以拥有一个 Simulation/World Host，不能把每个 ECS Entity Actor 化。
4. GameFrameX ProtoBuf 与 Hydra MessagePack/MemoryPack/自定义帧存在协议栈重叠，必须保留核心消息语义并在 adapter 边界转换，不能让两套 wire contract 同时成为权威。
5. GameFrameX 的 MongoDB Player 状态和 Hydra 的 Character Save/snapshot/delta 不能同时拥有同一事实；长期事实和高频实时状态必须分开。
6. GameFrameX 聚合仓库每日同步、Unity/.NET 版本基线和许可证声明需要单独建立交付门禁；未完成许可证核查前不能把两套代码直接合并进闭源公共核心。
7. GameFrameX 的功能列表不能替代真实运行时证据；需要增加 Golden 协议、跨 profile 状态哈希、故障注入、运行模式和多端兼容测试。

结论：可以建设“通用核心 + GameFrameX 在线适配 + Hydra World 适配”的大部分游戏基础框架，但第一阶段必须先验证 Headless Simulation、GameFrameX adapter 和 Hydra adapter 的独立组合，再接入 Unity、MongoDB、Hotfix、Orleans 和生产部署。

## 可插拔库复审追加

“可插拔”不能只等同于运行时加载 DLL。方案已补充 `PluginAbstractions + PluginHost`：插件通过 `PluginId`、`ApiVersion`、`Capabilities`、依赖版本、状态命名空间、注册入口和生命周期入口声明自己；宿主在启动前校验禁止引用、依赖 DAG、TypeId、序列化版本和可热更边界。

插件分为 `SimulationModule`、`HostAdapter`、`PresentationAdapter` 和 `ToolPlugin`。Hydra、GameFrameX、Unity、Laya、Godot 和 MongoDB 都只能作为相应插件或 Profile 实现，不能反向成为 Kernel 依赖。插件启动按拓扑顺序，停止按逆拓扑顺序；必需插件失败拒绝启动，可选插件显式降级；核心、Contracts、Snapshot schema 和 Simulation 基础模块不允许任意热卸载。

## Lyking 模块宿主边界复核（2026-10-03）

当前 Share `PluginHost` 与 Client `ModuleHost` 解决不同问题：前者组合服务图和管理异步生命周期，后者还拥有 Unity 主线程帧泵。建议保留两个运行时 Host，只共享稳定模块身份/版本规则与兼容规范；等出现同一插件需要跨两者运行的真实需求，再建 adapter。

实施前需要澄清的合同风险：`ModuleDependency.Required` 目前没有影响默认验证；可选模块启动失败后仍会进入完整计划的 Drain/Stop 遍历；`CreateScope` 只管理 scoped 服务，不启动 scoped 模块；`StateNamespace` 尚无状态写入门禁；生命周期模块实例在 VContainer Apply 之前由调用方提供，因此不能对它本身使用容器构造注入。这些事项先作为后续模块合同验收，不在本轮扩大代码范围。

“可插拔”分层定义为：首版是编译期包/显式模块组合；运行时可创建服务 Scope；内容包可按原子发布合同切换；任意 DLL 动态加载和热卸载暂不支持。这样与 Unity/AOT、活动对象引用和状态迁移约束一致。
