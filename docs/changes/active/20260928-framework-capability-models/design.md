# 基础框架能力模型体系设计

> 设计状态：草稿。本文是 `20260919-hydra-game-framework/design.md`（下称"顶层设计"）第 3、4 章结论的能力化重组：把散落在各章节的能力结论整理为有序的能力地图和统一格式的能力模型卡。口径冲突时以顶层设计为权威；本文只做索引、补全和细化，不推翻既有决策。
>
> **逐能力详细设计**见 [drafts/](drafts/README.md)：19 份草稿（CAP-01~12、14~20）每份含合同形状、设计决策与审核决策点，是本图的展开与实施依据。

## 1. 设计定位

框架 = 一组可独立验收的**基础能力**，按依赖顺序逐个落地。每个能力回答四件事：

1. **模型**：核心对象、合同形状、关键不变量。
2. **失败语义**：出错时怎么办，在哪个阶段暴露。
3. **使用方案**：在不同情况下（用况 U1–U4）怎么用、差异放在哪一层。
4. **验收证据**：用什么测试和演示证明能力存在。

差异 placement 原则：**核心不含情况分支**。单机、联机、测试的差异由 Profile（能力域 C）组合能力实现，能力合同本身不感知 Profile。

## 2. 继承的设计原则

以下原则来自顶层设计 3.8、3.9、4.2、6 节与 `20260928-module-manager-tidy`，是全部能力卡的公共前提：

1. 事件是事实，命令是意图，快照是状态；三者不互相冒充。
2. 一个事实只有一个 owner；镜像、快照和表现状态不反向成为权威。
3. 生命周期固定为 `Describe → Contribute → Plan → Validate → Apply → Start → Ready → Drain → Stop → Dispose`；启动按依赖正序，停止按逆拓扑序；Ready 是宿主状态，不是模块回调阶段。
4. 模块之间只用构造函数注入 + Port/Facade + Command/Event；禁止服务定位器和静态可变单例。
5. `StateNamespace` 全局唯一，能力冲突在 Validate 阶段失败，禁止后注册覆盖。
6. 核心程序集禁止引用 Unity、网络库、数据库 SDK 与具体 ECS；依赖方向由编译门禁保证。
7. 热路径禁止反射、无界分配与 LINQ；性能例外要有可重复基准。

## 3. 能力地图

五个能力域按依赖顺序排列（另设 F/G 两个平台核心域：F 客户端、G 服务器，承接核心能力在两侧平台的通用模块形态）。A 域是数据基础，B 域是有序模拟，C 域承载情况差异，D 域处理外部副作用，E 域保障长期运行。

