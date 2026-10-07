# 模块管理器重新整理任务清单

## 设计阶段

- [x] 读取 `CONTEXT-MAP.md`、`Share/CONTEXT.md` 和上层变更文档。
- [x] 使用 `Tool/AiWorkflow route` 确认主领域 `Share`、风险 `shared-contract`、建议档位 `controlled`。
- [x] 勘察 `PluginAbstractions`、`PluginHost` 全部源码，形成发现 F1–F5（见 `design.md` 第 1 节）。
- [x] 写入 `change.md`、`design.md`、`plan.md`、`tasks.md`、`review.md`、`evidence.md`。
- [x] 用户确认 `design.md` 第 2 节互斥选择：提问未获答复，按设计推荐采用选择 A（不动公共 API），确认依据与边界记录于 `design.md` "选择确认记录"。
- [x] 确认后变更转入 `ready`。

## 实施阶段

- [x] Task 1：`HostState` 独立成文件，健康登记表与宿主状态机解耦，`ModuleHost.Health` 缓存顺序引用。
- [x] Task 2：`ModuleCompositionException` 归位 `Validation/`（`git mv`），命名空间不变。
- [x] Task 3：生命周期口径统一（`IGameModule`、`ModuleHost`、`ModuleComposer` 注释）；选择 A，不收缩公共 API。

## 验证阶段

- [x] `dotnet build Share/GameFramework.slnx`：0 错误；1 个警告为 Vendor 既有（触及工程单独构建 0 警告 0 错误）。
- [x] `dotnet test Share/GameFramework.PluginHost.Tests/GameFramework.PluginHost.Tests.csproj`：22/22 通过。
- [x] `dotnet run --project Share/GameFramework.ModuleDemo`：与第一版同一生命周期轨迹。
- [x] `validate`、`git diff --check`、占位符扫描全部干净。
- [x] 证据写入 `evidence.md`（日期、命令、退出码、关键输出）。

## 审查与交付阶段

- [x] 不同执行上下文只读审查，结论写入 `review.md`（五项全过，2 条非阻塞观察已修订）。
- [x] 高严重度发现关闭（无）。
- [x] 证据齐全后转换 `active:verified`。

交付（`verified` 之后执行，不属于状态转换前置任务）：本地提交与推送需用户明确授权（`main` 为受保护分支），授权后按 `lyking-deliver` 边界执行。
