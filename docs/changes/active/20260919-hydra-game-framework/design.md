# 基于 Hydra 的通用游戏框架设计

> 设计状态：草稿。本文是基于当前可访问 Hydra 仓库的分析结果，不把 Hydra 文档中的目标架构自动视为已实现能力。

## 1. 分析范围与依据

用户给出的 `E:\hydra\_git2` 在当前机器不存在。实际可访问的仓库是 `E:\hydra_git2\hydra`，旁边还有 `E:\hydra_git2\hydra-unity` 和 Hydra 的战斗设计文档目录。本方案按这三个目录分析，并在实现前重新核对实际路径。

重点依据如下：

| Hydra 资料 | 得到的结论 |
|---|---|
| `docs/backend/architecture/system-overview.md` | Gateway、World Host、ECS World、Silo、Character Save 的职责边界 |
| `docs/backend/architecture/multi-world-architecture.md` | MetaWorld、LiveWorld 家族、ShowWorld 的数据方向和权威边界 |
| `docs/backend/modules/world/core-contract.md` | owner 串行、FIFO 顺序、身份代际、故障 fail-closed 规则 |
| `docs/backend/modules/world/README.md` | World Tick、LiveHost adapter、路由、清理和运行时入口 |
| `docs/backend/modules/world/contexts/*/CONTEXT.md` | Combat、Buff、Quest、Dungeon、Inventory、Physics、Projectile 的模块语言 |
| `docs/backend/modules/infrastructure/contexts/table/CONTEXT.md` | 表源、生成契约、二进制和运行时快照边界 |
| `docs/backend/design/runtime-content-pipeline.md` | authoring、编译、pack、manifest、current projection 和回滚 |
| `docs/client/hyflow-architecture.zh-CN.md` | 编辑态记录、运行时交付记录、运行对象图和稳定类型注册 |
| `docs/backend/architecture/mmo-server-framework*.md` | 登录、进入 World、权威 Tick、保存、重连和停服的分阶段闭环 |
| `hydra-shared/Bokura.Hydra.World.Shared` | Shared Live 运行时、ECS、Mirror、生成器、战斗和关卡逻辑实现入口 |
| `hydra-shared/Bokura.Hydra.World.Client` | session-scoped ShowWorld、命令路由、表现投影和 Client Session Frame |
| `hydra-server/Bokura.Hydra.World.Server` | 服务端 World、Meta/Live 生命周期、网络入口和业务适配 |
| `hydra-common/VetBrain` | ECS、Aspect、Mirror、AutoEvent、Save、TypeId 和代码生成能力 |
| `hydra-tools`、`hydra-config`、`hydra-unity` | 内容编译、运行时内容发布、Unity 托管依赖和资源投影 |

Hydra 仓库规模已经是产品级 MMO 工作区，包含大量服务端、共享运行时、通用库、工具和 Unity 依赖。它适合作为边界和失败语义的参考，不适合在 Lyking 第一版整体复制。

## 2. Hydra 的核心方案

### 2.1 三界模型

```text
MetaWorld
  拥有等级、装备、背包、任务进度、奖励、掉落、持久状态
        │ MetaLive / MetaShow 投影
        ▼
LiveWorld 家族
  Shared Live：实体、移动、战斗、交互、物理、AI、关卡逻辑
  ServerLive：多人权威、网络接入、服务端调度
  LocalLive：本地手感、预测、单人副本或离线体验
        │ LiveShow 有序投影
        ▼
ShowWorld
  拥有客户端会话内的展示状态、Presenter、Binder、动画、音频和 UI
```

关键约束：

1. ShowWorld 是投影消费方，不能写 MetaWorld 或 LiveWorld 真相。
2. MetaWorld 不执行高频位置、物理和战斗模拟。
3. ServerLive 和 LocalLive 复用同一套 shared Live 玩法逻辑，差异只放在宿主适配器。
4. 一个事实只有一个 owner；镜像、快照和表现状态不反向成为权威。
5. World、Session、Entity、Net 和持久身份使用不同的 ID，禁止互换。

### 2.2 Shared Live 与 Host Adapter

Hydra 最有价值的设计不是“服务端有一套、客户端再写一套”，而是把玩法核心放在 host-neutral shared runtime：

- shared 层只依赖 ECS、组件、查询、逻辑帧、事件和明确的 capability。
- ServerLive adapter 提供网络、权限、服务端 World、外部 IO 和权威输入。
- LocalLive adapter 提供本地输入、预测、本地路由和单机资源。
- ShowWorld 只接收投影、事件和 read model，不被 Live runtime 直接调用。

这样可在单机、联机、自动化测试和压测中使用同一份移动、战斗、交互和结算语义。

### 2.3 ECS 与有序运行

Hydra 使用 VetBrain ECS，并通过 Aspect、Grouping、Mirror、AutoEvent、TypeId 和代码生成降低反射和运行期注册成本。其稳定原则包括：

- Entity 是身份，Component 是数据，System 是逻辑。
- 一个 World/Session owner 内串行执行；不同 World 可以并行。
- 事件、组件、快照和回调按产生顺序应用，不能按帧随意合并。
- 跨线程、跨 World 和跨进程只通过 command/event/channel handoff。
- 正常业务拒绝使用 typed result，可信内部不变量破坏才进入 RuntimeFault 和确定性 teardown。
- 热路径尽量由状态变化、事件、到期时间和 dirty 标记驱动，避免每帧重复写组件。

Lyking 应先建立小型可测试运行内核，再决定是否引入完整 ECS 或代码生成。首版不把 Unity `MonoBehaviour` 作为玩法真相，也不把线程调度暴露给业务模块。

### 2.4 身份、代际与生命周期

Hydra 对身份的拆分是可靠性基础：

| 身份 | 作用 |
|---|---|
| `AccountId` | 账户级授权和登录主体 |
| `CharacterId` | 稳定角色存档身份 |
| `WorldSessionKey` | 一次角色进入某个 World 的运行会话 |
| `EntityId` | 当前 World 内 ECS 实体 |
| `NetId` | 同步层实体映射 |
| `RuntimeSliceId` | 客户端 ShowWorld 内某个 Live 内容片段 |
| `OwnerGeneration` | 持久化 owner 代际，拒绝旧写入 |
| `OperationId` | 一次业务操作的幂等身份 |