| 编号 | 能力 | 所属工程 | 前置 | 状态 |
|---|---|---|---|---|
| CAP-00 | 模块管理与组合 | PluginAbstractions / PluginHost / .VContainer | — | 已落地（verified） |
| **A 契约与身份** | | | | |
| CAP-01 | 身份模型 | Contracts | CAP-00 | 已定义未落地 |
| CAP-02 | 消息元数据分层 | Contracts | CAP-01 | 已定义未落地 |
| CAP-03 | 序列化与 schema 演进 | Contracts | CAP-01、CAP-02 | 未定义细案 |
| **B 运行内核** | | | | |
| CAP-04 | 时钟模型 | Kernel | CAP-01 | 已定义未落地 |
| CAP-05 | 可记录随机源 | Kernel | CAP-01 | 已定义未落地 |
| CAP-06 | 状态容器与命名空间 | Simulation | CAP-01、CAP-03 | 未定义细案 |
| CAP-07 | 命令与事件管线 | Simulation | CAP-02、CAP-06 | 未定义细案 |
| CAP-08 | 权威模型 | Simulation | CAP-07 | 已定义未落地 |
| CAP-09 | 快照与状态哈希 | Simulation | CAP-06 | 已定义未落地 |
| CAP-10 | 回放 | Simulation | CAP-07、CAP-09 | 已定义未落地 |
| **C 运行配置** | | | | |
| CAP-11 | LocalProfile | Profiles | B 域全部 | 未定义细案 |
| CAP-12 | AuthoritativeProfile | Profiles | CAP-08、CAP-16 | 未定义细案 |
| CAP-13 | HydraOnlineProfile | Profiles | CAP-12、CAP-18 | 顶层后置 |
| **D 平台适配** | | | | |
| CAP-14 | 资源租约 | Adapters 合同 | CAP-00 | 形状已定（顶层 3.8） |
| CAP-15 | 持久化 | Adapters | CAP-09、CAP-03 | 未定义细案 |
| CAP-16 | 网络传输 | Adapters | CAP-02 | 未定义细案 |
| CAP-17 | 表现会话 | Adapters 合同 | CAP-07 | 边界已定（顶层 4.5） |
| CAP-18 | 内容管线 | Tool + Adapters | CAP-03、CAP-09 | 未定义细案 |
| **E 运维能力** | | | | |
| CAP-19 | 健康与降级 | PluginHost（扩展） | CAP-00 | 启动期已落地，运行期未定义 |
| CAP-20 | 热更边界 | PluginHost + Profiles | CAP-00、CAP-18 | 边界已定，安全点未定义 |
| **F 客户端核心模块** | | | | |
| CAP-21 | 客户端宿主（平台桥、帧泵、前后台策略） | Adapters.Client | CAP-00、04、11 | 未定义细案 |
| CAP-22 | UI 框架（栈/层级/数据流纪律） | Adapters.Client | CAP-14、17、21 | 未定义细案 |
| CAP-23 | 输入系统（设备→动作→命令三层） | Adapters.Client | CAP-02、07、17 | 未定义细案 |
| CAP-24 | 场景与表现对象（池化、投影同步） | Adapters.Client | CAP-14、17、21 | 未定义细案 |
| CAP-25 | 客户端资源更新器 | Adapters.Client.Update | CAP-18、16、21 | 未定义细案 |
| CAP-26 | 音频（最小合同） | Adapters.Client | CAP-14、24 | 未定义细案 |
| **G 服务器核心模块** | | | | |
| CAP-27 | 服务器宿主（readiness、优雅停机、多 World） | ServerHost | CAP-00、12、15 | 未定义细案 |
| CAP-28 | 定时与调度（模拟内可回放 / 进程级隔离） | Simulation + ServerHost | CAP-04、06、07、27 | 未定义细案 |
| CAP-29 | 会话路由网关（多 World 拓扑、迁移） | ServerHost.Gateway | CAP-12、15、16、27 | 未定义细案 |
| CAP-30 | 日志与遥测（结构化、上下文织入） | Adapters.Observability | CAP-19、02 | 未定义细案 |

## 4. 四种标准用况

所有能力卡的"使用方案"都按这四种用况表述。新增用况时先扩充本节，再回填能力卡。

| 用况 | 名称 | 场景 | 承载 Profile |
|---|---|---|---|
| U1 | Headless 测试 | CI 单进程、确定性、可断言；手推时钟、假 adapter | LocalProfile |
| U2 | 单机游戏 | 本地权威、无网络、可选文件存档 | LocalProfile |
| U3 | 联机权威 | 客户端输入 + 服务端权威，transport 可替换 | AuthoritativeProfile |
| U4 | 在线多 World | Meta/Live/Show 三界、持久化、多人协调 | HydraOnlineProfile |

用况与能力启用矩阵（与顶层 4.6 Profile 兼容矩阵一致）：

