# 基于 Hydra 的通用游戏框架分析证据

## 运行环境

- 日期：2026-09-19
- Lyking 仓库：`D:\Lyking`
- Hydra 用户输入路径：`E:\hydra\_git2`，结果为不存在
- 实际分析路径：`E:\hydra_git2\hydra`
- 关联目录：`E:\hydra_git2\hydra-unity`、`E:\hydra_git2\hydra-battle-docs`

## 已执行命令

### Lyking 路由

命令：

```powershell
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "分析Hydra并在Lyking落地通用游戏框架方案" --path Client --path Server --path Share --path Tool --path docs
```

结果：退出码 `0`。主领域为 `Client`，关联领域为 `Server`、`Share`、`Tool`；风险为 `shared-contract`、`multi-area`；建议档位为 `controlled`。

### Hydra 路由尝试

命令：

```powershell
dotnet run --file scripts/ai/RouteHydraContext.cs -- --task "分析Hydra并在Lyking落地通用游戏框架"
```

结果：未建立有效路由证据。命令因 Hydra 生成程序集文件被其他进程占用而构建失败，错误涉及 `artifacts/obj/Hydra.AI.Semantics/...AssemblyInfo.cs`。本方案没有把该次失败当作 Hydra 路由成功，而是根据 Hydra `CONTEXT-MAP.md`、各 owner `CONTEXT.md`、稳定合同和代码目录进行人工核对。

### Lyking 当前工作区

`git status --short --branch` 显示当前分支为 `main`，本轮未修改业务代码。仓库已有 `Tool/AiWorkflow`，但没有可执行 Unity 游戏工程或服务端游戏运行时项目。

## 已阅读的 Hydra 资料范围

- `AGENTS.md`、`CONTEXT-MAP.md`、`docs/agents/authority-layers.md` 相关规则。
- `docs/backend/architecture/system-overview.md`、`multi-world-architecture.md`、`mmo-server-framework.md`、`mmo-server-framework-design.md`。
- `docs/backend/modules/world/CONTEXT.md`、`core-contract.md`、`README.md` 及 Combat、Buff、Quest、Dungeon、Inventory、Physics、Projectile 上下文。
- `docs/backend/modules/infrastructure/contexts/table/CONTEXT.md`、`table-system.md`。
- `docs/backend/design/runtime-content-pipeline.md`。
- `docs/client/CONTEXT.md`、`hyflow-architecture.zh-CN.md`。
- `hydra-shared/Bokura.Hydra.World.Shared`、`Bokura.Hydra.World.Client` 的项目文件和实现入口。
- `hydra-server/Bokura.Hydra.World.Server`、Gateway、Character Save、NodeRpc、Session 和 AppHost 的项目目录。
- `hydra-common/VetBrain`、`hydra-tools`、`hydra-config`、`hydra-unity` 的目录和项目入口。

## 验证边界

本轮是文档设计变更，没有运行游戏框架测试、Unity 测试、服务端集成测试或内容编译测试；这些命令属于后续任务，不能用本轮文档结构检查替代。变更保持 `draft`，待用户确认架构选择后再进入实现和新鲜验证。

## 方向复审

用户补充“我们是通用框架”后，重新检查了设计的核心依赖。复审结论：Hydra 的 `MetaWorld/LiveWorld/ShowWorld` 是在线 MMO 的运行配置，不应成为通用内核类型；方案已调整为 `GameInstance/SimulationState/Command/Event/Projection/Module/HostAdapter` 核心对象，并将 Hydra 三界下沉到 `HydraOnlineProfile`。第一条垂直链路改为 `LocalProfile` 的 Simulation → Projection → Presentation → Command，避免用 Hydra 产品语义冒充通用性。

## 复审后文档验证

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "落地基于Hydra的通用游戏框架" --path Client --path Server --path Share --path Tool --path docs`
  - 退出码：`0`
  - 摘要：主领域 `Client`；关联领域 `Server`、`Share`、`Tool`；风险 `shared-contract`、`multi-area`；建议档位 `controlled`。
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260919-hydra-game-framework`
  - 退出码：`0`
  - 摘要：`issues=[]`，变更文档结构有效。
- 命令：`git diff --check`
  - 退出码：`0`
  - 摘要：没有空白错误输出；变更目录当前仍是未跟踪文档目录，因此另行执行了目录级尾随空白扫描。
- 命令：目录级尾随空白扫描（PowerShell）
  - 结果：`no-trailing-whitespace`。