所有异步完成结果必须带回开始时的 identity/generation；迟到、重复或不匹配的结果只能成为 stale/duplicate 结果，不能修改当前状态。副本和场景切换使用 `Opening → Active → Closing → Closed` 这类闭集状态，清理失败不能伪装成成功。

### 2.5 Authoring 到 Runtime Content

Hydra 将内容分为三层：

```text
策划/编辑器 Authoring Record
        ↓ 确定性 Compiler
Runtime Delivery Record / Catalog
        ↓ 校验、打包、manifest、current projection
运行时 Prepared / Active / Published Snapshot
```

这条链路同时用于技能、Buff、子弹、行为树、关卡实体、Level Logic、Root Motion、表现时间轴和本地化内容。编辑器布局、注释、折叠等 editor-only 字段不进入运行时交付；运行时二进制契约只追加字段，不重排字段顺序；消费者只读取已验证的 pack，不自行解析 authoring source。

### 2.6 配置表与运行时快照

Hydra 的表系统分为：

1. `Table Source`：策划维护的源表。
2. `Table Schema`：字段、类型、维度和枚举契约。
3. `Generated Table Contract`：服务端和客户端编译使用的强类型接口。
4. `Table Bytes`：按契约编码的二进制数据。
5. `Runtime Table Snapshot`：进程中原子替换并供业务查询的一致视图。

表重载是批次级动作，成功、未变化和失败必须分别报告；业务缓存只根据实际更新表重建。Lyking 第一版应先支持文件源和内存快照，再接入 Excel/UGit 等编辑工具。

### 2.7 玩法模块的可复用抽象

Hydra 的模块边界可抽象成以下通用接口：

| 模块 | 通用抽象 | 首版策略 |
|---|---|---|
| Combat | AbilitySpec、AbilityInstance、资源、冷却、状态矩阵、效果 | 先做攻击/受击/资源/冷却四类最小能力 |
| Buff | Buff 定义、实例句柄、来源、层数、移除原因、表现摘要 | 先做可叠层、可到期、可移除的闭集生命周期 |
| Interaction | Template、Host、Check、Begin、Stage、Action | 先做范围检查和顺序 Action |
| Objective/Quest | Definition、Event/Signal、Instance、Scope、Progress、Completion | 事件驱动，禁止业务直接写进度 |
| Dungeon | Definition、Run、Roster、Result Fact、Settlement | 先做单人 Run，再加入多人参与者 |
| Inventory | Top-Level Inventory、Slot、Occupancy、Version、Receipt | 先做背包和装备槽，容量规则独立 |
| Physics/Projectile | 场景查询、命中引用、权威投射物、回放和校正 | 先提供查询接口和假实现，后接 Jolt/Unity |
| AI/Level Logic | 稳定 TypeId、图编译、runtime handler、快照 | 先做有限节点集和可重放执行 |

### 2.8 客户端会话与表现

Hydra 的 Client Session Frame 固定了网络接收、route safe point、LocalLive、ShowWorld、网络发送和 host-neutral publication 的顺序。`Presenter` 只把状态转换成表现意图，`Binder` 只负责 Unity View 生命周期；表现层不能修改权威 ECS。

ShowWorld 是登录到登出的 session scope。Live 场景切换只切换 runtime slice，不重建 ShowWorld；旧 slice 必须按明确 `RuntimeSliceId` 清理，不能用“不是当前 slice”的反向谓词，也不能使用全局 `CurrentSlice`。

### 2.9 通用框架的判定标准

通用框架不是把一个 MMO 项目拆成更多程序集，而是让同一套核心在不同玩法和部署方式下成立。核心层必须满足以下条件：

- 不依赖玩家、角色、任务、副本、公会、主城等产品词汇。
- 不依赖 Unity、网络、数据库、缓存、Orleans 或某一种 ECS 实现。
- 同一个模拟输入和初始状态，在固定时钟下可以得到可重放结果。
- 外部能力通过明确 adapter 注入，adapter 不能改变核心状态机语义。
- 玩法能力以模块注册，不通过修改核心枚举或复制核心循环扩展。
- 表现、持久化、网络和分布式调度都是可选能力，不是启动核心的隐式前置条件。

因此，Hydra 的 MetaWorld、LiveWorld、ShowWorld 只能作为一个“在线三界配置”落在通用框架之上；它们不能成为所有游戏都必须使用的根抽象。

## 3. Hydra 方案的适用性判断

### 3.1 应直接吸收的部分

- 三界权威边界和单向数据流。
- Shared Live + Server/Local Host Adapter。
- owner 串行、FIFO、有序消息和 identity/generation 校验。
- 事件驱动目标系统，业务描述“发生了什么”，不直接修改进度。
- authoring → compiler → runtime content → published snapshot。
- 表源、生成契约、二进制和运行时快照分层。
- Presenter/Binder 与 ECS 真相分离。
- 每个模块的唯一 owner、幂等身份、失败语义和回滚路径。

### 3.2 首版不应照搬的部分

- Orleans 不应成为每个实体的运行时依赖；第一版用本地 owner 和可替换的协调接口。
- DynamoDB、Valkey、Character Save 和 Kubernetes 不应在没有持久化需求时进入核心运行时。
- NodeRpc、Gateway、复杂 World Entry、PublicLive 线调度和跨服切线不应阻塞第一条垂直链路。
- 不能把 Hydra 的全部生成器、Unity managed DLL 发布和超大工具树一次性移植。
- 不能把目标类型、状态枚举和业务表无限扩张；新增机制必须通过复用评审。
- 不能把 Hydra 文档中的 CCU、延迟和容量目标直接当作 Lyking 验收值。

### 3.3 主要缺口

Lyking 当前只有项目规则、上下文和 AiWorkflow 工具，没有实际 Unity 工程、服务端可执行项目、共享协议程序集或游戏运行时测试入口。因此本变更只能把落地路径设计清楚，不能声明已完成任何运行时能力。