| 能力 | U1 测试 | U2 单机 | U3 联机权威 | U4 在线多 World |
|---|---|---|---|---|
| CAP-00 模块组合 | 必须 | 必须 | 必须 | 必须 |
| CAP-01 身份 | 必须 | 必须 | 必须 | 必须 |
| CAP-02 元数据 | 必须 | 必须 | 必须 | 必须 |
| CAP-03 schema 演进 | Golden 测试 | 存档迁移必须 | 混服协商 | 内容发布必须 |
| CAP-04 时钟 | 手动推进 | fixed/variable | 权威端 tick | Live host tick |
| CAP-05 随机 | 固定种子 | 本地实例种子 | 权威端独占 | 权威端独占 |
| CAP-06 状态容器 | 必须 | 必须 | 必须 | 必须 |
| CAP-07 命令事件管线 | 必须 | 必须 | 必须 | 必须 |
| CAP-08 权威模型 | local | local | server-auth | server-auth + 分域 |
| CAP-09 快照哈希 | 必须 | 存档必须 | checkpoint 必须 | World 恢复必须 |
| CAP-10 回放 | 必须 | 可选 | 排障必须 | 会话恢复必须 |
| CAP-11 LocalProfile | — | 必须 | — | — |
| CAP-12 AuthoritativeProfile | — | — | 必须 | — |
| CAP-13 HydraOnlineProfile | — | — | — | 必须 |
| CAP-14 资源租约 | 假加载器 | 打包资源必须 | 打包资源必须 | RuntimeSlice 必须 |
| CAP-15 持久化 | 无 | 可选文件 | 可选 checkpoint | Meta/Character Save |
| CAP-16 网络传输 | 内存 loopback | 无 | 必须可替换 | gateway/host adapter |
| CAP-17 表现会话 | 无 | 可选 | 可选 | ShowWorld 投影 |
| CAP-18 内容管线 | 编译期快照 | 打包期必须 | 双端 revision 一致 | 原子发布门 |
| CAP-19 健康降级 | 必须 | 必须 | 必须 | 必须 |
| CAP-20 热更边界 | 不适用 | 版本化发布 | 版本化发布 | Feature 安全点热更 |

---

## 5. 能力域 A：契约与身份

> 目标工程 `Share/GameFramework.Contracts`。只定义数据与接口，不引用任何运行时、平台、网络、数据库类型（顶层 4.2 规则 1）。

### CAP-01 身份模型

**解决什么问题**：跨系统对象引用需要稳定身份；不同域的身份（实体、实例、命令、事件序号、内容版本）长得像但不能互换，互换会破坏回放与安全校验。

**核心对象**：`EntityId`、`InstanceId`、`CommandId`、`EventSequence`、`ContentRevision`、`Generation`（代际）；`ModuleId` 已在 CAP-00 落地，`CharacterId`、`WorldSessionKey`、`NetId` 只属于 CAP-13 边界合同。

**关键不变量**：
- ID 由所属域创建，其他域只传递不制造。
- 各域 ID 是不同类型，编译期即不可互换；不做隐式转换。
- 异步回调必须校验 instance、generation、sequence；校验失败按迟到输入拒绝（顶层 6 规则 9）。

**失败语义**：使用过期 generation 或乱序 sequence 不是异常，是可观察的拒绝路径；只有不变量被破坏（如同一域出现重复 ID）才进入 RuntimeFault。

**使用方案**：
- U1：ID 生成器注入固定种子，断言输出确定且可读。
- U2：本地递增分配；存档携带 ID 即可恢复。
- U3：EntityId/CommandId 由权威端分配，客户端只能引用；提交命令携带来源 SubjectId。
- U4：World、Session、Entity、Net、持久身份五域严格分域（顶层 2.1 约束 5）。

**前置**：CAP-00。**验收证据**：ID 类型互斥的编译期测试 + generation 拒绝测试。

### CAP-02 消息元数据分层

**解决什么问题**：命令、事件、快照承担不同语义，共用一个强制 envelope 会让本地事件伪造网络身份、让幂等检查出现在不该出现的地方。

**核心对象**（顶层 4.4 已定，本文落为合同清单）：

| 元数据 | 必需字段 |
|---|---|
| `CommandMetadata` | `SchemaVersion`、`CommandId`、来源 `SubjectId`、输入序列；产生 mutation 的入口加 `OperationId` |
| `EventMetadata` | `SchemaVersion`、`EventSequence`、来源 `InstanceId`、逻辑时间 |
| `SnapshotMetadata` | `SchemaVersion`、`InstanceId`、逻辑时间、`ContentRevision`、`StateHash` |

**关键不变量**：
- `WorldSessionKey`、`OwnerGeneration`、发送方与持久化 fence 只在 CAP-12/CAP-13 边界合同出现。
- 幂等只在 mutation boundary 强制；回放、事实事件、本地高频输入不要求网络身份。

**失败语义**：缺少必需元数据的消息在提交边界拒绝；不静默补默认值。

**使用方案**：
- U1：最小元数据 + 断言字段齐全。
- U2：本地输入不伪造 `OperationId` 以外的字段。
- U3：`OperationId` + 输入序列支撑重试去重；未知结果先查询再重试（顶层 6 规则 8）。
- U4：会话字段由 Live host 边界附加，shared 消息不感知。

