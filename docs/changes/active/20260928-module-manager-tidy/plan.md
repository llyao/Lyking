# 模块管理器重新整理实施计划

> **面向执行者：** 每个任务完成后先运行任务内的验证，再更新 `tasks.md` 和 `evidence.md`。设计未从 `draft` 进入 `ready` 前不得编写实现代码。

**目标：** 按 `design.md` 选择 A 或 B 完成模块管理器的代码归位与口径统一，保持公共行为与第一版验证结果完全一致。

**前提：** 用户已确认 `design.md` 第 2 节的互斥选择，变更元数据状态已转为 `ready` 并进入 `active`。

---

## 文件与职责地图

| 任务 | 新建或修改路径 | 职责 |
|---|---|---|
| Task 1 | `Share/GameFramework.PluginHost/Hosting/HostState.cs`（新建）、`ModuleHealthRegistry.cs`（修改）、`ModuleHost.cs`（修改） | 宿主状态机独立成文件；健康登记表只含登记表 |
| Task 2 | `Share/GameFramework.PluginAbstractions/Validation/ModuleCompositionException.cs`（移动）、`ModuleCompositionException.cs`（删除） | 组合异常归位到 Validation 目录，命名空间不变 |
| Task 3 | `ModuleComposer.cs`、`IGameModule.cs`（修改）；选择 B 时同步 `ModuleHost.cs`、`ModuleDemo/Program.cs`、测试文件 | 注释口径统一；选择 B 时收缩公共入口并适配下游 |
| Task 4 | 本变更目录 `evidence.md`、`tasks.md` | 记录新鲜证据 |
| Task 5 | 本变更目录 `review.md`、`change.md` | 独立审查与状态转换 |

## Task 1：宿主状态机归位

**文件：**

- 新建：`Share/GameFramework.PluginHost/Hosting/HostState.cs`
- 修改：`Share/GameFramework.PluginHost/Hosting/ModuleHealthRegistry.cs`
- 修改：`Share/GameFramework.PluginHost/Hosting/ModuleHost.cs`

- [ ] **步骤 1：** 把 `HostState` 枚举移入 `Hosting/HostState.cs`，命名空间保持 `Lyking.GameFramework.PluginHost.Hosting`，注释保留固定流转说明。
- [ ] **步骤 2：** `ModuleHealthRegistry.cs` 移除 `HostState` 定义；为嵌套 `PlannedModuleRef` 补注释，说明它只是健康报告的顺序 id 引用，与 `Planning/PlannedModule` 无关（选择 B 时提升为顶级 `HealthReportRef` 并同步 `ModuleHost`）。
- [ ] **步骤 3：** `ModuleHost.Health` 改为在计划固定后缓存顺序引用列表，属性访问不再每次构造。
- [ ] **步骤 4：** 运行 `dotnet build Share/GameFramework.slnx`，预期 0 警告 0 错误。

## Task 2：组合异常归位

**文件：**

- 新建：`Share/GameFramework.PluginAbstractions/Validation/ModuleCompositionException.cs`
- 删除：`Share/GameFramework.PluginAbstractions/ModuleCompositionException.cs`

- [ ] **步骤 1：** 用 `git mv` 移动文件到 `Validation/`，命名空间保持 `Lyking.GameFramework.PluginAbstractions`，类型与成员不变。
- [ ] **步骤 2：** 运行 `dotnet build Share/GameFramework.slnx`，预期 0 警告 0 错误。

## Task 3：口径统一与 API 收缩（选择 B 时）

**文件：**

- 修改：`Share/GameFramework.PluginHost/Composition/ModuleComposer.cs`
- 修改：`Share/GameFramework.PluginAbstractions/IGameModule.cs`
- 修改（仅选择 B）：`Share/GameFramework.PluginHost/Hosting/ModuleHost.cs`、`Share/GameFramework.ModuleDemo/Program.cs`、`Share/GameFramework.PluginHost.Tests/*.cs`

- [ ] **步骤 1：** `IGameModule.cs` 注释统一为：模块回调 `Describe → Contribute → Start → Drain → Stop`；`Plan/Validate/Apply` 由宿主执行；`Ready` 是宿主状态；容器释放发生在停止或失败回滚之后。
- [ ] **步骤 2：** `ModuleComposer.Compose` 注释补充明确警示；选择 B 时直接删除该入口并同步 `ModuleHost.Build`、`ModuleDemo` 与测试的调用点。
- [ ] **步骤 3：** 核对上层变更文档引用：`20260919-hydra-game-framework/design.md` 3.9 节为权威口径，本变更文档不改写其语义。
- [ ] **步骤 4：** 运行完整验证（见 Task 4 步骤 1–3）。

## Task 4：验证与证据

- [ ] **步骤 1：** `dotnet build Share/GameFramework.slnx`，预期 0 警告 0 错误。
- [ ] **步骤 2：** `dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`，预期 22/22 通过。
- [ ] **步骤 3：** `dotnet run --project Share/GameFramework.ModuleDemo`，预期输出与第一版同一生命周期轨迹。
- [ ] **步骤 4：** `git diff --check` 与占位符扫描，预期无输出、无占位内容。
- [ ] **步骤 5：** 把全部命令的日期、退出码、关键输出写入 `evidence.md`，更新 `tasks.md`。

## Task 5：独立审查与状态转换

- [ ] **步骤 1：** 由不同执行上下文做只读审查：核对文件归位后命名空间未漂移、公共 API 未变化（选择 A）、注释口径一致、证据新鲜。
- [ ] **步骤 2：** 高严重度发现未关闭前保持 `active`；关闭后把结论写入 `review.md`。
- [ ] **步骤 3：** 证据齐全后转换 `active:verified`；提交、推送需用户明确授权。

## 阶段门

| 阶段 | 进入条件 | 退出条件 |
|---|---|---|
| 设计 | 勘察完成、工件齐全 | 用户确认选择 A/B，变更 `ready` |
| 实施 | 变更 `active` | 构建 0 警告 0 错误 |
| 验证 | 实施完成 | 22/22 测试、演示轨迹一致、门禁干净 |
| 交付验收 | 验证证据齐全 | 独立审查通过，状态 `verified` |

## 回滚顺序

1. 按单文件 `git` 回滚移动与修改；移动文件先恢复原路径再删新路径，保持历史可追溯。
2. 无数据、协议、持久化影响，不需要兼容窗口。
