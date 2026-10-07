# 基础框架能力模型体系独立审查

## 审查方式

只读独立审查，由独立执行上下文（子代理）完成，与本设计的编写上下文分离。审查者实际复跑了 `route`、`validate`、`git diff --check`、`git status`、lyking-ai JSON 解析与 `Share/` 代码类型核实（`ModuleHealthReport`、`HostState`、`ModuleId` 存在性），并通读基准文件：顶层 `20260919-hydra-game-framework/design.md`（647 行）及同目录 `tasks.md`、`20260928-module-manager-tidy/change.md` 与 `design.md`、`Share/GameFramework.PluginAbstractions/IGameModule.cs`、`docs/ai/workflow.md`。

## 审查项与首轮结论

| 项 | 内容 | 首轮 |
|---|---|---|
| A | 能力编号 CAP-00~20 四文件一致 | 不通过（change.md 待定选择写 CAP-04~07，与 S3 的 CAP-04~10 矛盾） |
| B | 用况矩阵与顶层 4.6 无冲突 | 通过 |
| C | 生命周期序列与统一口径一致 | 通过（附 minor：原则 3 未写明 Ready 归属） |
| D | 失败语义与顶层 3.9 一致 | 通过 |
| E | 每卡使用方案 ≥2 用况 | 不通过（CAP-12 仅 U3；CAP-13 豁免未在 change.md 记录） |
| F | 文档卫生（无占位符、元数据合法、状态不越级） | 通过 |
| G | evidence.md 声称与实际相符 | 不通过（声称复跑 `isValid: true`，当时 review.md 未落盘、复跑不可能为真） |

## 发现与处置

| # | 严重度 | 位置 | 问题 | 处置 |
|---|---|---|---|---|
| 1 | blocker | evidence.md 复跑记录 | 记录了未发生的 `isValid: true` | evidence.md 改为如实记录；validate 在 review.md 落盘后真实复跑，结果回填（见 evidence.md 工具门禁证据节） |
| 2 | major | change.md 待定选择 1 | CAP-04~07 与 CAP-04~10 矛盾 | 改为 CAP-04~10 |
| 3 | major | design.md CAP-12 | 使用方案仅覆盖 U3，违反自设验收 | 改为四用况显式点名（U1/U2/U4 标注不适用及载体）；CAP-13 豁免写入 change.md 验收条 |
| 4 | minor | change.md/plan.md | "二十一个能力卡"表述夸大 | 改为"21 个能力编号（19 张完整能力卡 + CAP-00 事实 + CAP-13 占位）" |
| 5 | minor | design.md 原则 3 | 未写明 Ready 为宿主状态 | 补"Ready 是宿主状态，不是模块回调阶段" |
| 6 | minor | design.md CAP-11 | 组合清单漏 CAP-06 | 补入 |
| 7 | minor | design.md CAP-04/10 | U2 缺 turn；回放"可选"粒度差 | CAP-04 U2 补 turn 并注明顶层 4.6；CAP-10 U2 加注"能力必含，使用可选" |
| 8 | minor | design.md CAP-20 | 停机 Drain 与热更安全点两个概念并存未区分 | 补区分说明（依赖正序排空 vs 全量排空后逆拓扑替换），标注 S6 细化时不得混用 |
| 9 | minor | design.md CAP-04/08 U4 | 章节号引用不准 | CAP-04 U4 改引顶层 4.6，CAP-08 U4 改引顶层 2.4 |

全部 blocker 与 major 修复后才允许复跑 validate 并回填证据；minor 已全部采纳修复。

## 审查结论

首轮 FAIL（1 blocker、2 major）。修订后由编写上下文按处置表逐项修正，validate 复跑结果与证据回填见 `evidence.md`。审查者对基准口径的三项复核确认（route 与元数据一致、Share 脏文件归属 module-manager-tidy、已落地事实无误）仍然有效。

状态转换提示：本变更仍为 `draft`，须用户确认 `design.md` 第 11 节待定选择后方可转 `ready`；本审查不构成对该选择的确认。