**前置**：CAP-01。**验收证据**：元数据字段存在性校验测试 + 幂等去重测试。

### CAP-03 序列化与 schema 演进

**解决什么问题**：存档、快照和网络包跨版本可读；生成物（协议、表、代码）在客户端、服务端、工具之间不漂移。

**核心对象**：稳定 `TypeId` 注册表、`SchemaVersion`、追加兼容规则（只加字段不改语义）、旧 generation 拒绝策略、编解码器注册（由模块经 CAP-00 的 Contribute 贡献）。

**关键不变量**：
- 消息与状态携带 `SchemaVersion`；同主版本内追加兼容，跨主版本必须显式迁移。
- 同一内容族禁止双重生成（顶层任务：Luban 与 Hydra compiler 所有权）。
- 生成代码、二进制与适配输出禁止手改（顶层 6 规则 5）。

**失败语义**：读到超出兼容范围的旧版本，返回带版本诊断的失败，由调用方决定恢复路径；解析器不做"尽力而为"的静默丢弃。

**使用方案**：
- U1：Golden 协议测试锁序列化字节。
- U2：存档带版本，读旧档走显式迁移。
- U3：混服时新旧版本协商，不兼容即拒绝进入。
- U4：内容包 `ContentRevision` 与 schema 版本共同构成发布一致性门（配合 CAP-18）。

**前置**：CAP-01、CAP-02。**验收证据**：追加兼容/旧版本拒绝/Golden 字节三组测试。

---

## 6. 能力域 B：运行内核

> 目标工程 `Share/GameFramework.Kernel`（时钟、随机、快照、回放合同）与 `Share/GameFramework.Simulation`（状态、命令事件管线、权威模型）。只依赖 Contracts。

### CAP-04 时钟模型

**解决什么问题**：模拟推进需要可注入、可控制的时间；业务读宿主墙钟会破坏确定性与回放。

**核心对象**：`ISimulationClock`，支持 fixed（固定步长）、variable（可变步长）、turn（回合号）三种模式（顶层 4.3）。

**关键不变量**：业务代码只读逻辑时间；帧循环由宿主驱动，模块不修改帧循环。

**失败语义**：时钟倒退或步长非法是宿主配置错误，启动时校验失败。

**使用方案**：
- U1：手动 `Advance(ticks)`，测试完全控制时间。
- U2：fixed/variable/turn 均可选（顶层 4.6 LocalProfile 时间模型）；fixed 帧驱动、variable 跟随渲染帧、turn 用于回合制样例。
- U3：权威端按 tick 推进，客户端时钟只用于表现插值。
- U4：由 Live host 的 tick 合同决定（顶层 4.6 时间模型行）。

**前置**：CAP-01。**验收证据**：三种时钟模式各一组推进/边界测试。

### CAP-05 可记录随机源

**解决什么问题**：随机结果必须可复现；全局随机数让同一段回放在两台机器产生不同世界。

**核心对象**：`IRandomSource`，由实例种子 + 稳定 stream 标识派生子序列（顶层 4.3）。

**关键不变量**：禁止直接调用全局随机；每个消费方使用独立 stream，避免相互推进污染顺序。

**失败语义**：未注册 stream 的消费在开发期报警（fail-fast 策略由 Profile 决定）。

**使用方案**：
- U1：固定种子断言完整序列。
- U2：实例种子随存档保存。
- U3/U4：随机源只存在于权威端；客户端表现层用本地随机，结果不进模拟。

**前置**：CAP-01。**验收证据**：跨宿主同种子同序列测试 + stream 隔离测试。

### CAP-06 状态容器与命名空间

**解决什么问题**：模块各自持有状态会导致快照漏字段、回放错位；需要一个统一可序列化的状态容器，并让归属可验证。

**核心对象**：`SimulationState`、`StateNamespace` 前缀登记（沿用 CAP-00 descriptor 的声明，Validate 阶段查重）、状态版本号。

**关键不变量**：
- 模块只写自己 namespace 内的状态；跨 namespace 只读走 Projection。
- 状态整体可序列化（配合 CAP-03），任何字段进入快照必须声明 schema。

**失败语义**：namespace 冲突在 Plan/Validate 拒绝启动（已落地语义）；运行期越界写入按 RuntimeFault 处理。

