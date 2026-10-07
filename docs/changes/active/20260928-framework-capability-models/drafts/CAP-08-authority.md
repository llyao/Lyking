# CAP-08 权威模型 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Simulation` | 前置：CAP-07
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

谁有权接受输入、冲突听谁的、在哪里提交——这是单机与联机的根本差异。不模型化的话，这些规则散在网络 handler 和业务代码里，每种玩法重写一遍且互相矛盾。

**MMO 驱动力**：
- 服务端权威是 MMO 的底线：客户端永远不可信；预测只影响手感，不影响真相。
- 冲突解决：两人同时捡一个道具、同时攻击同一目标——裁决规则必须集中、可测试。
- 提交边界 = 安全边界：鉴权、限速、幂等、代际校验全部收敛在一处，漏一个入口就是漏洞。

**通用性**：local（单机）、server-authoritative（MMO/竞技）、lockstep（RTS）、rollback（格斗）是四种已知的权威形态；第一版实现 local 与 server-authoritative，lockstep/rollback 保留在合同里由后续需要驱动。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IAuthorityModel` | 权威策略：输入接受、冲突解决、提交边界行为 |
| `AuthorityDecision` | 接受/拒绝/挂起 + 原因 |
| 提交边界 | 命令进入模拟前的最后校验点（管线 Submit 与执行之间） |
| `IPredictionTracker` | server-auth 下的客户端预测对账（本能力定义合同，实现属 CAP-12） |
| 预测 ID 空间 | CAP-01 D3 约定的负值 EntityId 段 |

## 3. 合同形状

```csharp
public enum AuthorityPolicy { Local, ServerAuthoritative /*, Lockstep, Rollback — 保留枚举位 */ }

public interface IAuthorityModel
{
    AuthorityPolicy Policy { get; }

    /// <summary>提交边界校验。管线在执行前调用；拒绝的命令不入 owner 队列。</summary>
    AuthorityDecision ValidateSubmission(in SubmissionContext submission);

    /// <summary>执行前二次校验（命令在队列中等待时世界可能已变化：目标已死、资源已转移）。</summary>
    AuthorityDecision ValidateExecution(in ExecutionAttempt attempt);
}

public readonly record struct SubmissionContext(
    CommandMetadata Metadata,
    TypeId CommandType,
    OwnerKey Owner,
    /// <summary>提交通道（LocalInput / Network / System），鉴权规则按通道区分。</summary>
    SubmissionChannel Channel);

public readonly record struct ExecutionAttempt(
    CommandMetadata Metadata,
    ISimulationClock Clock,
    /// <summary>权威端在执行时补充的归属校验回调（实体是否存在、Subject 是否有权）。</summary>
    Func<EntityId, Generation, bool> EntityExists);

public enum AuthorityDecisionKind { Accept, Reject, Defer }
public sealed record AuthorityDecision(
    AuthorityDecisionKind Kind,
    string? Reason = null,
    /// <summary>Defer 时给出建议重试的 tick（如等待权威确认）。</summary>
    long? RetryAtTick = null);
