# 基础框架能力模型体系任务清单

## 设计阶段

- [x] 读取 `CONTEXT-MAP.md`、`Share/CONTEXT.md`、`docs/ai/workflow.md`。
- [x] 使用 `Tool/AiWorkflow route` 确认主领域 `Share`、风险 `shared-contract`、建议档位 `controlled`。
- [x] 通读顶层 `20260919-hydra-game-framework/design.md` 与任务清单，提取既有能力结论（3.7/3.8/3.9、4.1–4.6 节）。
- [x] 勘察 `GameFramework.PluginAbstractions`、`GameFramework.PluginHost` 现有合同与测试覆盖，确认已落地能力事实。
- [x] 写入 `change.md`、`design.md`、`plan.md`、`tasks.md`。
- [x] 形成能力地图 CAP-00~20、四种标准用况（U1–U4）、六切片落地顺序（S1–S6）。
- [ ] 用户确认 `design.md` 第 11 节待定选择后转 `ready`。

## 草稿深化阶段

- [x] 产出 `drafts/` 19 份逐能力详细草稿（CAP-01~12、14~20），统一模板：问题（含 MMO 驱动力）→ 对象 → 合同形状 → 设计决策（含备选与推荐）→ 不变量 → 失败语义 → U1–U4 使用方案 → 关系 → 非目标 → 验收证据 → 审核决策点。
- [x] 补充平台核心模块：F 域客户端 CAP-21~26（宿主桥、UI、输入、场景表现对象、资源更新器、音频）与 G 域服务器 CAP-27~30（服务器宿主、定时调度、会话路由网关、日志遥测），共 28 份草稿。
- [x] `drafts/README.md` 索引与审核进度表；全局决策 G1–G4 一次审核生效。
- [ ] 用户按编号逐份审核（每份的 Q1、Q2… 审核决策点）；审核结论回填各草稿状态行。

## 后续切片挂钩点（各自独立立项，不在本变更内实施）

- [ ] S2 契约层：CAP-01~03 → 新建 `Share/GameFramework.Contracts`，三组合同测试。
- [ ] S3 运行内核：CAP-04~10 → `GameFramework.Kernel`、`GameFramework.Simulation`，含跨宿主哈希与回放分歧测试。
- [ ] S4 Profile 与样例：CAP-11~12、CAP-19 运行期 → `MinimalAction`/`TurnBased` × 两 Profile 哈希一致（依赖用户确认样例与 Unity/ECS 待定项）。
- [ ] S5 平台适配：CAP-14~17 → 资源租约与持久化优先。
- [ ] S6 内容与热更：CAP-18、CAP-20 → 失败不发布、回滚、逆拓扑 Drain 安全点（承接 `20260928-module-manager-tidy` 非目标移交项）。

## 审查与验收阶段（本变更）

- [x] `validate` 工具门禁通过（修复独立审查 blocker 后真实复跑：`isValid: true`，无 issue）。
- [x] 口径交叉核对记录（生命周期、Profile 矩阵、失败语义、元数据分层与顶层一致），见 `evidence.md`。
- [x] 只读独立审查，结论写入 `review.md`（首轮 FAIL：1 blocker、2 major、6 minor；已全部修复采纳）。
- [x] 证据写入 `evidence.md`（首轮缺失、撤回失实声称、复跑通过全程如实记录）。
- [ ] 用户确认 `design.md` 第 11 节待定选择后转 `ready`。