**使用方案**：
- U1/U2/U3 通用：状态容器由 GameInstance 创建，模块经 Context 获得自己的 namespace 句柄。
- U4：Session/World/Show 作用域的状态归属由 CAP-13 划分。

**前置**：CAP-01、CAP-03。**验收证据**：namespace 隔离测试 + 快照字段完备性测试（快照后恢复，哈希一致）。

### CAP-07 命令与事件管线

**解决什么问题**：外部意图如何有序进入模拟、模拟事实如何有序发出；无序或并发写入会破坏 owner 串行。

**核心对象**：`Command`（意图）与 `Event`（事实）路由表（由模块 Contribute 注册处理器）、FIFO 队列、owner 串行执行器。

**关键不变量**：
- 查询走类型安全 Port/Facade，动作走 Command；事件总线不取代查询接口（顶层 3.8 规则 4）。
- 同一 owner 内串行，事件按产生顺序应用，不按帧合并（顶层 2.3）。
- Drain 后新输入拒绝且可观察；已入队输入排空后再 Stop（CAP-00 已落地语义，管线对齐）。

**失败语义**：处理器抛异常按模块故障处理（必需模块回滚、可选模块降级，沿用 CAP-00 失败语义表）；队列满按可配置策略拒绝并记录诊断。

**使用方案**：
- U1：直接提交命令、收集事件列表断言。
- U2：输入设备 → Command → 模拟 → Event → 表现刷新。
- U3：网络 handler 只解析、鉴权、提交 owner command，不直接改模拟（顶层 6 规则 4）。
- U4：跨 World/进程只通过 command/event/channel handoff（顶层 2.3）。

**前置**：CAP-02、CAP-06。**验收证据**：FIFO 顺序、迟到拒绝、队列满、关闭后输入四组测试。

### CAP-08 权威模型

**解决什么问题**：谁有权接受输入、如何解决冲突、在哪里提交，是联机与单机的根本差异；差异必须模型化而不是散在业务代码。

**核心对象**：`IAuthorityModel`，定义输入接受、冲突解决和提交边界；支持 local、server-authoritative、lockstep、rollback 四种 profile 策略（顶层 4.3）。

**关键不变量**：提交边界是唯一 mutation 入口；客户端预测只产生临时状态，权威确认后收敛。

**失败语义**：非法输入（越权、过期、重复）在提交边界拒绝并携带原因；不进入模拟。

**使用方案**：
- U1/U2：local——输入即提交。
- U3：server-authoritative——客户端预测 + 服务端确认，冲突以服务端为准。
- U4：World owner 串行 + 身份代际校验（顶层 2.4）。

**前置**：CAP-07。**验收证据**：local 与 server-auth 两种策略下同一命令序列产生相同状态哈希。

### CAP-09 快照与状态哈希

**解决什么问题**：保存、恢复、断点续传和跨宿主一致性校验都依赖"状态的完整基线 + 可比较的摘要"。

**核心对象**：`ISimulationSnapshot`（含 schema、`ContentRevision`、逻辑时间、状态版本，顶层 4.3）、`IStateHasher`。

**关键不变量**：
- 相同初始状态 + 时钟 + 种子 + 命令序列 ⇒ 相同哈希。
- 快照必须能独立恢复出可运行状态，不依赖外部上下文。

**失败语义**：恢复时 schema/ContentRevision 不匹配按 CAP-03 拒绝；哈希不一致是分歧信号，进入 CAP-10 分歧定位。

**使用方案**：
- U1：任意时刻快照 → 恢复 → 继续推进，哈希与不间断路径一致。
- U2：存档即快照。
- U3：checkpoint 定期落盘，重连从 checkpoint 恢复。
- U4：映射到 World/Session 恢复边界（顶层 4.6）。

**前置**：CAP-06。**验收证据**：快照恢复等价测试 + 跨宿主哈希一致测试。

### CAP-10 回放

**解决什么问题**：从初始状态重建任意一段过程，并指出两份执行从哪里开始分歧——排障、复现和反作弊共同依赖。

**核心对象**：`IReplayStream`，记录命令、时钟、随机和快照引用；报告首个分歧位置（顶层 4.3）。

**关键不变量**：回放输入 = 初始快照 + 命令/时钟/随机序列；三者之外的东西不进流。