```

**挂载**：`IAuthorityModel` 由 Profile 提供（经 CAP-00 Contribute 注册为单例能力，capability 名 `authority`——与 `ModuleCapabilities` 的"同能力唯一提供者"机制吻合）；管线在两个校验点调用它。

## 4. 关键设计决策（含推荐）

**D1｜两段校验（推荐）**：提交时校验（快速拒绝，防垃圾入队）+ 执行时校验（时效校验，世界已变）。单段校验要么放垃圾进队列，要么在执行时暴露 TOCTOU（检查-使用间隙）。两段都有，语义分工明确。

**D2｜鉴权数据不进核心（推荐）**：`SubmissionContext` 只带通道与身份，"这个 Subject 能不能发这个命令"的业务规则由 Profile/adapter 提供的 AuthorityModel 实现回答——核心不出现权限表、等级、VIP。核心只强制：**Network 通道的 mutation 必须有 OperationId**（幂等底线）。

**D3｜server-auth 的对账契约（推荐最小化）**：核心只定义 `IPredictionTracker` 的数据面（预测提交、权威回执、分歧回调），不定义预测算法（ECS 快照回滚/仅位置预测由 CAP-12 与玩法层选型）。合同：

```csharp
public interface IPredictionTracker
{
    /// <summary>客户端：提交一条预测，返回预测句柄（含负值预测 EntityId 的映射）。</summary>
    PredictionHandle Track(PredictedInput input);
    /// <summary>权威回执：确认到某 InputSequence；未确认的预测进入分歧处理。</summary>
    PredictionReconcileResult Reconcile(long authoritativeInputSequence, ISimulationSnapshot authoritativeState);
    event Action<PredictionMismatch>? OnMismatch;
}
```

**D4｜local 策略几乎直通（推荐）**：`LocalAuthorityModel` 默认全 Accept（保留提交边界执行元数据完整性校验）。单机不做假鉴权；需要本地反作弊（排行榜防改档）时在存档层（CAP-15）加签，不污染权威模型。

**D5｜冲突解决内聚在执行校验（推荐）**："同时捡道具"这类冲突天然表现为执行时校验失败（第一个成功，第二个 `Reject("already taken")`）——不需要专门的冲突仲裁器。**先到先得是默认语义**；特殊玩法（同时命中同归）由玩法模块在处理器内自行处理，框架不猜测。

## 5. 不变量

1. 所有 mutation 命令必经提交边界；绕过管线的状态写入是架构违规（CAP-06 归属 + 测试把守）。
2. Network 通道 + mutation ⇒ `OperationId` 必填（核心强制，不可配置关闭）。
3. 拒绝必带原因（诊断字符串枚举化），拒绝计数进 CAP-19 健康指标——大量拒绝是攻击或 bug 的信号。
4. 预测状态永不进入权威 `SimulationState`；预测 ID（负值段）在权威快照中非法。
5. `Defer` 只能来自执行校验（提交校验没有"等一等"的语义）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 提交校验拒绝 | `RejectedValidation` + 原因回传提交方；不入队 |
| 执行校验拒绝 | 命令丢弃 + 生成"拒绝事件"（可订阅，供客户端撤销预测） |
| AuthorityModel 自身抛异常 | 该命令按处理器异常处理（owner 隔离，CAP-07 D6）；连续异常触发健康降级 |
| 幂等登记表查询失败 | 命令按未见过处理 + 高级诊断（登记表故障意味着幂等失效，需告警） |

## 7. 使用方案

- **U1 Headless 测试**：注入记录型 AuthorityModel 断言两段校验都被调；构造 TOCTOU 用例（入队后目标消失 → 执行拒绝 + 拒绝事件）。
- **U2 单机**：Local 直通；存档防篡改走 CAP-15。
- **U3 联机权威**：ServerAuthoritative + 限速/鉴权在 AuthorityModel 实现内（Profile 组装）；客户端预测经 `IPredictionTracker` 对账。
- **U4 在线多 World**：每 World 实例独立 AuthorityModel（World owner 串行 + 代际校验，顶层 2.3/2.4）；跨 World 请求在边界转为目标 World 的命令。

## 8. 与其他能力的关系

- 依赖：CAP-01（代际校验）、CAP-02（OperationId 底线）、CAP-07（挂载点：两段校验）。
- 下游：CAP-12（server-auth 完整组装与预测实现）、CAP-16（Network 通道来源）、CAP-19（拒绝率指标）。

## 9. 非目标

- 不实现 lockstep/rollback 策略（合同保留枚举位，需要时立项，先验证核心对它们没有隐藏假设）。
- 不做账号/权限系统（Profile/Features 层）。
- 不做反作弊检测（行为分析等运营能力）。

## 10. 验收证据

- 两段校验触发测试；Network+mutation 缺 OperationId 拒绝测试。
- TOCTOU 用例；先到先得冲突用例（两命令一资源）。
- 预测对账合同测试：预测→确认→无分歧；预测→分歧→回调带双方状态哈希。

## 11. 审核决策点

- **Q1**：D1 两段校验（提交 + 执行）——同意？
- **Q2**：D2 核心只强制"Network mutation 必带 OperationId"，业务鉴权全在 Profile 实现——同意？
- **Q3**：D3 预测对账只定数据面合同、算法归 CAP-12——同意？
- **Q4**：D5 先到先得作为冲突默认语义、不做通用仲裁器——同意？
- **Q5**：第一版只实现 Local + ServerAuthoritative，lockstep/rollback 留枚举位——同意？
