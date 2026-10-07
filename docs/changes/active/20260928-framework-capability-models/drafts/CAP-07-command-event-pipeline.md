# CAP-07 命令与事件管线 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Simulation` | 前置：CAP-02、CAP-06
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

外部意图如何**有序**进入模拟、模拟事实如何**有序**发出。没有统一管线时：并发写状态、事件顺序随线程调度漂移、Drain 后还有输入溜进来——有序运行（顶层 2.3）无从谈起。

**MMO 驱动力**：
- owner 串行：同一玩家/同一 World 的操作必须串行，不同 owner 可并行——管线的 owner 模型直接对应 MMO 的并发单元。
- 补包与审计：事件按序号交付，客户端断线后从 `EventSequence` 补播。
- 停服安全：Drain 拒新收旧——停服维护时不丢在途操作。

**通用性**：单机游戏同样受益（输入队列化让录像/回放免费获得）；回合制游戏天然就是"低频命令、批量结算"。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ICommandBus` | 命令提交入口；提交后由管线在 owner 上下文内串行处理 |
| `IEventSink` | 命令处理器内发布事件的出口 |
| `OwnerKey` | 串行单元：`InstanceId` + 可选子键（如 WorldId / 玩家分区） |
| 命令处理器 | 模块在 Contribute 阶段注册的委托/类型；声明 `OwnerPolicy` 与是否 mutation |
| `IEventChannel<T>` | 事件的订阅出口（投影、日志、回放都从这取） |
| 队列策略 | 队列满时的行为：拒绝新输入（推荐）/ 拒绝最旧（可配） |

## 3. 合同形状

```csharp
public readonly record struct OwnerKey(InstanceId Instance, long SubKey = 0)
{
    public static OwnerKey Whole(InstanceId id) => new(id, 0);
}

/// <summary>提交结果：有序枚举，不含异常。</summary>
public enum SubmitStatus { Accepted, Queued, RejectedQueueFull, RejectedDraining, RejectedValidation }

public interface ICommandBus
{
    /// <summary>热路径同步提交。只做入队与校验，不执行业务。</summary>
    SubmitStatus Submit<TCommand>(in CommandEnvelope<TCommand> command) where TCommand : notnull;

    /// <summary>带返回值的提交（要求-响应）：等待处理完成并取回结果。</summary>
    /// <remarks>仅限可信内部调用（Port 语义的命令化）；网络入口禁止使用。</remarks>
    ValueTask<CommandResult<TResult>> SubmitAsync<TCommand, TResult>(
        CommandEnvelope<TCommand> command, CancellationToken ct);

    bool IsDraining { get; }
}

public sealed record CommandResult<TResult>(SubmitStatus Status, TResult? Value, string? ErrorDetail);

/// <summary>命令处理器注册（Contribute 阶段调用）。</summary>
public interface ICommandHandlerRegistry
{
    /// <summary>isMutation=true 的处理器必须可幂等（配合 CAP-02 OperationId）。</summary>
    void Register<TCommand>(
        Func<TCommand, ICommandContext, ValueTask> handler,
        CommandHandlerOptions options);
}

public sealed record CommandHandlerOptions
{
    public required bool IsMutation { get; init; }
    public OwnerPolicy Owner { get; init; } = OwnerPolicy.BySubject;  // BySubject / WholeInstance / Custom(OwnerKey)
    public int MaxQueueDepth { get; init; } = 4096;
}

/// <summary>处理器执行上下文：发布事件 + 读写状态句柄（受 namespace 约束）。</summary>
public interface ICommandContext
{
    IEventSink Events { get; }
    ISimulationClock Clock { get; }
    /// <summary>当前命令的元数据（处理器不需要透传身份）。</summary>
    CommandMetadata Command { get; }
}