**失败语义**：分歧时定位到首个不一致输入，不猜测后续。

**使用方案**：
- U1：bug 复现的主手段——录一次，断言到哈希级。
- U2：观战/回放功能直接复用；回放能力本身按顶层 4.6 为 LocalProfile 必含，此处"可选"仅指用户可见的回放功能可关闭。
- U3/U4：服务器排障与会话恢复，回放流与 checkpoint 配合。

**前置**：CAP-07、CAP-09。**验收证据**：录制-回放哈希一致测试 + 人为分歧定位测试。

---

## 7. 能力域 C：运行配置

> 目标工程 `Share/GameFramework.Profiles`。Profile 是能力的组合器：选择时钟、权威、传输、持久化并写入兼容矩阵；核心项目不得反向引用 Profile（顶层 4.2）。

### CAP-11 LocalProfile

**解决什么问题**：单机与测试需要的最小完整闭环：本地权威、本地时钟、无网络、可选文件存档。

**核心对象**：组合 CAP-04/05/06/07/08(local)/09/10 + 内存传输 + 假资源加载器；必须支持 Headless（顶层 4.6）。

**关键不变量**：即使无网络，也要走正式 command、event、projection、snapshot、replay 入口（顶层 5 节），不允许 Profile 开捷径。

**失败语义**：沿用各能力失败语义；Profile 只决定策略参数，不新增失败路径。

**使用方案**：U1 与 U2 的载体；两条验收样例 `MinimalAction + LocalProfile`、`TurnBased + LocalProfile`（顶层 4.5）。

**前置**：B 域全部。**验收证据**：两个样例的 Headless 测试 + snapshot/replay + 状态哈希比较。

### CAP-12 AuthoritativeProfile

**解决什么问题**：客户端输入 + 服务端权威的通用组合，不绑定具体传输或存储。

**核心对象**：CAP-08 的 server-authoritative 策略 + 可替换 transport 合同（CAP-16）+ 可选 checkpoint（CAP-15）；会话身份字段（`WorldSessionKey`、`OwnerGeneration`、持久化 fence）在本 Profile 边界合同出现（顶层 4.4）。

**关键不变量**：验证同一 Simulation 可替换 HostAdapter 与 AuthorityModel（顶层任务清单）；样例输入与状态哈希与 LocalProfile 下一致。

**失败语义**：断线重连走 checkpoint 恢复；传输失败不产生半提交 mutation。

**使用方案**：
- U1/U2：不适用（其载体是 CAP-11）。
- U3：本 Profile 的载体。两个样例先用 LocalProfile 跑稳，再接本 Profile 验证哈希一致（顶层 4.6 阶段验收）。
- U4：不适用（其载体是 CAP-13）。

**前置**：CAP-08、CAP-16。**验收证据**：同输入跨 Profile 哈希一致 + 断线恢复测试。

### CAP-13 HydraOnlineProfile

**解决什么问题**：Meta/Live/Show 三界、World Entry、持久化和多人协调的映射。

**状态**：顶层明确后置——只在 CAP-12 证据完成后才验证三界映射（顶层 4.6、第 7 节）。本文不展开模型卡，避免超前设计；开工时从顶层 2.1、2.2 节出发单独细化。

**前置**：CAP-12、CAP-18。

---

## 8. 能力域 D：平台适配

> 目标工程 `GameFramework.Adapters.*`。所有外部副作用从这里出入；adapter 合同进 Contracts 或独立合同包，实现按平台放 Client/Server。

### CAP-14 资源租约

**解决什么问题**：资源加载的后端（YooAsset、Addressables、文件系统）各不相同；业务代码不能接触后端句柄，也不能忘记释放。

**核心对象**（顶层 3.8 已定形状）：`IAssetProvider.LoadAsync(AssetKey, AssetScope, CancellationToken) → AssetLease<T>`；`AssetLease` 由 `AssetScope` 统一释放。

**关键不变量**：YooAsset/Addressables/Unity 类型只出现在 adapter 内部；lease 生命周期绑定 scope，scope 释放时全部回收。

**失败语义**：加载失败返回带资源键诊断的失败；重复释放 lease 是编程错误，开发期 fail-fast。

**使用方案**：
- U1：假加载器（内存对象表）。
- U2/U3：打包资源；scope 对应关卡/会话。
- U4：`RuntimeSlice` 由内容管线（CAP-18）投递。