- 命令：`rg -n "TODO|TBD|占位|placeholder|待补|待实现" docs/changes/active/20260919-hydra-game-framework`
  - 结果：只命中 `change.md` 中对“占位符扫描”的验收说明，没有发现实际 TODO、TBD 或待实现占位内容。

本轮仍未运行不存在的游戏框架、Unity、服务端集成和内容编译测试；新增的确定性、快照、回放、依赖门禁、样例和替代前端验收属于后续实现阶段。变更继续保持 `draft`，不能据此声明运行时框架已完成。

## GameFrameX 只读分析证据

- 来源：`https://github.com/GameFrameX/GameFrameX`
- 分析提交：`dbedb9f6d685e11b02f42b02141a69dadb10f655`
- 读取范围：根 README、Server README、Server 项目文件、Architecture Analyzer、Unity 包清单、Config/Protobuf/Tools README。
- 观察结果：GameFrameX 提供 .NET 10 Actor 服务端、Apps/Hotfix 分层、TCP/WebSocket/HTTP、MongoDB CacheState、Luban、ProtoBuf、多端客户端、OpenTelemetry、Docker 和管理后台接入。
- 观察结果：聚合仓库的 Server、Unity、LayaBox、Config、Protobuf、FairyGUIProject、Tools 来自独立源仓库，聚合目录会被同步覆盖；正式实现必须定位到对应源仓库或 Lyking 自有 adapter。
- 组合结论：GameFrameX 只进入 `GameFrameXAdapter`，Hydra 只进入 `HydraOnlineProfile` 或 World adapter；核心只依赖通用 Contracts、Simulation、Projection、Snapshot 和 Replay 合同。
- 风险记录：根仓库 README/API 标注 AGPL-3.0，Server 子目录文档标注 Apache-2.0；许可证、私有 UPM 包、版本基线和闭源分发边界尚未完成法律审查。

本轮未构建或运行 GameFrameX、Hydra、Unity 和 Lyking 游戏运行时；以上是基于指定提交和文档/项目文件的架构分析，不代表两套代码已完成兼容集成。

## GameFrameX 组合方案复核

- 结论：组合可行，推荐形态为“通用 Contracts/Simulation Kernel + GameFrameX Online Adapter + Hydra World Adapter”。
- 禁止项：核心不得引用 GameFrameX Actor、`CacheState`、MongoDB、ProtoBuf 消息类型、Unity 类型或 Hydra World 类型；不得让同一事实由 Actor、ECS、MongoDB 和 Character Save 同时拥有权威性。
- 必需验证：Headless 无数据库运行、GameFrameX adapter 接入后状态哈希不变、ProtoBuf 与核心消息合同 Golden 测试、Luban/Hydra 内容生成物 revision 检查、Hotfix 版本回退、许可证和私有依赖清单。

以上仍属于设计阶段证据，未证明运行时兼容和生产部署可用性。

## 可插拔库设计复核

- 已补充 `PluginAbstractions`、`PluginHost`、Plugin manifest、能力声明、依赖 DAG、状态命名空间、生命周期、Health、Drain 和热更边界。
- 已将插件分为 `SimulationModule`、`HostAdapter`、`PresentationAdapter`、`ToolPlugin`，禁止插件修改核心帧循环、全局时钟、全局随机源或其他插件的状态空间。
- 已将 `HydraWorldPlugin`、`GameFrameXOnlinePlugin` 和 `UnityPresentationPlugin` 定义为候选组合，不代表这些插件已经实现。

## 模块管理模式外部资料

访问日期：2026-09-25。以下资料用于比较模块发现、依赖、生命周期和运行时激活语义，不把文档描述当作 Lyking 已实现能力。

