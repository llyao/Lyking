# 基础框架能力模型体系证据

本文件记录本变更设计会话中真实执行的验证与核对结果。所有条目均为本会话实际运行输出，无推断项。

## 路由证据

- 日期：2026-09-28
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- route --text "在 Share/GameFramework 设计基础框架能力模型体系与使用方案" --path "Share/GameFramework.PluginAbstractions" --path "Share/GameFramework.PluginHost"`
- 关键输出：`primaryArea: Share`；`riskSignals: [shared-contract]`（共享契约变更）；`suggestedProfile: controlled`。
- 结论：与 `change.md` 元数据 `areas`、`riskSignals`、`profile` 一致。

## 口径交叉核对（设计一致性证据）

核对基准：`docs/changes/active/20260919-hydra-game-framework/design.md`（顶层设计）与 `docs/changes/active/20260928-module-manager-tidy/change.md`。

| 核对项 | 本变更位置 | 基准位置 | 结果 |
|---|---|---|---|
| 生命周期序列含 Ready 为宿主状态、非模块回调 | design.md 第 2 节原则 3 | module-manager-tidy change.md"完成后应看到什么"；IGameModule.cs 注释 | 一致 |
| 混合模块模式七条调用规则 | design.md 第 2 节原则 4/5 | 顶层 design.md 3.8 规则 1–8 | 一致（摘录未改写） |
| 消息元数据三表字段 | design.md CAP-02 | 顶层 design.md 4.4 | 一致（字段逐项对照） |
| 时钟/随机/快照/哈希/回放最小要求 | design.md CAP-04/05/09/10 | 顶层 design.md 4.3 | 一致 |
| Profile 兼容矩阵 | design.md 第 4 节用况矩阵 | 顶层 design.md 4.6 | 一致（U1–U4 为 4.6 三 Profile 的用况化展开，未新增能力承诺） |
| 资源租约调用面 | design.md CAP-14 | 顶层 design.md 3.8 规则 6 与示例 | 一致（合同签名未变） |
| 失败语义（必需回滚/可选降级/Drain 拒绝/Stop 汇总） | design.md CAP-07/19 | 顶层 design.md 3.9 失败语义表 | 一致 |
| CAP-00 已落地事实（22 用例、Demo 轨迹、选择 A） | design.md 第 3 节、第 10 节 S1 | module-manager-tidy change.md | 一致 |
| 逆拓扑 Drain 安全点归属 | design.md CAP-20 | module-manager-tidy change.md 非目标（移交后续） | 一致（归属 CAP-20/S6） |

## 工具门禁证据

- 日期：2026-09-28
- 命令：`git diff --check`
- 结果：退出码 0，无空白错误（输出的 CRLF 提示为行尾警告，非检查失败）。
- 命令：`dotnet run --project Tool/AiWorkflow/Lyking.AiWorkflow.csproj -- validate --change docs/changes/active/20260928-framework-capability-models`
- 首轮结果：`isValid: false`，缺失 `evidence.md`、`review.md`（设计会话中途的预期状态）。
- 独立审查发现首轮记录声称"复跑通过"与事实不符（blocker，见 `review.md` #1），已如实撤回该声称。
- 修复审查发现并落盘 `review.md` 后，2026-09-28 真实复跑同一命令：输出 `{"issues":[],"isValid":true}`，无 issue。

## 范围核对

- `git status` 确认本会话仅新增 `docs/changes/active/20260928-framework-capability-models/` 下四个文件；未修改 `Share/` 下任何代码文件。
- 工作区中 `Share/` 既有脏文件（`IGameModule.cs`、`ModuleComposer.cs` 等）属于 `20260928-module-manager-tidy` 已验证变更的暂存内容，本会话未触碰。