**前置**：CAP-00。**验收证据**：scope 释放回收全部 lease 测试 + 假加载器闭环演示。

### CAP-15 持久化

**解决什么问题**：状态如何在进程重启、设备切换、多人存档间存活，同时不与快照/回放争夺权威。

**核心对象**：持久化 adapter 合同（文件、SQLite、云存储、Character Save 各一实现）；存档 barrier（停服/换 World 前的保存收敛点）。

**关键不变量**：
- 持久化只是快照（CAP-09）的搬运工，不制造第二权威（顶层 3.6 风险：MongoDB 文档模型与 Character Save 不能各自成为权威）。
- 写入带 fence/generation，旧 fence 不覆盖新档。

**失败语义**：保存失败进入显式降级并保留内存真相；恢复失败按 CAP-03 版本诊断处理。

**使用方案**：U1 无；U2 可选文件存档；U3 可选 checkpoint；U4 Meta/Character Save 由 adapter 提供（顶层 4.6）。

**前置**：CAP-09、CAP-03。**验收证据**：存档-恢复等价测试 + fence 过期写入拒绝测试。

### CAP-16 网络传输

**解决什么问题**：消息如何在进程间可靠有序地流动，而不把某个传输库焊进核心。

**核心对象**：transport 合同（连接、发送、接收、断连事件）；loopback（内存）、WebSocket、HTTP/2 等实现；handler 纪律（只解析、鉴权、提交 command）。

**关键不变量**：transport 不解析业务语义；重连不重复提交已确认 mutation（依赖 CAP-02 的 `OperationId`）。

**失败语义**：断连是可恢复事件，由 Profile 决定重连策略；协议错误关闭连接而非静默丢弃。

**使用方案**：U1 内存 loopback；U2 无；U3/U4 可替换真实传输。

**前置**：CAP-02。**验收证据**：loopback 全链路测试 + 断连重连去重测试。

### CAP-17 表现会话

**解决什么问题**：客户端/编辑器如何消费模拟结果，又不反向污染权威。

**核心对象**：`PresentationSession` 合同——从 Projection（只读视图）读取，把输入转换为 Command 提交（顶层 4.5）。

**关键不变量**：
- 表现层不写权威状态；Unity 结果必须经正式 command route 回流（顶层 6 规则 3）。
- PresentationSession 是可选 adapter：Headless 没有、Web/Godot/Unity 各自实现。

**失败语义**：投影滞后或丢失由表现层补偿（重读 Projection），不影响模拟。

**使用方案**：U1 无；U2/U3 可选；U4 ShowWorld 是本合同的一个实现（Presenter/Binder 属 Unity/Hydra 适配包）。

**前置**：CAP-07。**验收证据**：Headless 运行无表现会话 + 只读性编译门禁（核心不引用表现程序集）。

### CAP-18 内容管线

**解决什么问题**：策划内容（表、配置、关卡）从编辑到运行时的路径要可验证、可回滚，不能靠手工拷文件。

**核心对象**：authoring 源 → compiler → pack → manifest（内容哈希、revision）→ current projection（运行时指向当前包）→ 回滚（顶层 2.5）；与表系统所有权划分（Luban vs Hydra compiler）。

**关键不变量**：
- 失败不发布：编译/校验失败保留旧 active 包（顶层 3.8 规则 8）。
- 运行时只读 current projection；`ContentRevision` 进快照元数据（CAP-02/09）。

**失败语义**：发布中途失败原子回退；revision 不匹配的内容在加载时拒绝。

**使用方案**：
- U1：编译期快照（无运行时切换）。
- U2：打包期生成资源包。
- U3：双端 revision 一致性门。
- U4：原子发布门 + 灰度切换。

**前置**：CAP-03、CAP-09。**验收证据**：内容哈希、字段追加、失败不发布、回滚四组测试（顶层任务清单）。

---

## 9. 能力域 E：运维能力

### CAP-19 健康与降级

**解决什么问题**：长运行系统需要知道"现在还好吗"，并在局部故障时保持核心可用。

**核心对象**：启动期健康已随 CAP-00 落地（`ModuleHealthRegistry`、`HostState`、`ModuleHealthReport`）；本能力扩展**运行期**健康：周期性健康探测、Degraded 原因留存、诊断导出（版本、profile、scope、启动耗时，顶层 3.8 规则 8）。

