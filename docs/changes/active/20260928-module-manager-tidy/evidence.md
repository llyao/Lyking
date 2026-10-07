# 模块管理器重新整理证据

## 设计阶段（2026-09-28）

### 环境与路由

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "重新整理模块管理器" --path Share/GameFramework.PluginHost/`
  - 退出码：`0`
  - 结果：主领域 `Share`，风险信号 `shared-contract`，建议档位 `controlled`。

### 勘察范围

- 通读 `Share/GameFramework.PluginHost`（Composition/CompositionValidator.cs、Composition/ModuleComposer.cs、Hosting/ModuleHost.cs、Hosting/ModuleHealthRegistry.cs、Hosting/ModuleContext.cs、Hosting/ModuleHostException.cs）。
- 通读 `Share/GameFramework.PluginAbstractions` 全部合同文件（根目录 `IGameModule.cs`、`ModuleCompositionException.cs` 及 `Hosting/`、`Modules/`、`Planning/`、`Registrar/`、`Validation/` 子目录）。
- 读取 `Share/GameFramework.PluginHost.VContainer`（VContainerModuleContainer.cs、VContainerModuleMaterializer.cs）、测试项目 4 个测试文件与 `Share/GameFramework.ModuleDemo/Program.cs` 的行数与结构。
- 读取 `20260919-hydra-game-framework` 的 `change.md`、`design.md` 3.9 节、`tasks.md`、`plan.md`、`review.md`、`evidence.md`，确认模块管理第一版的已确认决策与待办边界。

### 勘察结论

形成发现 F1–F5（见 `design.md` 第 1 节）：`HostState` 内嵌于健康登记表文件、`PlannedModuleRef` 命名混淆、`ModuleCompositionException` 根目录散置、生命周期口径三处不一、`ModuleComposer` 双入口靠注释区分。第一版 22 个测试全绿的行为与公共合同形状不在整理范围。

### 本轮边界

本轮只产出设计工件与证据，未修改任何 `Share/` 源码；构建、测试与演示验证属于实施阶段，不提前声明。

## 设计确认记录（2026-09-28）

- 向用户提出 A/B 确认问题，未获答复。按 `design.md` "选择确认记录"的依据采用选择 A（不动公共 API），不属于未确认的关键兼容、数据或安全决策；如实记录：A 是设计推荐，非用户口头确认。
- 命令：`validate --change docs/changes/active/20260928-module-manager-tidy --transition draft:ready`
  - 退出码：`0`，`isValid=true`。
- 命令：`validate --change docs/changes/active/20260928-module-manager-tidy --transition ready:active`
  - 退出码：`0`，`isValid=true`。

## 实施与验证证据（2026-09-28）

### 实施内容（选择 A）

- 新建 `Share/GameFramework.PluginHost/Hosting/HostState.cs`：`HostState` 枚举独立成文件，命名空间不变。
- 修改 `Share/GameFramework.PluginHost/Hosting/ModuleHealthRegistry.cs`：移除 `HostState` 定义；为 `PlannedModuleRef` 补充"健康报告顺序 id 引用，与 Planning/PlannedModule 无关"注释。
- 修改 `Share/GameFramework.PluginHost/Hosting/ModuleHost.cs`：健康报告顺序引用列表改为构造时缓存（`_healthOrder`），`Health` 属性不再每次构造；类注释统一生命周期口径。
- 移动（`git mv`）`ModuleCompositionException.cs` 至 `Share/GameFramework.PluginAbstractions/Validation/`，命名空间与类型成员不变，git 识别为重命名。
- 修改 `Share/GameFramework.PluginAbstractions/IGameModule.cs`：合同注释统一为"模块回调 `Describe → Contribute → Start → Drain → Stop`；`Plan/Validate/Apply` 由宿主执行；`Ready` 是宿主状态；容器释放发生在停止或失败回滚之后"。
- 修改 `Share/GameFramework.PluginHost/Composition/ModuleComposer.cs`：`Compose` 补充警示注释（不携带实例对齐，宿主构建必须用 `ComposeWithModules`）。

### 构建、测试与演示

- 命令：`dotnet build Share/GameFramework.slnx`
  - 退出码：`0`；0 错误，1 个警告。该警告为 `Share/Vendor/VContainer/Runtime/Internal/TypeKeyHashTable2.cs(36)` 的 CS0169，属 vendored 第三方锁定源码既有警告（PROVENANCE 禁止手改），非本变更引入。本变更触及工程单独构建均为 0 警告。
- 命令：`dotnet build Share/GameFramework.PluginHost/GameFramework.PluginHost.csproj` 与 `dotnet build Share/GameFramework.PluginAbstractions/GameFramework.PluginAbstractions.csproj`
  - 退出码：`0`；各自 0 警告 0 错误。
- 命令：`dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`
  - 退出码：`0`
  - 结果：**22/22 通过，0 失败，0 跳过**（122 ms）。与第一版数量一致，行为无回归。
- 命令：`dotnet run --project Share/GameFramework.ModuleDemo`
  - 退出码：`0`
  - 结果：与第一版同一轨迹：Plan → Validate → Apply → Start（含可选模块 Degraded）→ Command/Event → Scope（作用域内一致）→ Drain（拒绝新命令）→ Stop（逆拓扑序 `presentation.eventlog → game.counter → framework.clock`），宿主最终状态 `Stopped`。

### 门禁

- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260928-module-manager-tidy`
  - 退出码：`0`，`isValid=true`，`issues=[]`。
