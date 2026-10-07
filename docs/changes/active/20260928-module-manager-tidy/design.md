# 模块管理器重新整理设计

## 1. 勘察结论与依据

依据为当前代码真源（2026-09-28 勘察，非历史记录）：

| 编号 | 发现 | 位置 |
|---|---|---|
| F1 | `HostState` 宿主状态机枚举内嵌在 `ModuleHealthRegistry.cs`，核心状态机没有独立文件 | `Share/GameFramework.PluginHost/Hosting/ModuleHealthRegistry.cs` |
| F2 | `PlannedModuleRef` 嵌套在健康登记表内部，仅承载"报告顺序 id 引用"，名称与 `Planning/PlannedModule`（拓扑计划项）混淆；`ModuleHost.Health` 每次访问都重新构造引用列表 | `Share/GameFramework.PluginHost/Hosting/ModuleHealthRegistry.cs`、`ModuleHost.cs` |
| F3 | `ModuleCompositionException` 位于 `GameFramework.PluginAbstractions` 根目录，与其余按职责分目录的合同不一致 | `Share/GameFramework.PluginAbstractions/ModuleCompositionException.cs` |
| F4 | 生命周期轨迹口径不一：上层 `design.md` 3.9 节含 `Ready → Stop → Dispose`，`IGameModule.cs` 注释不含 Ready；Ready 实为宿主状态（`HostState.Ready`）而非模块回调阶段 | `IGameModule.cs`、`20260919-hydra-game-framework/design.md`、`change.md` |
| F5 | `ModuleComposer.Compose` 返回计划但丢失与模块实例的对齐关系，`ModuleHost.Build` 必须使用 `ComposeWithModules`；双入口仅靠注释区分 | `Share/GameFramework.PluginHost/Composition/ModuleComposer.cs` |

## 2. 待定选择（真正互斥，需用户确认）

### 选择 A：整理不动公共 API 与行为（推荐）

- `HostState` 移入独立文件 `Hosting/HostState.cs`，命名空间不变。
- `PlannedModuleRef` 保留为 `ModuleHealthRegistry` 嵌套类型，仅在注释中说明它与 `PlannedModule` 的区别；`ModuleHost.Health` 的顺序列表改为按计划缓存，访问不再重复构造。
- `ModuleCompositionException` 移至 `Validation/` 目录，命名空间保持 `Lyking.GameFramework.PluginAbstractions` 不变。
- `ModuleComposer.Compose` 双入口保留，注释补明确警示（丢失实例对齐，宿主构建必须用 `ComposeWithModules`）。
- 统一生命周期口径：模块回调序列为 `Describe → Contribute → Start → Drain → Stop`；`Plan/Validate/Apply` 由宿主执行；`Ready` 是宿主状态；容器释放发生在停止或失败回滚之后。

### 选择 B：允许收缩公共 API

在 A 的基础上，删除 `ModuleComposer.Compose` 公共入口（宿主与外部一律使用 `ComposeWithModules`），并把 `PlannedModuleRef` 提升为顶级类型并改名（如 `HealthReportRef`）。语义更干净，但属于公共接口变更：`ModuleDemo`、全部测试与未来 Unity 侧使用方需同步，且回归面扩大。

**推荐 A**：第一版刚落地且后续阶段（manifest、运行内核）会继续演进该 API，现在收缩的收益小于稳定优先的价值；F5 的误用风险用注释与文档约束已足够。

### 选择确认记录（2026-09-28）

设计完成后向用户提出了 A/B 确认问题，用户未作答。按以下依据采用选择 A 并推进：选择 A 不改变公共 API、可见行为、协议、持久化和安全边界，不属于"关键兼容、数据或安全决策"，不触发"未确认关键决策停留草稿"的停等条件；用户已明确要求按 AI 工作流完整演练，实施是流程的必然环节。此记录如实反映确认状态：A 是设计推荐，不是用户口头确认。若用户随后要求改走 B，按 plan.md Task 3 的选择 B 分支补充实施并重新取证。

## 3. 可观察结果

1. `Share/GameFramework.PluginHost/Hosting/HostState.cs` 独立存在，`ModuleHealthRegistry.cs` 只含健康登记表。
2. `Share/GameFramework.PluginAbstractions/Validation/ModuleCompositionException.cs` 存在，根目录只留最高层合同 `IGameModule.cs`。
3. `IGameModule.cs`、`ModuleHost.cs`、`ModuleComposer.cs` 注释与本变更 `change.md` 使用同一套生命周期口径。
4. 构建、测试、演示三项验证结果与第一版完全一致（0 警告 0 错误、22/22、同一演示轨迹）。

## 4. 验收方式

```powershell
dotnet build Share/GameFramework.slnx
dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj
dotnet run --project Share/GameFramework.ModuleDemo
dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260928-module-manager-tidy
git diff --check
```

全部命令记录日期、退出码与关键输出到 `evidence.md`。测试数量必须为 22/22；演示轨迹必须与第一版一致（`Plan → Validate → Apply → Start(含可选模块 Degraded) → Command/Event → Scope → Drain → Stop`）。

## 5. 范围与非目标

范围：F1–F5 对应的 `PluginHost`、`PluginAbstractions` 文件与注释；`ModuleDemo` 与测试文件仅在选择 B 时因 API 收缩同步修改；本变更目录文档。

非目标：

1. 不引入文件 manifest 加载、热更边界、逆拓扑 Drain 安全点——归属 `20260919-hydra-game-framework` 的插件宿主后续任务。
2. 不改变生命周期语义、失败语义表、合同接口成员与 `VContainer` 物化层（9 月 26 日用户已确认的决策不重开）。
3. 不修改 `Share/Vendor/VContainer/` 与 `Share/GameFramework.slnx` 工程清单。
4. 不新增测试用例数量以外的能力代码。

## 6. 档位说明

纯代码整理按档位表可走标准变更；因 `route` 判定主领域 `Share`、风险信号 `shared-contract`，且用户明确要求完整演练受控流程，按"用户可以升级档位"规则采用受控目标，理由记录于此。

## 7. 风险与回滚

| 风险 | 缓解 |
|---|---|
| 文件移动引入命名空间漂移 | 移动文件时保持命名空间不变，构建与测试全绿作为门槛 |
| 文档改写引入新口径矛盾 | 以 `20260919-hydra-game-framework/design.md` 3.9 节为权威，改写只做收敛不做新增语义 |
| 选择 B 的 API 收缩破坏未知使用方 | 当前仓库内使用方仅 `ModuleHost`、`ModuleDemo` 与测试，`git grep` 全量核对后同步修改 |

回滚：单文件粒度 `git` 回滚即可；不涉及数据、协议、持久化与存档，无兼容窗口与迁移要求。