**关键不变量**：必需模块故障拒绝启动或进入故障；可选模块只能进入显式 Degraded，核心继续运行，不静默跳过。

**失败语义**：已落地（CAP-00 失败语义表）：Start 失败逆拓扑回滚、Drain 拒绝新输入、Stop 失败汇总不半关闭。

**使用方案**：四种用况通用；U3/U4 额外要求健康报告可作为负载摘除依据。

**前置**：CAP-00。**验收证据**：启动期已有 22 用例；运行期切片补充降级恢复与诊断导出测试。

### CAP-20 热更边界

**解决什么问题**：哪些东西可以运行时替换、哪些必须版本化发布；没有边界的热更会把状态迁移和回滚变成隐式行为。

**核心对象**：可热更清单（Feature/Tool/内容包）、版本化发布清单（Core/Contracts/Snapshot schema/Simulation 基础模块）、热更安全点（逆拓扑 Drain 后替换，再按拓扑重启，顶层 3.7、3.8 规则 1）。

**关键不变量**：
- 热加载只允许替换声明为可热更的 Feature/Tool 插件。
- 切换失败保留旧 active 版本，不留下半套注册或半套资源。
- 正在运行实例的安全点由 Drain 语义保证（`20260928-module-manager-tidy` 非目标中移交的"逆拓扑 Drain 安全点"归属本能力）。注意区分两个概念：停机时的 Drain 按依赖正序执行（上游先停流，`IGameModule` 合同既有语义）；热更安全点是全量排空后按逆拓扑停止、替换、再按拓扑重启（顶层 3.7 加载流程），二者是同一 Drain 阶段的不同消费方，S6 细化时不得混用。

**失败语义**：回退到旧版本并报告失败原因；热更不适用于带活动状态未排空的对象。

**使用方案**：U1 不适用；U2/U3 版本化发布为主；U4 Feature 级热更 + 内容包灰度。

**前置**：CAP-00、CAP-18。**验收证据**：插件兼容、可热更回退、Drain 安全点测试（顶层审查清单）。

---

## 10. 落地顺序与切片建议

能力不逐个立项，按依赖合并为六个切片，每个切片是独立受控目标并留下独立证据：

| 切片 | 包含能力 | 关键验收 |
|---|---|---|
| S1（已完成） | CAP-00 + CAP-19 启动期 | 22 用例全绿、Demo 轨迹（已 verified） |
| S2 契约层 | CAP-01、02、03 | `Contracts` 工程 + 三组合同测试 |
| S3 运行内核 | CAP-04~10、28 模拟内 | 时钟/随机/快照/回放/管线/调度全绿，含跨宿主哈希 |
| S4 Profile 与样例 | CAP-11、12、CAP-19 运行期 | 两样例 × 两 Profile 哈希一致 |
| S5 平台适配 | CAP-14、15、16、17 | 按需逐个：资源租约与持久化优先 |
| S6 内容与热更 | CAP-18、20 + CAP-13 启动评估 | 失败不发布、回滚、Drain 安全点 |
| S7 客户端核心 | CAP-21~26 | 宿主桥 + UI/输入/场景闭环（依赖用户确认 Unity 版本） |
| S8 服务器核心 | CAP-27、29、30、28 进程级 | 优雅停机、多 World 路由、审计日志 |

顺序纪律：切片只能前进不能跳越——S3 的合同测试依赖 S2 的类型；S4 的哈希一致验收依赖 S3 的 `IStateHasher`；S6 依赖 S5 的资源租约承载内容包。

## 11. 待定选择

1. **S2 从哪开始（真正互斥）**：先建 `GameFramework.Contracts` 独立工程（合同先行、评审面小、但多一次工程抽象），还是把契约合同并入 S3 内核切片首批文件（少一个前置工程、但合同与实现同切片评审、违反"合同先行"的既有倾向）。本文按"合同先行"写成 S2 独立切片，需用户确认后才进 `ready`。
2. 顶层遗留三项待确认（Unity 版本、`MinimalAction`/`TurnBased` 样例、ECS 策略）继续有效：分别只阻塞 S4/S5 中对应实施项，不阻塞 S2/S3。