| 资料 | 核验到的事实 | 对本方案的影响 |
|---|---|---|
| [GameFrameX `GameFrameworkEntry`](https://github.com/GameFrameX/com.gameframex.unity/blob/main/Runtime/Base/GameFrameworkEntry.cs) | 维护接口到实现类型的映射；`GetModule<T>()` 缺实例时通过 `Activator.CreateInstance` 创建；按 `Priority` 排序；`Update` 正序轮询、`Shutdown` 逆序关闭 | 保留其易用的门面，但把服务定位限制在 adapter 内部，核心改用显式依赖和 Scope |
| [GameFrameX `GameFrameworkComponent`](https://github.com/GameFrameX/com.gameframex.unity/blob/main/Runtime/Base/GameFrameworkComponent.cs) | Unity `Awake` 中解析实现类型并调用 `RegisterModule`，组件是配置和模块注册的桥 | 可作为 Unity Host 的兼容入口，不能作为跨 World 的状态 owner |
| [Unity UPM 项目 manifest](https://docs.unity3d.com/Manual/upm-manifestPrj.html) | 项目清单声明依赖、版本和 scoped registry | 用于包分发与版本锁定，不承担运行时服务调用和状态生命周期 |
| [Unreal Game Features Plugins](https://dev.epicgames.com/documentation/en-us/unreal-engine/game-features-plugins-in-unreal-engine) | 功能可独立打包并按需激活/停用 | 借鉴 Feature 开关和显式激活，但需在 Lyking 追加安全点、状态迁移和活动实例检查 |
| [Bevy `Plugin` trait](https://docs.rs/bevy_app/latest/bevy_app/trait.Plugin.html) | 插件加入 App 时执行 `build`，就绪后执行 `finish`，运行前可执行 `cleanup`；默认禁止同类插件重复添加 | 借鉴小而清晰的生命周期，补上版本依赖、Scope、持久化和跨进程合同 |
| Hydra `E:\hydra_git2\hydra\docs\backend\design\composition-registration-governance.md` | Contribution 汇总为冻结的 CompositionPlan，经 Validate 后由 Host Applier 物化；生产代码禁止绕过计划直接注册 | 作为 Lyking 的核心组合模式，分别适配 MSDI、VContainer 和 Headless Host |

外部资料只证明这些项目的设计取向；本轮没有构建或集成它们，也没有把网络资料作为运行时验证证据。

## 本轮新增验证

- 命令：`dotnet test Tool/AiWorkflow/Tests/Lyking.AiWorkflow.Tests.csproj --no-restore`
  - 退出码：`0`
  - 结果：156/156 通过，0 失败，0 跳过。
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260919-hydra-game-framework`
  - 退出码：`0`
  - 结果：`issues=[]`，受控目标文档结构有效。
- 命令：`git diff --check -- docs/changes/active/20260919-hydra-game-framework`
  - 结果：无输出；目录级尾随空白扫描为 `no-trailing-whitespace`。

## 模块管理中间件第一版证据（2026-09-26）

用户在本轮明确指示：先设计并实施用 VContainer 的模块管理中间件，跑通全流程。以下为本轮真实执行证据。

### 环境与路由

- 命令：`dotnet --info`
  - 结果：.NET SDK 10.0.201（`global.json` 锁定 `10.0.201`，`rollForward: latestPatch`），运行时 10.0.5，RID win-x64。
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "用VContainer实现游戏框架的模块管理中间件" --path Share --path docs`
  - 退出码：`0`
  - 结果：主领域 `Share`，风险 `shared-contract`，建议档位 `controlled`。

### VContainer 引入决策证据

- NuGet 探测：`dotnet add package VContainer` 解析到最新稳定版 `1.0.2`（NuGet flatcontainer API 确认全版本仅 `0.0.0/1.0.0/1.0.1/1.0.2`）。
- 运行时验证：该包 lib 目录为 `net7.0`，构建容器时在 `VContainer.InjectorCache` 内调用 `Godot.GD.Print` 触发 `0xC0000005` 崩溃（脱离 Godot 宿主不可用）。结论：禁止使用 NuGet `VContainer` 包。
- 处置：按源码引入官方 `VContainer.Standalone` 编译范围到 `Share/Vendor/VContainer/`，上游 `master` 提交 `4fc1ed47dff1314e73fbf2a85ab88f53601414f6`，MIT 许可证随附，来源与边界记录在 `Share/Vendor/VContainer/PROVENANCE.md`；仅裁剪 `Runtime/Unity/**` 与 `.asmdef/.meta`，未改动任何 `.cs` 内容。
- 命令：`dotnet build Share/Vendor/VContainer/VContainer.Standalone.csproj`
  - 结果：0 错误（netstandard2.1，1 个无害警告已随构建消除）。

### 构建、测试与演示

- 命令：`dotnet build Share/GameFramework.slnx`
  - 退出码：`0`；0 警告 0 错误。包含：`VContainer.Standalone`、`GameFramework.PluginAbstractions`、`GameFramework.PluginHost`、`GameFramework.PluginHost.VContainer`、`GameFramework.PluginHost.Tests`、`GameFramework.ModuleDemo`。
- 命令：`dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`
  - 退出码：`0`
  - 结果：**22/22 通过，0 失败，0 跳过**（63 ms）。覆盖：拓扑排序与同层稳定序、依赖环、缺失依赖、主/次版本不兼容、重复模块 Id、重复能力、命名空间冲突、跨模块重复服务贡献、启动顺序、逆序停止、必需模块启动失败逆序回滚（含回滚清单）、可选模块 Degraded 降级、Drain 正序排空与幂等、Drain 后拒绝新作用域、单模块停止失败不阻断并汇总、未启动宿主拒绝解析/作用域、中间件钩子顺序、Scoped 隔离/Singleton 共享/Transient 新建/作用域释放回收、必需服务缺失结构化异常、命令 FIFO 推进 + 事件按序落盘 + Drain 拒绝新输入 + Stop 逆序的端到端全流程。
- 命令：`dotnet run --project Share/GameFramework.ModuleDemo`
  - 退出码：`0`
  - 结果：控制台完整输出 `Plan → Validate → Apply → Start(含可选模块 Degraded) → Command/Event → Scope → Drain → Stop` 轨迹；排空后提交被拒绝；停止按逆拓扑序。

### 门禁与工作区

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260919-hydra-game-framework`
  - 退出码：`0`，`isValid=true`，`issues=[]`。
- 命令：同上 `--transition ready:active`
  - 退出码：`0`，`isValid=true`。说明：用户在同一会话内确认设计并指示实施，变更实际经过 `ready` 后进入 `active`；`draft:ready` 边因工具要求"元数据状态必须与转换目标一致"而无法事后补验，此处如实记录。
- 命令：`git diff --check`（全仓库）
  - 退出码：`0`，无空白错误。
- 占位符扫描（`TODO|TBD|placeholder|待补|待实现`，覆盖变更文档与 Share 新增工程）：仅命中 `evidence.md` 中对扫描命令的自述与 `obj/project.assets.json` 构建产物；源码与文档无占位内容。
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- check-workspace --change docs/changes/active/20260919-hydra-game-framework`
  - 结果：所有修改/新增文件均在变更 `paths` 内；`issues` 报告 `workspace.branch`：当前分支 `main` 被视为受保护分支，`canProceed=false`。本轮未执行任何 Git 写操作（无暂存、无提交）；后续提交需按 `lyking-deliver` 建分支或取得用户授权。

### 本轮边界

- 未做：Unity 工程、文件 manifest 加载、热更边界、Simulation 内核（GameInstance/快照/回放）与 profile；这些属于 plan.md 后续任务，不因本轮证据声明完成。
- NuGet 探测产生的临时工程位于系统临时目录，未进入仓库。

## Lyking 当前模块系统补充核对

- 日期：2026-10-03。
- 路由：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "继续分析通用游戏框架模块管理方案，模块边界、依赖图、服务调用、Scope、资源租约与插件启停" --path Share --path Client --path Server --path Tool --path docs/changes/active/20260919-hydra-game-framework`。退出码 `0`；主领域 Share，关联 Client/Server/Tool，风险 `shared-contract`、`multi-area`，建议 `controlled`。
- 静态阅读：`Share/GameFramework.PluginAbstractions/IGameModule.cs`、`Modules/*`、`Hosting/*`、`Share/GameFramework.PluginHost/Composition/*`、`Hosting/ModuleHost.cs` 与相关测试；`Client/GameFramework/Core/Modules/*`、`Core/ServiceLocation/*`、`GameClientBuilder.cs`、`docs/architecture.md`、`docs/module-guide.md`。
- 事实：Share 宿主 Build 接收调用方创建的 `IGameModule` 列表，组合描述符并物化服务容器；没有文件 manifest 或动态程序集加载入口。命名 Scope 目前管理服务实例。Client Builder 可扫描带特性的程序集模块，客户端宿主另行驱动主线程帧阶段。
- 静态观察：`ModuleDependency.Required` 在默认依赖校验中没有分支；可选模块 Start 失败后 Drain/Stop 遍历仍覆盖完整计划；`StateNamespace` 只在组合期检查冲突。以上需补充语义决定与行为测试，未在本轮修改代码或运行这些新增场景测试。
- 结论：保留 Client 帧宿主与 Share 服务组合宿主；统一规范和兼容规则，不强行统一运行时基类。首版的“插件”指编译期组合扩展，不指任意 DLL 热加载/热卸载。
