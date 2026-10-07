# CAP-02 消息元数据分层 · 设计草稿

> 状态：**待审核** | 能力域：A 契约与身份 | 目标工程：`Share/GameFramework.Contracts` | 前置：CAP-01
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

命令、事件、快照是三种语义不同的消息：

- **命令**是外部对模拟的意图——可以失败、可以重复、可以被拒绝；
- **事件**是模拟已产生的事实——不可撤销，只能被消费；
- **快照**是某一时刻的状态基线——用于恢复与比较。

如果三者共用一个强制 envelope（字段全集），会出现两类真实事故：本地事件被迫伪造网络会话身份（误导排障）、幂等检查出现在回放与事实流里（破坏确定性）。

**MMO 驱动力**：
- 重试与去重：网络重试必须带 `OperationId`，服务端才能幂等去重；没有它，"充值两次"。
- 顺序与审计：事件流要有全流单调的 `EventSequence`，才能做客户端补包、服务端审计、回放对账。
- 跨版本：三种消息都要 `SchemaVersion`，配合 CAP-03 的演进规则。

**通用性**：单机高频输入（每帧移动）不值得携带重试身份——元数据分层让"哪些字段必填"按消息语义决定，而不是按网络场景一刀切。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `CommandMetadata` | 命令信封：版本、命令身份、来源、输入序号、可选操作身份 |
| `EventMetadata` | 事件信封：版本、全流序号、来源实例、逻辑时间 |
| `SnapshotMetadata` | 快照信封：版本、实例、逻辑时间、内容版本、状态哈希 |
| `OperationId` | mutation 的幂等键；同键重试必须得到相同结果或可查询的进行中状态 |
| `InputSequence` | 单一来源（Subject）内的输入序号，用于乱序/丢失检测 |
| envelope | 元数据 + 载荷的组合体；载荷由 CAP-03 编解码器管理 |

## 3. 合同形状

```csharp
public sealed record CommandMetadata
{
    public required int SchemaVersion { get; init; }
    public required CommandId Id { get; init; }
    public required SubjectId Subject { get; init; }
    public required long InputSequence { get; init; }
    /// <summary>仅产生 mutation 的命令必填；查询型命令为 null。</summary>
    public OperationId? Operation { get; init; }
    /// <summary>命令发出的逻辑时间（可选，便于诊断；不参与权威判定）。</summary>
    public long? IssuedLogicalTime { get; init; }
}

public sealed record EventMetadata
{
    public required int SchemaVersion { get; init; }
    public required EventSequence Sequence { get; init; }   // 单实例全流单调
    public required InstanceId Instance { get; init; }
    public required long LogicalTime { get; init; }         // 产生事件的逻辑时间
}

public sealed record SnapshotMetadata
{
    public required int SchemaVersion { get; init; }
    public required InstanceId Instance { get; init; }
    public required long LogicalTime { get; init; }
    public required ContentRevision Content { get; init; }  // 与 CAP-18 对齐
    public required StateHash Hash { get; init; }           // 与 CAP-09 对齐
}

/// <summary>操作身份：U3/U4 由客户端生成、服务端登记；U2 可由本地递增生成。</summary>
public readonly record struct OperationId(Guid Value);

/// <summary>envelope = 元数据 + 类型化载荷。载荷类型即命令类型（稳定 TypeId，见 CAP-03）。</summary>
public sealed record CommandEnvelope<TPayload>(CommandMetadata Metadata, TPayload Payload);
public sealed record EventEnvelope<TPayload>(EventMetadata Metadata, TPayload Payload);
```

**会话级字段的位置**：`WorldSessionKey`、`OwnerGeneration`、发送方连接身份、持久化 fence **不出现在上述记录里**——它们是 CAP-12/16 边界的传输头（`TransportFrame.Header`）与持久化头（`PersistenceRecord.Fence`）。核心合同保证：把这些字段加进 envelope 的代码无法通过编译（字段不存在），也不会有"万能扩展属性袋"（见 Q2）。

## 4. 关键设计决策（含推荐）

**D1｜三信封分离（推荐，顶层 4.4 既定）**：不做单一 `IMessage` 大信封。代价是三条记录维护，收益是字段必填性精确。