### 3.4 GameFrameX 方案分析

本次补充分析的公开仓库为 [GameFrameX/GameFrameX](https://github.com/GameFrameX/GameFrameX)，读取的 `main` 提交为 `dbedb9f6d685e11b02f42b02141a69dadb10f655`。它是一个聚合发布仓库，包含 Server、Unity、LayaBox、Config、Protobuf、FairyGUIProject、Tools 和 Docker 目录；Server、Unity 等目录又对应独立源仓库，聚合目录中的修改会被同步覆盖。

GameFrameX 的强项是应用层和交付层基础设施：

- .NET 10 Actor 服务端，以消息队列保证同一 Actor 的串行处理。
- Apps 状态层与 Hotfix 业务层分离，使用 Roslyn Analyzer 约束类型和处理器位置。
- TCP、WebSocket、HTTP、KCP 等传输入口，ProtoBuf 协议和代码生成。
- MongoDB 状态持久化、Luban 配置表、OpenTelemetry、健康检查、Docker 启动和管理后台对接。
- Unity、LayaBox 以及外部 Cocos、Godot 客户端适配，Unity 侧包含 HybridCLR 和 YooAsset。

这些能力适合承载账号、会话、网关、玩家长期状态、后台接口、配置交付和运维，但它的状态模型是以 `Account/Player/Component/CacheState` 为中心，Actor 也不等同于高频 ECS 实体。不能把 GameFrameX 的 `Player`、MongoDB 写入或网络 Handler 直接放入通用 Simulation。

### 3.5 Hydra 与 GameFrameX 的组合结论

两者可以组成一套覆盖大部分游戏前后端基础能力的框架，但组合方式必须是“分层适配”，不是代码目录合并：

```text
通用 Contracts / Simulation Kernel
        │
        ├── Hydra World Profile
        │     World/ECS、Tick、Command/Transfer、Projection、RuntimeContent
        │
        ├── GameFrameX Online Adapter
        │     Gateway、Session、Actor、ProtoBuf、MongoDB、Hotfix、Monitor
        │
        └── Client Adapters
              Unity / Laya / Godot / Web / Headless Presentation
```

组合职责：

| 能力 | 主要来源 | 在通用框架中的位置 |
|---|---|---|
| Simulation、ECS、World owner、Tick、Projection | Hydra | `Kernel/Simulation` 可选 `HydraWorldProfile` |
| 账号、会话、网关、玩家长期状态 | GameFrameX | `OnlineAdapter`，不能进入核心状态模型 |
| Actor 串行调度 | GameFrameX | 可作为 `IAuthorityModel` 或 `HostAdapter` 的宿主，不把实体 Actor 化 |
| Command/Event/Transfer 网络语义 | Hydra + GameFrameX ProtoBuf | 核心定义语义，GameFrameX 负责 ProtoBuf/TCP/WebSocket 编解码 |
| Snapshot、Delta、Checkpoint | Hydra 语义 + GameFrameX MongoDB | `PersistenceAdapter`；高频 Live 状态不直接写 MongoDB |
| 配置和内容 | GameFrameX Luban + Hydra RuntimeContent | Luban 负责表格，Hydra compiler 负责图、技能、时间线等，统一 manifest/revision/hash |
| 热更新 | GameFrameX Hotfix | 只热更应用逻辑；核心契约、状态 schema 和 Simulation 程序集必须版本化 |
| Unity/Laya/Godot | GameFrameX 客户端包 | 只消费 Projection/read model，并提交 Command |
| 监控、健康、Docker、管理后台 | GameFrameX | Host/部署层，可替换，不成为 Simulation 依赖 |

必须保留的隔离：

1. **Actor 与 ECS 隔离。** 一个 Actor 可以拥有一个 `SimulationInstance` 或 World Host；ECS Entity 不创建成独立 Actor，否则会把高频 Tick 变成消息调度瓶颈。
2. **长期状态与实时状态隔离。** Account、Character、Inventory 等低频事实通过持久化 adapter 保存；位置、物理、战斗临时状态由 World/Simulation owner 管理，通过 snapshot 或结算事实跨边界。
3. **协议语义与传输隔离。** Core 只认识 `Command/Event/Transfer/Snapshot` 合同，GameFrameX ProtoBuf 只是一个 codec；不能让 `ReqPlayer*` 成为核心 API。
4. **内容管线单一来源。** Luban 和 Hydra compiler 不能同时生成同一个运行时类型；每个内容族必须明确 source、producer、manifest、revision 和 consumer。
5. **热更新边界固定。** Hotfix 可以替换应用 handler 和非核心模块；改变消息 schema、快照格式、owner 规则或 Simulation 行为时，必须走兼容版本和回放验证。
6. **前端表现可替换。** Unity 的 Presenter/Binder、Laya 的 View、Godot 的 Node 和 Headless read model 都实现同一个 Projection adapter，不能把 ShowWorld 或 Unity 生命周期写入核心。

可行目标是“通用核心 + 在线后端适配 + 可选 Hydra World”，而不是承诺所有游戏都自动获得 MMO 的匹配、战斗、社交和运营系统。账号、背包、邮件、排行榜、房间、匹配、任务、战斗、场景和公会应作为独立 Features 按需组合。

### 3.6 组合方案的硬风险

- GameFrameX 聚合仓库 README 标注根仓库为 AGPL-3.0，Server 子目录文档又标注 Apache-2.0；Hydra 当前参考工作树未找到根许可证文件。正式复用前必须逐个源仓库确认许可证、版权和闭源分发边界。
- GameFrameX README 的 Unity 版本、Server 文档和仓库说明存在不同基线表述；Unity、.NET、IL2CPP/AOT、HybridCLR 和 Godot 目标必须建立兼容矩阵，不能直接把 `.NET 10 + Unity 2019` 当成公共基线。
- GameFrameX 的默认持久化模型偏向 MongoDB 文档和 Player Actor；Hydra 的 Character Save、owner generation、snapshot/delta 和 restore journal 更严格，二者不能各自成为同一事实的权威。
- Hydra 和 GameFrameX 都有代码生成、配置生成和热更新链路；若不先统一 manifest、schema revision 和发布门，会产生客户端、服务端和工具的生成物漂移。
- GameFrameX 的 Hotfix 与 Hydra 的 RuntimeContent/生成程序集属于不同的更新机制，需要先定义加载顺序、版本回退和正在运行实例的安全点。
- GameFrameX 的公开仓库功能覆盖面较大，但运行时测试、真实部署和多端兼容不能只按 README 宣称判断；必须补充 Golden 协议测试、跨 profile 回放测试、故障注入和端到端证据。

### 3.7 可插拔库设计

要让框架能被不同项目、不同宿主和不同基础设施组合，插件必须是受约束的扩展点，不是任意程序集加载器。推荐拆成以下包：

```text
GameFramework.Contracts       稳定数据、接口、ID、版本和错误合同
GameFramework.Kernel          Simulation 生命周期、时钟、快照、回放和模块调度
GameFramework.PluginAbstractions  插件描述、能力、生命周期和注册接口
GameFramework.PluginHost      发现、依赖排序、兼容校验、启动、停机和诊断
GameFramework.Profiles        Local、Authoritative、GameFrameX、Hydra 组合
GameFramework.Adapters.*      网络、存储、前端、内容和运维实现
GameFramework.Features.*      账号、背包、邮件、房间、战斗等可选业务能力
GameFramework.Samples.*       MinimalAction、TurnBased 等验收样例
```

插件合同至少包含以下信息：

| 合同 | 作用 |
|---|---|
| `PluginId` | 稳定插件身份，不能使用程序集名称代替 |
| `ApiVersion` | 插件接口版本，支持主版本不兼容检查 |
| `Capabilities` | 插件提供的能力，如 `Transport`、`Persistence`、`Presentation`、`SimulationModule` |
| `Dependencies` | 依赖的插件和版本范围，形成可验证 DAG |
| `StateNamespace` | 插件拥有的状态前缀、快照字段和迁移版本 |
| `Register` | 注册命令、事件、系统、编解码、Projection 和 adapter |
| `Start/Stop` | 在明确生命周期阶段启动、排空、关闭和释放资源 |
| `Health` | 报告就绪、降级、故障和版本信息 |

核心接口应保持类似以下形状，禁止暴露 Unity、MongoDB、GameFrameX、Hydra 或某一种网络库：

```csharp
public interface IGamePlugin
{
    PluginDescriptor Describe();
    void Register(PluginRegistry registry);
    ValueTask StartAsync(PluginContext context, CancellationToken cancellationToken);
    ValueTask StopAsync(PluginContext context, CancellationToken cancellationToken);
}
```

插件类型分为四类：

1. `SimulationModule`：只能读写当前 Simulation 的受控状态，必须遵守时钟、随机、命令、事件和回放合同，不能直接访问外部 IO。
2. `HostAdapter`：提供网络、Actor、数据库、资源、线程和平台能力，所有外部副作用从这里出入。
3. `PresentationAdapter`：只消费 Projection/read model，把输入转换为 Command；不得反写权威状态。
4. `ToolPlugin`：提供表格、内容、协议和诊断工具，不能成为运行时核心依赖。

插件加载流程固定为：

```text
读取 manifest
  → 校验 PluginId、ApiVersion、Capabilities、依赖和禁止引用
  → 按依赖 DAG 拓扑排序
  → 注册合同和类型 ID
  → 创建 GameInstance / Profile
  → 启动插件并报告 Health
  → 运行 Simulation
  → 进入 Drain，拒绝新输入并排空 owner
  → 按逆拓扑顺序停止和释放
```

可插拔必须满足以下约束：

- 插件不能修改核心帧循环、全局时钟、全局随机源或其他插件的状态命名空间。
- 插件注册的命令、事件和组件必须有稳定 TypeId、schema version 和序列化合同；卸载前要确认没有活动实例继续引用它。
- 插件之间只能通过公开合同、事件或 capability port 通信，不能通过静态单例、反射查找内部类型或直接引用对方数据库。
- 插件的线程模型、分配预算、幂等边界和失败语义写入 descriptor，并在启动时验证。
- 热加载只允许替换声明为可热更的 Feature/Tool 插件；Core、Contracts、Snapshot schema 和 Simulation 基础模块使用版本化发布。
- 插件缺失时必须有明确策略：必需插件拒绝启动，可选插件进入降级状态并保持核心可运行，不能静默跳过。

对应到本方案：`HydraWorldPlugin` 提供 World/ECS/RuntimeContent，`GameFrameXOnlinePlugin` 提供 Actor/Session/ProtoBuf/MongoDB/Hotfix，`UnityPresentationPlugin` 提供 Presenter/Binder，三者都只依赖 `PluginAbstractions` 和各自的 profile 合同。

### 3.8 模块管理模式比较与推荐

模块管理要同时解决五件事：模块如何被发现、依赖如何表达、对象属于哪个生命周期、其他模块如何调用、模块如何安全停止或替换。单一模式通常只能解决其中一部分。

| 模式 | 代表方案 | 优点 | 缺点 | 适用边界 |
|---|---|---|---|---|
| 全局模块表/服务定位器 | GameFrameX `GameFrameworkEntry` | 接入成本低；统一 `Update`/`Shutdown`；Unity Component 可承载配置；旧项目迁移快 | 依赖隐藏在 `GetModule<T>()`；懒创建可能绕过初始化顺序；默认进程级单例，难表达多 World/Session；资源句柄和释放责任容易泄漏 | 基础设施兼容层、单一 Unity 进程的简单服务；不作为通用核心调用方式 |
| 构造函数 DI + 容器 | .NET MSDI、Unity VContainer | 依赖显式、易测试、Scope 清楚；实现可替换 | 只解决对象创建，不自动解决模块选择、profile、拓扑和状态 owner；注册散落时仍会漂移；反射容器会增加启动不确定性 | 普通应用服务和宿主内部实现；作为底层物化器 |
| Contribution → CompositionPlan → Validate → Apply | Hydra/VetBrain | 依赖、profile、生命周期、冲突和禁止引用可在启动前校验；生成代码和手写模块走同一条路径；不同 Host 可物化同一计划 | 需要 descriptor、生成器和校验器；开发门槛高；简单项目显得重；计划冻结后运行时动态改装受限 | 通用框架的模块组合根、跨 Client/Server/Headless 的共享合同 |
| 包管理/清单依赖 | Unity UPM `manifest.json` | 版本、来源和传递依赖清楚；适合分发、缓存和复现构建 | 主要解决包级依赖，不表达运行时 Scope、状态所有权和调用语义；包能安装不等于模块能启动 | 发行、版本锁定和工具链依赖 |
| 运行时 Game Feature 插件 | Unreal Game Features Plugins | 功能可独立打包，能按需加载/激活/停用，适合 DLC 和可选功能 | 强依赖引擎资产与生命周期；停用时的状态迁移、网络兼容和活动实例清理仍需项目自定义；不适合作为 Simulation 核心模型 | 可选玩法、DLC、运营活动和内容开关 |
| App/Plugin 组合 | Bevy `Plugin` | API 小；`build/ready/finish/cleanup` 生命周期清楚；插件组合直观；重复插件有显式规则 | 依赖更多依靠添加顺序和运行时约束；对跨进程协议、存档迁移和状态 owner 没有完整答案 | 轻量运行时、样例和纯代码模块 |
| Actor/邮箱模块 | GameFrameX Server Actor | 同一业务主体内串行，天然隔离可变状态；适合玩家、房间、订单等服务 | 消息和序列化有成本；不适合把高频 ECS Entity 一一 Actor 化；跨 Actor 调用复杂 | 在线应用服务和低频持久状态 |

**推荐采用混合方案：**

```text
包/清单层：锁定模块版本和来源
        ↓
贡献层：模块发布 descriptor，不直接写容器
        ↓
组合层：按 Host + Profile + Scope 生成 CompositionPlan
        ↓
校验层：检查依赖 DAG、冲突、生命周期、能力和禁止引用
        ↓
物化层：MSDI/VContainer/自有 Host 只应用已验证计划
        ↓
调用层：构造函数注入 + 领域门面/Port + Command/Event
        ↓
运行层：Owner 串行执行，按安全点 Drain、替换和释放
```

推荐的实际规则如下：

1. **编译期默认，运行时有限。** Core、Contracts、Simulation、Snapshot schema 不支持任意 DLL 热卸载；Feature、Tool 和资源内容可以在安全点按 manifest 版本切换。这样保留可插拔能力，又不把状态迁移和回滚变成隐式行为。
2. **依赖显式化。** 业务模块只通过构造函数获得依赖；禁止在业务代码中调用全局 `GetModule<T>()`、`IServiceProvider.GetService` 或反射查找其他模块。服务定位器只允许留在兼容层门面内部。
3. **模块分四类。** `SimulationModule` 只改当前模拟状态；`HostAdapter` 处理网络、存储、资源和线程；`PresentationAdapter` 消费 Projection 并产生 Command；`ToolPlugin` 只参与编译、校验和发布。
4. **调用分两种。** 查询能力使用类型安全 Port/Facade，要求-响应调用返回明确结果；跨 owner 或跨进程动作使用 Command/Event，并携带 `OperationId`、版本和来源信息。不要把所有调用都做成事件，也不要让事件总线取代明确的查询接口。
5. **状态按 Scope 归属。** App/Root 只放真正全局且线程安全的基础设施；Session、World、Live、Show 使用 scoped 对象；模块 descriptor 声明 `StateNamespace`，禁止跨模块直接写状态或共享静态可变单例。
6. **资源管理必须封装租约。** 通用合同使用 `IAssetProvider.LoadAsync(AssetKey, AssetScope, CancellationToken)` 返回 `AssetLease<T>`；YooAsset、Addressables、文件系统等只在 adapter 内部出现。`AssetLease` 由 `AssetScope` 统一释放，业务代码不接触后端句柄，也不能忘记 `Release()`。
7. **生命周期固定。** `Describe → Contribute → Plan → Validate → Apply → Start → Ready → Drain → Stop → Dispose`；启动按依赖正序，停止按逆拓扑序。必需模块缺失直接拒绝启动，可选模块只能进入显式降级状态。
8. **可观测和可回滚。** 每个模块记录 `PluginId`、版本、profile、scope、依赖、健康状态和启动耗时；计划、内容包和协议都带 revision/hash。切换失败保留旧 active 版本，不能留下半套注册或半套资源。

资源管理的目标调用面示例：

```csharp
public sealed class LoginPresenter
{
    private readonly IAssetProvider _assets;

    public LoginPresenter(IAssetProvider assets)
    {
        _assets = assets;
    }

    public async ValueTask<AssetLease<LoginView>> LoadAsync(
        AssetScope scope,
        CancellationToken cancellationToken)
    {
        return await _assets.LoadAsync<LoginView>(
            new AssetKey("ui/login"), scope, cancellationToken);
    }
}
```

这个调用面保留了 GameFrameX 的“门面易用性”，吸收了 Hydra 的 Scope、Plan 和 owner 约束，同时把 YooAsset/Addressables/Unity 类型挡在适配层外。它是 Lyking 的首选模块模式；GameFrameX 全局模块表、Hydra CompositionPlan、UPM/Feature manifest 和 Bevy Plugin 分别作为兼容、组合、分发和轻量样例能力存在，而不是互相替代。

公开参考：

- [GameFrameX `GameFrameworkEntry`](https://github.com/GameFrameX/com.gameframex.unity/blob/main/Runtime/Base/GameFrameworkEntry.cs)：接口到实现映射、懒创建、优先级链表、统一更新和逆序关闭。
- [GameFrameX `GameFrameworkComponent`](https://github.com/GameFrameX/com.gameframex.unity/blob/main/Runtime/Base/GameFrameworkComponent.cs)：Unity Component 在 `Awake` 阶段解析实现并注册模块。
- [Unity Package Manager 项目清单](https://docs.unity3d.com/Manual/upm-manifestPrj.html)：项目级依赖、scoped registry 和包版本声明。
- [Unreal Game Features Plugins](https://dev.epicgames.com/documentation/en-us/unreal-engine/game-features-plugins-in-unreal-engine)：可选功能插件的打包、激活和停用思路。
- [Bevy `Plugin` trait](https://docs.rs/bevy_app/latest/bevy_app/trait.Plugin.html)：`build`、`ready`、`finish`、`cleanup` 生命周期与重复插件规则。
- Hydra 参考工作区的 `E:\hydra_git2\hydra\docs\backend\design\composition-registration-governance.md`：Contribution、CompositionPlan、Validate、Host Patch Merge、Host Applier 的具体约束。

### 3.9 模块管理中间件第一版设计（VContainer 物化）

#### 决策记录（2026-09-26，用户确认）

1. 模块管理中间件是框架第一个落地切片，先于 Unity 工程和服务端；本切片不引入 Player/Combat/Product 词汇，也不改变第 3.8 节的混合模式结论。
2. 物化层选用 VContainer。NuGet 上的 `VContainer` 包最高 1.0.2 且为 Godot 平台构建（运行时调用 Godot 原生互操作，脱离 Godot 崩溃），禁止使用；改为按源码引入官方 `VContainer.Standalone`（master `4fc1ed47dff1314e73fbf2a85ab88f53601414f6`，MIT）到 `Share/Vendor/VContainer/`，保留 LICENSE 并记录来源。Unity 侧后续使用 UPM 包，同一核心 API。
3. 物化器只允许使用 VContainer 在 Standalone 源码与 UPM 包中共同存在的最小核心 API 子集：`ContainerBuilder`、`Lifetime`、`Register`、`RegisterInstance`、`As`、`Build`、`IObjectResolver.Resolve`、`CreateScope`、`Dispose`。不使用 VContainer 入口点（`IInitializable`/`IStartable`/`IAsyncStartable`）驱动生命周期。
4. 生命周期由 PluginHost 拥有：`Describe → Contribute → Plan → Validate → Apply → Start → Ready → Drain → Stop → Dispose`。VContainer 只负责按已验证计划物化对象图（构造函数注入），这符合第 3.8 节"构造函数 DI 只作为底层物化器"的定位。
5. Unity 版本、ECS 策略和 `MinimalAction`/`TurnBased` 样例仍保持待确认，只阻塞对应后续阶段，不阻塞本切片。

#### 分层与工程

```text
Share/Vendor/VContainer/                       VContainer 官方源码（MIT，锁定提交）
Share/GameFramework.PluginAbstractions/        模块合同：Descriptor、Registrar、Plan、Validator、Context、Middleware
Share/GameFramework.PluginHost/                组合器、校验器、生命周期宿主、健康注册表
Share/GameFramework.PluginHost.VContainer/     VContainer 物化器与作用域工厂
Share/GameFramework.PluginHost.Tests/          全流程 headless 测试
Share/GameFramework.ModuleDemo/                控制台全流程演示
```

#### 合同形状

```csharp
public interface IGameModule
{
    ModuleDescriptor Describe();
    void Contribute(IModuleRegistrar registrar);   // 只进中间表示，不直接触碰容器
    ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken);
    ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken);
    ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken);
}

public interface IModuleRegistrar
{
    void Register(Type serviceType, Type implementationType, ModuleLifetime lifetime);
    void RegisterInstance(Type serviceType, object instance);
    // 每条贡献记录 ContributedBy，用于冲突与命名空间校验
}

public interface IModuleContainerMaterializer
{
    IModuleContainer Apply(CompositionPlan plan);   // 计划内含已校验的服务贡献
}

public interface IModuleContainer
{
    object? Resolve(Type serviceType);
    IModuleScope CreateScope(string scopeName);
}

public interface IModuleLifecycleMiddleware   // 生命周期中间件钩子链
{
    ValueTask OnModuleStarting(PlannedModule module, CancellationToken ct);
    ValueTask OnModuleStarted(PlannedModule module, TimeSpan elapsed, CancellationToken ct);
    ValueTask OnModuleFault(PlannedModule module, Exception failure, CancellationToken ct);
    ValueTask OnModuleStopped(PlannedModule module, TimeSpan elapsed, CancellationToken ct);
}
```

#### 失败语义

| 阶段 | 失败 | 行为 |
|---|---|---|
| Plan/Validate | 依赖环、缺失依赖、版本不兼容、重复 ModuleId、重复能力、StateNamespace 冲突 | 拒绝启动；收集全部诊断后一次性报告；不创建容器 |
| Start | 必需模块 Start 失败 | 按逆拓扑序停止已启动模块并释放容器；宿主进入 `Failed`，保留原始异常 |
| Start | 可选模块 Start 失败 | 该模块进入 `Degraded`，核心继续；健康报告记录失败原因 |
| Drain | Drain 后提交新输入 | 拒绝并可观察；已入队输入排空后再进入 Stop |
| Stop | 单模块 Stop 失败 | 继续停止其余模块，最后汇总失败，不留下半关闭状态 |

#### 范围纪律

1. `Contribute` 只写中间表示（`IModuleRegistrar`）；同一 `CompositionPlan` 将来可物化到 MSDI 或自有 Host，物化器可替换。
2. 模块之间只用构造函数注入 + Port + Command/Event；禁止从模块内部调用服务定位器。
3. `StateNamespace` 全局唯一；能力冲突必须在 Validate 阶段失败，不允许后注册覆盖先注册。
4. 第一版支持根作用域和命名会话作用域（`CreateScope`）；作用域内只重验 scoped/单例服务解析，scoped 模块生命周期延后到运行内核阶段。
5. VContainer 装配发生在启动/作用域创建时；热路径不得反复 Resolve，复用构造函数注入的引用。
6. 源码引入不包含 VContainer 源生成器与 Unity 编辑器部分；反射注入只发生在实例创建时，与热路径规则不冲突。

#### 本切片验收

- `dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj` 全绿，覆盖：依赖排序、逆序停止、依赖环、缺失必需依赖、版本不兼容、重复 Id、能力冲突、StateNamespace 冲突、必需模块启动失败回滚、可选模块降级、Drain 拒绝、作用域隔离、中间件顺序。
- `dotnet run --project Share/GameFramework.ModuleDemo` 输出完整生命周期轨迹（Plan → Validate → Start → Command/Event → Scope → Drain → Stop）。
- 工具门禁：`validate`、`git diff --check`；证据写入 `evidence.md`。


```text
第一层：通用内核
  GameFramework.Contracts
  GameFramework.Kernel
  GameFramework.Simulation
  GameFramework.Modules

第二层：运行配置
  LocalProfile                 单机、本地回放、离线测试
  AuthoritativeProfile         客户端输入 + 服务端权威
  HydraOnlineProfile           Meta/Live/Show、World Entry、持久化和多人协调

第三层：平台适配
  UnityAdapter                 View、输入、场景、资源、动画、物理
  NetworkAdapter               本地回环、WebSocket、HTTP/2 或其他传输
  PersistenceAdapter           文件、SQLite、云存储或 Character Save
  ToolAdapter                  表格、编辑器、编译器和运行时内容发布
```

第一版不强制拆成多个 NuGet 或独立仓库，但程序集和目录边界应按上述职责建立。通用内核不引用 `Client`、`Server`、`Tool` 和具体平台；运行配置可以组合内核与多个 adapter；产品玩法只依赖 `Modules` 和配置合同。

### 4.1 核心对象

通用内核只保留以下稳定对象：

| 对象 | 责任 | 不负责 |
|---|---|---|
| `GameInstance` | 一次可运行模拟的生命周期和配置 | 玩家登录、网络连接 |
| `SimulationState` | 可序列化、可重放的玩法状态 | Unity 对象、数据库连接 |
| `Command` | 外部对模拟提出的意图 | 直接修改组件或持久状态 |
| `Event` | 模拟产生的事实 | 代替快照或可靠存档 |
| `Projection` | 从权威状态派生的只读视图 | 反向写入模拟 |
| `Module` | 注册组件、系统、命令和事件处理器 | 修改核心帧循环 |
| `HostAdapter` | 提供时钟、输入、输出、资源和外部 IO | 改写核心业务规则 |

`EntityId`、`InstanceId`、`CommandId`、`EventSequence` 和 `ContentRevision` 是通用身份；`CharacterId`、`WorldSessionKey`、`NetId`、`RuntimeSliceId` 只在 HydraOnlineProfile 或对应 adapter 中出现。

### 4.2 依赖方向与禁止引用

通用性必须由依赖图和编译门禁保证，不能只靠目录命名：

```text
GameFramework.Contracts
        ↓
GameFramework.Kernel
        ↓
GameFramework.Simulation
        ↓
GameFramework.Profiles ─────┬── LocalProfile
                            ├── AuthoritativeProfile
                            └── HydraOnlineProfile
        ↓                    ↓
Samples / Features      Client / Server / Tool Adapters
```

强制规则：

1. `Contracts` 只定义数据和接口，不引用任何运行时、平台、网络、数据库或 Hydra 类型。
2. `Kernel` 只能引用 `Contracts`；`Simulation` 只能引用 `Kernel` 和 `Contracts`。
3. `Profiles` 可以组合核心和 adapter 合同，但核心项目不得反向引用 profile、Client、Server、Tool 或 Samples。
4. `Client`、`Server`、`Tool` 和具体前端只能通过 profile 或 adapter 接入核心；禁止互相引用实现程序集。
5. 核心项目禁止引用 Unity、MonoBehaviour、网络传输库、数据库 SDK、Orleans、Hydra 产品命名空间和具体 ECS 实现。
6. 依赖规则必须由项目引用检查或 Roslyn analyzer 执行；“代码审查时记得不要引用”不算验收证据。

### 4.3 时间、权威和可重放合同

`GameInstance` 不强制所有游戏使用固定帧，而是由 profile 选择时钟和权威模型：

| 合同 | 责任 | 最小要求 |
|---|---|---|
| `ISimulationClock` | 提供逻辑时间、步长或回合号 | 支持 fixed、variable、turn 三种模式，禁止业务读取宿主墙钟 |
| `IRandomSource` | 提供可记录的随机序列 | 由实例种子和稳定 stream 标识派生，禁止直接调用全局随机数 |
| `IAuthorityModel` | 定义输入接受、冲突解决和提交边界 | 支持 local、server-authoritative、lockstep 或 rollback profile |
| `ISimulationSnapshot` | 保存和恢复可运行状态 | 包含 schema、content revision、逻辑时间和状态版本 |
| `IStateHasher` | 生成跨宿主可比较的状态摘要 | 相同初始状态、时钟、随机种子和命令序列必须得到相同摘要 |
| `IReplayStream` | 记录命令、时钟、随机和快照引用 | 能从初始快照重建事件序列，并报告首个分歧位置 |

快照、回放和状态哈希是内核能力；网络同步、数据库保存和 Hydra 的 `Character Save` 只是 adapter。浮点、定点、序列化顺序和平台差异必须由 profile 写入兼容矩阵，不能隐含在实现里。

### 4.4 消息元数据分层

命令、事件和快照承担不同语义，不能共用一个强制 envelope：

| 元数据 | 必需字段 | 适用边界 |
|---|---|---|
| `CommandMetadata` | `SchemaVersion`、`CommandId`、来源 `SubjectId`、输入序列 | 外部意图；只有会产生 mutation 的入口才要求 `OperationId` |
| `EventMetadata` | `SchemaVersion`、`EventSequence`、来源 `InstanceId`、逻辑时间 | 已发生事实；不强制 `OperationId` 或网络会话身份 |
| `SnapshotMetadata` | `SchemaVersion`、`InstanceId`、逻辑时间、`ContentRevision`、`StateHash` | 状态基线、恢复、回放和跨宿主比较 |

`WorldSessionKey`、`OwnerGeneration`、网络发送方和持久化 fence 只在 `AuthoritativeProfile` 或 `HydraOnlineProfile` 的边界合同出现。幂等只在 mutation boundary 强制，回放、事实事件和本地高频输入不应伪造网络身份。

### 4.5 可选表现会话和样例边界

`PresentationSession` 是从 Projection 读取的可选 adapter，不是 `GameInstance` 的必需组成部分。Headless、Web、Godot、Unity 或编辑器都可以实现自己的 read-model adapter；`ShowWorld`、`Presenter`、`Binder` 和 `RuntimeSlice` 只属于 HydraOnline 或 Unity 适配包。

通用性由两个样例验证，而不是由 Player/Combat API 命名决定：

- `Samples/MinimalAction`：一个无账号、无持久化、无网络的动作命令，验证 Entity、Command、Event、Projection、时钟和回放。
- `Samples/TurnBased`：一个回合制行动和结算，验证 turn clock、不同的 authority policy、快照恢复和同一核心的再次组合。

`Player`、`Combat`、`Objective`、`MetaWorld` 和 `ShowWorld` 可以作为独立 Features 或 HydraOnline 样例，但不能作为核心项目的验收对象。

### 4.6 Profile 兼容矩阵

| 能力 | LocalProfile | AuthoritativeProfile | HydraOnlineProfile |
|---|---|---|---|
| Headless 运行 | 必须 | 必须 | 必须先有模拟层 |
| 网络传输 | 可选 loopback | 必须有可替换 transport | 必须接入 Hydra gateway/host adapter |
| 持久化 | 默认无 | 可选 checkpoint | Meta/Character Save 由 adapter 提供 |
| Unity/ShowWorld | 可选 | 可选 | 可选，仅作为 PresentationSession |
| 时间模型 | fixed/variable/turn | 由权威端决定 | 由 Live host 和 profile 合同决定 |
| 回放/快照 | 必须 | 必须 | 必须映射到 World/Session 恢复边界 |

阶段验收至少覆盖 `MinimalAction + LocalProfile` 和 `TurnBased + LocalProfile`；再用同一组输入和状态哈希接入 AuthoritativeProfile。只有完成这些证据后，才验证 Hydra 三界映射。

## 5. 第一条垂直链路

```text
通用配置快照
  ↓
LocalProfile 创建 GameInstance 和 SimulationState
  ↓ Simulation Step（由 profile 时钟决定）
MinimalAction / TurnBased 模块推进状态
  ↓ Projection
Headless 或可选 PresentationSession 读取只读视图
  ↑ Command
输入命令经过 owner 校验后回到 Simulation
```

垂直链路先使用 LocalProfile、内存 transport 和假资源加载器，仍然必须经过正式 command、event、projection、snapshot 和 replay 入口。等两个样例在 Headless 测试稳定并通过状态哈希后，再把相同的 Simulation 接到 AuthoritativeProfile，最后才接入 HydraOnlineProfile、Unity、网络和真实内容包。

## 6. 必须明确的工程规则

1. `GameFramework.Kernel` 不引用 Unity、网络、数据库、具体 ECS 或 Hydra 产品词汇。
2. `GameFramework.Simulation` 只依赖通用 command、event、state、clock、snapshot、replay 和 module contract。
3. `PresentationSession` 是可选只读适配器；Unity 结果要通过正式 command route 回流，不能直接写权威数据。
4. `Server` 不让网络 handler 直接改模拟；handler 只解析、鉴权和提交 owner command。
5. `Tool` 只修改 canonical source 或生成器，禁止手改生成代码、二进制和适配输出。
6. 热路径禁止反射、无界分配、LINQ、字符串拼接和跨进程 RPC；性能例外要有可重复基准。
7. 事件是事实，命令是意图，快照是状态；三者不互相冒充。
8. 所有重试必须带 operation identity；未知结果先查询或恢复，不能盲目重复业务 mutation。
9. 任何异步回调都必须验证 instance、generation、sequence 和 owner identity；没有这些字段的本地事件不得伪造网络身份。
10. 设计文档中的目标架构、代码中的实际行为和测试中的验证证据必须分开记录。
11. 核心运行时采用已验证的最小 API 基线；`.NET 10` 只作为工具和测试的暂定基线，Unity 版本只在适配包和兼容矩阵中锁定。

## 7. 设计验收与暂定结论

当前设计满足：

- 已分析 Hydra 的系统、World、ECS、内容、表、客户端、工具和 MMO 服务端边界。
- 已区分 Hydra 的当前实现证据与目标架构文档。
- 已明确 Lyking 的 Client、Server、Share、Tool 所有权和受控风险。
- 已给出第一条可运行通用垂直链路、运行配置和 Hydra profile 的关系、非目标、兼容和回滚边界。
- 已补充核心依赖 DAG、禁止引用、时间/随机/快照/回放合同、元数据分层、可选表现会话、两个非 MMO 样例和 profile 兼容矩阵。

仍需要用户确认的真正互斥选择：

> 2026-09-26 更新：用户已确认模块管理先行，并选定 VContainer 作为物化层（见 3.9 节决策记录）。下列第 2、3、4 项仍待确认，分别只阻塞 Unity、样例和 ECS 相关阶段；第 1 项的第一阶段起点按"模块管理中间件 → 通用内核 → LocalProfile 样例"顺序执行。

1. 是否同意以“通用内核 + LocalProfile + 两个非 MMO 样例”作为第一阶段，而把 Hydra 三界放到后续 profile。
2. Unity 版本和平台基线；本计划默认按 Unity 6 LTS 设计适配边界，但不会在确认前创建工程。
3. 是否接受 `MinimalAction + TurnBased` 作为首阶段的两个通用样例；它们分别覆盖动作命令和回合时钟，不要求玩家、战斗、目标或持久化语义。
4. 是否允许第一阶段引入第三方 ECS；默认先用自有最小接口验证边界，再评估 VetBrain 或其他 ECS。

在这些选择确认前，变更保持 `draft`，不得进入代码实施。
