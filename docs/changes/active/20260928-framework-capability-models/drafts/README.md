# 能力设计草稿索引

> 定位：**MMO 核心基础框架，可裁剪扩展到各类型游戏**。核心合同按 MMO 的需求驱动力设计（多 World、长会话、服务端权威、持久化、内容发布、热更、代际安全），但核心程序集不出现 MMO 产品词汇（MetaWorld/LiveWorld/ShowWorld 只属于 CAP-13）；单机、回合制、动作等类型通过 Profile 裁剪组合（不用的能力不实现 adapter 即可）。覆盖面 = **核心通用（A–E 域）+ 客户端核心模块（F 域）+ 服务器核心模块（G 域）**。

## 审核方式

1. 按编号顺序（依赖顺序）逐份审核，每份草稿最后一节是**审核决策点**，编号为该文件的 `Q1、Q2…`，真正互斥、需要你拍板。
2. 审核结论直接告诉我，例如："CAP-01：Q1 选 A，Q2 同意推荐，其余过"。
3. 每份草稿头部有状态行：`待审核 → 已审核 / 需修改`；全部能力域 A、B 审核通过后，即可按 S2/S3 切片立项实施。
4. 与既有权威文档的关系：顶层 `20260919-hydra-game-framework/design.md` 是决策权威；本目录草稿是把对应能力卡展开为可实施的设计，若与顶层冲突，以顶层为准并回改草稿。

## 草稿清单与审核进度

| 编号 | 草稿 | 能力域 | 状态 |
|---|---|---|---|
| CAP-00 | 模块管理与组合（已落地，无草稿：见 `Share/GameFramework.PluginAbstractions`、22 用例与 20260928 整理变更） | — | 已落地 verified |
| [CAP-01](CAP-01-identity.md) | 身份模型 | A 契约 | 待审核 |
| [CAP-02](CAP-02-message-metadata.md) | 消息元数据分层 | A 契约 | 待审核 |
| [CAP-03](CAP-03-serialization.md) | 序列化与 schema 演进 | A 契约 | 待审核 |
| [CAP-04](CAP-04-clock.md) | 时钟模型 | B 内核 | 待审核 |
| [CAP-05](CAP-05-random.md) | 可记录随机源 | B 内核 | 待审核 |
| [CAP-06](CAP-06-state.md) | 状态容器与命名空间 | B 内核 | 待审核 |
| [CAP-07](CAP-07-command-event-pipeline.md) | 命令与事件管线 | B 内核 | 待审核 |
| [CAP-08](CAP-08-authority.md) | 权威模型 | B 内核 | 待审核 |
| [CAP-09](CAP-09-snapshot-hash.md) | 快照与状态哈希 | B 内核 | 待审核 |
| [CAP-10](CAP-10-replay.md) | 回放 | B 内核 | 待审核 |
| [CAP-11](CAP-11-local-profile.md) | LocalProfile | C 配置 | 待审核 |
| [CAP-12](CAP-12-authoritative-profile.md) | AuthoritativeProfile | C 配置 | 待审核 |
| CAP-13 | HydraOnlineProfile | C 配置 | 顶层后置，无草稿 |
| [CAP-14](CAP-14-asset-lease.md) | 资源租约 | D 适配 | 待审核 |
| [CAP-15](CAP-15-persistence.md) | 持久化 | D 适配 | 待审核 |
| [CAP-16](CAP-16-transport.md) | 网络传输 | D 适配 | 待审核 |
| [CAP-17](CAP-17-presentation.md) | 表现会话 | D 适配 | 待审核 |
| [CAP-18](CAP-18-content-pipeline.md) | 内容管线 | D 适配 | 待审核 |
| [CAP-19](CAP-19-health-runtime.md) | 健康与降级（运行期） | E 运维 | 待审核 |
| [CAP-20](CAP-20-hotswap.md) | 热更边界 | E 运维 | 待审核 |
| **F 客户端核心模块** | | | |
| [CAP-21](CAP-21-client-host.md) | 客户端宿主（平台桥与帧泵） | F 客户端 | 待审核 |
| [CAP-22](CAP-22-ui.md) | UI 框架（栈/层级/数据流） | F 客户端 | 待审核 |
| [CAP-23](CAP-23-input.md) | 输入系统（设备→动作→命令） | F 客户端 | 待审核 |
| [CAP-24](CAP-24-scene-views.md) | 场景与表现对象（池化/同步） | F 客户端 | 待审核 |
| [CAP-25](CAP-25-client-updater.md) | 客户端资源更新器 | F 客户端 | 待审核 |
| [CAP-26](CAP-26-audio.md) | 音频（最小合同） | F 客户端 | 待审核 |
| **G 服务器核心模块** | | | |
| [CAP-27](CAP-27-server-host.md) | 服务器宿主（readiness/优雅停机） | G 服务器 | 待审核 |
| [CAP-28](CAP-28-scheduling.md) | 定时与调度（模拟内/进程级） | G 服务器 | 待审核 |
| [CAP-29](CAP-29-gateway.md) | 会话路由网关（多 World 拓扑） | G 服务器 | 待审核 |
| [CAP-30](CAP-30-logging.md) | 日志与遥测 | G 服务器 | 待审核 |

## 统一模板

每份草稿固定结构：解决什么问题（含 MMO 驱动力）→ 术语与核心对象 → 合同形状（C# 签名）→ 关键设计决策（含备选与推荐）→ 不变量 → 失败语义 → 使用方案（U1 Headless 测试 / U2 单机 / U3 联机权威 / U4 在线多 World）→ 与其他能力的关系 → 非目标 → 验收证据 → 审核决策点。

## 全局决策（一次审核，全部草稿生效）

- **G1**：核心程序集零平台依赖（不引用 Unity/传输库/数据库 SDK），差异全部进 adapter 与 Profile（顶层 4.2 既定，草稿不再重复论证）。
- **G2**：合同类型使用 `required init` record/struct，不可变优先；可变状态只存在于 CAP-06 状态容器与显式声明的缓冲。
- **G3**：所有异步合同返回 `ValueTask`，带 `CancellationToken`；热路径 API 同步。
- **G4**：草稿中的 C# 签名是**合同意图**，实施时允许在不改变语义的前提下调整命名与可空性；改变语义需回到草稿重新审核。