**D2｜禁止扩展属性袋（推荐）**：不给 envelope 加 `Dictionary<string, object> Tags` 之类逃生舱。理由：逃生舱必然被用来塞会话身份，分层立即腐化；真实需要的附加信息（诊断标签）走日志，不走消息。

**D3｜`OperationId` 生成责任（推荐）**：发起方生成（客户端/调用模块），接收方登记去重。格式用 GUIDv4：生成无协调成本；幂等登记表只需 `(Subject, OperationId)` 二级索引。

**D4｜`InputSequence` 的间隙语义（推荐）**：权威端检测到序号间隙时**不等待**——接受乱序到达的输入但记录诊断；真正的丢失重传由传输层（CAP-16）负责。备选"间隙即拒绝"对 UDP 式传输过严，对 TCP 式传输多余。

## 5. 不变量

1. 事件是事实：`EventMetadata` 不携带任何"意图"字段（无 Operation、无 Subject——来源是 Instance 而非玩家）。
2. 命令是意图：可以被拒绝；`CommandMetadata` 不携带"结果"字段。
3. 幂等只在 mutation boundary 强制：回放流、事件流、本地高频输入的合同里没有幂等字段。
4. `EventSequence` 在单 Instance 的事件流内严格 +1 无间隙（由 CAP-07 管线保证）；跨 Instance 不比较。
5. `LogicalTime` 来自 CAP-04 时钟，禁止业务写墙钟。
6. 会话/网络/持久化身份只出现在对应边界合同；shared 层消息类型不得引用。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 命令缺 `Operation` 但处理器声明为 mutation | CAP-07 提交边界拒绝，诊断"missing operation identity" |
| 重复 `(Subject, OperationId)` | 权威端返回首次处理结果（登记表查询），不重复执行 |
| `InputSequence` 回退/重复 | 接受但记诊断；由来源自身修复（客户端预测序号管理属 CAP-08） |
| `SchemaVersion` 超出兼容范围 | CAP-03 解码拒绝，带版本诊断 |
| 事件序号出现间隙 | 管线 RuntimeFault（内部不变量破坏，见 CAP-07） |

## 7. 使用方案

- **U1 Headless 测试**：手工构造最小元数据；断言辅助 `CommandEnvelope.Fake(payload)` 只用于测试（内部生成确定性 ID）。
- **U2 单机**：`Operation` 由本地递增生成（存档需要重做保护时才有用）；`InputSequence` 可省略校验（Profile 配置关闭）。
- **U3 联机权威**：客户端为每条 mutation 生成 `OperationId`；断线重连后先查询登记表再决定重试（顶层 6 规则 8"未知结果先查询"）。
- **U4 在线多 World**：`WorldSessionKey`/`OwnerGeneration` 由 Live host 在传输头附加，shared 玩法代码不感知；跨 World handoff 的消息在边界重组信封。

## 8. 与其他能力的关系

- 依赖：CAP-01（全部 ID 类型）、CAP-04（LogicalTime 语义）。
- 下游：CAP-03（SchemaVersion 的校验规则）、CAP-07（提交边界执行必填校验与幂等登记）、CAP-09/10（快照/回放复用信封）、CAP-12/16（边界字段附加）。

## 9. 非目标

- 不定义传输序列化格式（CAP-03/16）。
- 不定义幂等登记表的存储与过期策略（CAP-12 Profile 决定；默认内存 LRU）。
- 不做消息路由/主题订阅模型（事件分发属 CAP-07）。

## 10. 验收证据

- 元数据必填性校验测试（缺字段在提交边界拒绝，带字段名诊断）。
- 幂等测试：同 `OperationId` 重试两次，业务效果一次、响应一致。
- 序号单调性测试：事件流间隙触发 RuntimeFault。
- Golden 测试（配合 CAP-03）：三信封的序列化字节锁定。

## 11. 审核决策点

- **Q1**：同意三信封分离（D1），还是希望先做单一消息信封减少类型数量？
- **Q2**：是否接受"禁止扩展属性袋"（D2）——意味着以后加字段必须改合同（走版本演进）而不是塞字典？
- **Q3**：`OperationId` 用 GUID 由发起方生成（D3）——同意？
- **Q4**：`InputSequence` 间隙不等待、只诊断（D4）——同意，还是需要"间隙即拒收"的严格模式作为 Profile 开关？