/// <summary>事件订阅出口（Contribute 阶段注册订阅；投递按序）。</summary>
public interface IEventChannelRegistry
{
    void Subscribe<TEvent>(Func<EventEnvelope<TEvent>, ValueTask> subscriber);
}
```

**执行模型**：提交只入队；真正的处理发生在 clock step 内（每 tick 处理各 owner 队列中已到的命令，逐 owner 串行、owner 间并行）。这把"输入 → tick → 事件"固化为单一节奏，回放因此只依赖"(tick, 命令序列)"。

## 4. 关键设计决策（含推荐）

**D1｜提交与执行分离（推荐）**：Submit 恒同步恒快（入队），业务执行在 tick 内。好处：网络线程直接调 Submit 不阻塞；回放/测试拿到天然确定性；背压（队列深度）成为显式信号。备选"Submit 即执行"在单机省一层队列，但并发提交、Drain 语义、回放记录全部复杂化。

**D2｜owner 粒度默认按 Subject（推荐）**：`OwnerPolicy.BySubject` 把同来源命令串行（防玩家自己操作乱序），`WholeInstance` 服务全局结算类命令。MMO 的 World 级串行由 `Custom(OwnerKey)` 表达（WorldId 作 SubKey）——不内置"World"概念，用子键组合。

**D3｜要求-响应命令只给内部（推荐）**：`SubmitAsync<T,TResult>` 是 Port 语义的糖（内部模块间调用），网络入口必须用无返回 Submit + 事件/推送回执——防止把 RPC 习惯带进模拟层（响应超时、重试语义会污染管线）。

**D4｜队列满策略：拒绝新输入（推荐默认）**：拒绝提交方并返回 `RejectedQueueFull`——背压上抛比静默丢旧（掉命令！）或无界增长（OOM）安全。`RejectOldest` 作为可选策略留给"只要最新"的输入（如移动朝向），按命令处理器配置。

**D5｜事件投递模型：同步有序直推（推荐）**：处理器发布事件 → 当前 tick 结束后按序推给订阅者（同 tick 内 FIFO）。订阅者慢不阻塞模拟：订阅者拿到的是有序信封，自己排队缓冲。不做全局事件总线自动广播所有类型——订阅显式到类型，配 CAP-00 Contribute 时声明，Validate 可查"发布无人订阅"给提示（警告级）。

**D6｜异常处理：处理器异常 = 模块故障**：沿 CAP-00 语义（必需模块回滚/可选降级）；管线自身保证：异常发生在 owner 上下文内，该 owner 队列停止消费并上报，其他 owner 不受染（隔离失败）。

## 5. 不变量

1. 同一 OwnerKey 的命令严格 FIFO、串行执行；不同 owner 不保证相互顺序。
2. 事件按产生顺序投递；`EventSequence` 全流单调无间隙。
3. Drain 后 Submit 返回 `RejectedDraining`；已入队命令全部处理完毕才允许 Stop（与 CAP-00 Drain 语义一致）。
4. mutation 处理器必须幂等（同 `(Subject, OperationId)` 重复提交只执行一次）——幂等登记属 CAP-02/08，管线在提交边界执行。
5. 禁止在命令处理器内直接提交"要求-响应"命令（自等待死锁面）；跨命令协作用事件。
6. 查询不走命令总线：状态读走 Port（CAP-06 D4）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 处理器抛异常 | 所属 owner 队列停摆 + 上报故障；必需模块按 CAP-00 回滚，可选模块 Degraded |
| 队列满 | 按 D4 策略拒绝，`SubmitStatus` 可观察 + 诊断计数 |
| 幂等重复 | 返回首次结果（登记表），不重复执行 |
| 事件订阅者抛异常 | 记诊断并继续投递后续订阅者与事件（订阅者故障不进模拟）；反复失败由 CAP-19 健康上报 |
| Drain 后仍有在途 owner 未排空 | Stop 阻塞等待（带超时，超时 RuntimeFault） |

## 7. 使用方案

- **U1 Headless 测试**：提交 N 命令 → `AdvanceAsync(1)` → 断言事件序列；`RejectedDraining`/队列满用例直接驱动。
- **U2 单机**：输入系统每帧 Submit；本地回显事件驱动表现刷新。
- **U3 联机权威**：网络 handler 只解析、鉴权、Submit（顶层 6 规则 4）；客户端按 `EventSequence` 补包。
- **U4 在线多 World**：每 World 一个 owner（SubKey=WorldId）；跨 World 协作用事件 handoff（不共享队列）。

## 8. 与其他能力的关系

- 依赖：CAP-00（生命周期）、CAP-02（信封与幂等）、CAP-04（执行节奏绑定 clock step）、CAP-06（处理器经 Context 触达状态）。
- 下游：CAP-08（提交边界的权威校验）、CAP-10（回放记录"何时何序"）、CAP-16（网络入口纪律）、CAP-17（投影消费事件）。

## 9. 非目标

- 不做发布/订阅中间件语义（重试、死信、优先级队列）——事件是模拟事实流，不是 MQ。
- 不做分布式事务/两阶段提交；跨 owner 协调是 CAP-13 与业务模式职责。
- 不内置命令校验 DSL（鉴权、配额在 CAP-08 的 AuthorityModel 与 adapter 层）。

## 10. 验收证据

- FIFO 与 owner 隔离测试（含并发提交验证串行化）。
- Drain 全场景：Drain 后拒绝、在途排空、Stop 超时。
- 幂等重试、队列满背压、订阅者故障隔离、处理器异常 owner 隔离测试。
- 确定性：同命令序列两实例事件流逐位一致（配合 CAP-09）。

## 11. 审核决策点

- **Q1**：D1 提交与执行分离（执行绑定 clock step）——同意？这是 B 域最重要的一刀。
- **Q2**：D2 owner 默认按 Subject、World 用 SubKey 表达——同意不内置 World 概念？
- **Q3**：D3 要求-响应命令仅限内部、网络入口禁用——同意？
- **Q4**：D4 队列满默认拒绝新输入——同意？
- **Q5**：D5 事件同步有序直推、订阅者自缓冲——同意不做自动广播总线？