- 命令：`git diff --check`
  - 退出码：`0`，无空白错误（仅 git 对改动文件的 LF→CRLF 行尾提示，非空白错误）。
- 占位符扫描（`TODO|TBD|placeholder|待补|待实现`）：变更文档与触及源码目录无命中。

### 独立审查证据（2026-09-28）

- 审查方式：由独立只读代理（不同执行上下文）对 `git diff HEAD -M`、工作区文件与全部工件做静态交叉核对；审查者未重跑构建与测试（保持只读），运行时证据以本文件记录的本会话真实执行输出为准。
- 结论：五项审查点（命名空间、公共 API、注释口径、行为等价性、文档一致性）全部通过；2 条非阻塞观察（验收措辞落差、状态描述滞后）已在 `change.md` 修订；无高严重度发现。详见 `review.md`。

## 验证命令

- `dotnet build Share/GameFramework.slnx`（2026-09-28）
- `dotnet build Share/GameFramework.PluginHost/GameFramework.PluginHost.csproj`（2026-09-28）
- `dotnet build Share/GameFramework.PluginAbstractions/GameFramework.PluginAbstractions.csproj`（2026-09-28）
- `dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`（2026-09-28）
- `dotnet run --project Share/GameFramework.ModuleDemo`（2026-09-28）
- `dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260928-module-manager-tidy`（2026-09-28）
- `git diff --check`（2026-09-28）
- 占位符扫描 `TODO|TBD|placeholder|待补|待实现`（2026-09-28）

## 验证结果

| 命令 | 退出码 | 结果 |
|---|---|---|
| `dotnet build Share/GameFramework.slnx` | `0` | 0 错误，1 个警告（Vendor 既有 CS0169，非本变更引入） |
| 触及工程单独构建 ×2 | `0` | 各 0 警告 0 错误 |
| `dotnet test Share/GameFramework.PluginHost.Tests` | `0` | **22/22 通过，0 失败，0 跳过**（122 ms），与第一版一致 |
| `dotnet run --project Share/GameFramework.ModuleDemo` | `0` | 同一生命周期轨迹：Plan → Validate → Apply → Start（含 Degraded）→ Command/Event → Scope → Drain（拒绝新命令）→ Stop（逆拓扑序），宿主 `Stopped` |
| `validate --change …module-manager-tidy` | `0` | `isValid=true`，`issues=[]` |
| `git diff --check` | `0` | 无空白错误（仅 LF→CRLF 行尾提示） |
| 占位符扫描 | — | 变更文档与触及源码目录无命中 |

## 本轮边界

- 未做：文件 manifest、热更边界、逆拓扑 Drain 安全点（归属上层变更后续任务）；未修改 `Share/Vendor/VContainer/`、`Share/GameFramework.slnx` 与上层变更文档。
- 提交尚未执行；`main` 分支为受保护分支，提交与推送需用户明确授权。
