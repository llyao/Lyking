# CAP-28 定时与调度 · 设计草稿

> 状态：**待审核** | 能力域：G 服务器核心模块（合同两侧通用） | 目标工程：`Share/GameFramework.Simulation`（模拟内）+ `GameFramework.ServerHost`（进程级） | 前置：CAP-04、CAP-06、CAP-07、CAP-27
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

"到点发生的事"有两个完全不同的世界：**游戏内定时**（buff 到期、活动开启、建筑完成）属于模拟状态，必须可回放、可快照；**进程级任务**（日志滚动、指标上报、定时备份）属于运维，绝不能进模拟。不分开的话：回放后活动时间错位（模拟内定时没进快照），或者定时备份把模拟时序搅乱（后台任务碰了模拟状态）。

**MMO 驱动力**：
- 活动日历、日常刷新、维护倒计时——全部是"逻辑时间到点触发"，必须随快照/回放移动（服务器切换、回档、跨宿主迁移）。
- 长会话下定时器数量大（每玩家 N 个 buff 计时），调度结构必须可扩展且进快照。

**通用性**：单机游戏同样有游戏内定时（倒计时、冷却）；合同两侧通用，进程级部分只有服务器使用。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ISimulationScheduler` | 模拟内调度器：按逻辑 tick 触发；**状态进快照**（内核内置 IStateContributor） |
| `ScheduledHandle` | 定时器句柄：取消、查询 |
| `IBackgroundTaskRunner` | 进程级后台任务：真实时间驱动、与模拟隔离 |
| `ServerCron` | 服务器运维定时（维护窗口、备份），CAP-27 宿主持有 |

## 3. 合同形状

```csharp
/// <summary>模拟内调度器。经 CAP-00 Contribute 由内核模块贡献；状态归内核 namespace（可快照可回放）。</summary>
public interface ISimulationScheduler
{
    /// <summary>在 triggerTick 触发回调（owner 上下文内执行，同 CAP-07 处理器纪律）。</summary>
    ScheduledHandle Schedule(long triggerTick, Func<ITimerContext, ValueTask> callback, TimerOptions? options = null);

    /// <summary>周期触发（从 startTick 起，每 intervalTick 一次）。</summary>
    ScheduledHandle ScheduleRepeating(long startTick, long intervalTick, Func<ITimerContext, ValueTask> callback, TimerOptions? options = null);

    bool Cancel(ScheduledHandle handle);
    int ActiveCount { get; }
}

public sealed record TimerOptions(string? Name = null, bool PersistAcrossSnapshot = true);

/// <summary>回调上下文：与命令处理器同权（发事件、读时钟），不直接改状态外对象。</summary>
public interface ITimerContext
{
    ISimulationClock Clock { get; }
    IEventSink Events { get; }
    ScheduledHandle Self { get; }      // 自我取消/重排
}

/// <summary>进程级后台任务（只有服务器用）。真实时间驱动；禁止触碰模拟状态。</summary>
public interface IBackgroundTaskRunner : IAsyncDisposable
{
    void Register(IBackgroundTask task);
    ValueTask StartAsync(CancellationToken ct);
    ValueTask DrainAsync(TimeSpan maxWait, CancellationToken ct);   // 停机前等在途任务收敛（CAP-27 流程内）
}

public interface IBackgroundTask
{
    string Name { get; }
    TimeSpan Interval { get; }
    /// <summary>失败不抛出：返回 false 记入连续失败计数（连续失败由 CAP-19 告警）。</summary>
    ValueTask<bool> RunOnceAsync(CancellationToken ct);
}
```

**时序纪律**：

```text
tick N 推进（CAP-04）→ 调度器取出 triggerTick ≤ N 的到期定时器
  → 逐个在 owner 上下文执行（同 tick 内 FIFO）→ 产生的事件正常进事件流
快照（CAP-09）→ 定时器表（triggerTick、参数、周期）作为内核状态块一并 Capture
回放（CAP-10）→ 定时器由重算重建，与原执行逐位一致
```

## 4. 关键设计决策（含推荐）

**D1｜模拟内定时器状态必须进快照（推荐，本能力第一原则）**：调度器实现为内核内置 `IStateContributor`——定时器表（触发 tick、类型、周期、参数索引）随快照走。漏掉这条，"服务器迁移后活动提前/推迟"就是必然后果。回调不序列化（注册制：持久化的是"定时器身份 + 参数"，回调由模块启动时按身份注册回来）。

**D2｜注册制回调 + 稳定身份（推荐）**：定时器身份 = `(OwnerNamespace, TimerName)` 稳定字符串；模块在 Contribute 时声明"我会重建这些定时器"（如同事件处理器注册）。快照只存身份与时间参数——回放/恢复时回调从注册表取，序列化里永远没有委托。

**D3｜真实时间绝不进模拟调度（推荐）**：`ISimulationScheduler` 只认逻辑 tick（CAP-04 D4 延伸）。需要"现实 22:00 开活动"的墙钟语义时：后台任务（进程级）在 22:00 提交一条 `StartEventCommand` 进入模拟——墙钟→命令→逻辑时间，单向转换点显式可见。

**D4｜到期回调在 owner 上下文执行（推荐）**：定时器回调与命令处理器同权同责（发事件、幂等、异常进 owner 隔离）——定时器不是旁门左道，是"系统发出的命令"。避免回调直写状态绕过管线。

**D5｜进程级任务与模拟的三条隔离线（推荐）**：(a) 不读不写 SimulationState；(b) 与模拟交互仅经 ICommandBus 提交命令；(c) 停机时先于模拟 drain 完成收敛（`DrainAsync(maxWait)` 在 CAP-27 终止流程中的位置固定）。备份/上报类任务的 IO 抖动因此不可能影响 tick 时延。

**D6｜同 tick 到期风暴的顺序稳定（推荐）**：同 tick 到期的多个定时器按 `(triggerTick, 创建序)` 稳定排序执行——顺序确定性是回放一致性的前提（CAP-09 哈希可验证）。

## 5. 不变量

1. 模拟内定时器表进快照；恢复/回放后触发序列逐位一致（终态哈希一致的前提件）。
2. 定时器回调身份化；序列化数据里无委托、无闭包。
3. `triggerTick` 必须晚于当前 tick（过去时刻立即拒绝——用 `-1` 立即执行的写法不存在，需要立即动作就发命令）。
4. 进程级任务与模拟的三条隔离线（D5）不可跨越；违反即架构违规。
5. 周期定时器在快照/恢复后继续按原周期（起点不变）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 回调抛异常 | 同命令处理器异常路径（owner 隔离，CAP-07 D6）；周期定时器异常后是否续订由模块在回调内决定（框架不自动复活） |
| 身份未注册（恢复时找不到重建者） | 恢复失败 + 诊断指明缺失身份（快照有效但代码缺失） |
| 取消已触发的句柄 | 幂等返回 false |
| 后台任务连续失败 | 计数上报 CAP-19；框架不自动重启任务（重启策略归 CAP-27/运维） |
| tick 跳变（Variable 大步长） | 到期定时器全部在该步内按序触发（不丢弃） |

## 7. 使用方案

- **U1**：定时器快照往返测试（恢复后触发序列一致）；回放重算测试；到期风暴顺序断言。
- **U2 单机**：冷却、倒计时全走模拟调度器。
- **U3 联机权威**：buff 到期、日常刷新走模拟调度器；定时备份/指标上报走后台任务。
- **U4 在线多 World**：每 World 独立调度器（随 World 快照走）；Meta 日历（全服活动）由专用实例 + 命令广播到各 World。

## 8. 与其他能力的关系

- 依赖：CAP-04（tick 驱动）、CAP-06（状态贡献）、CAP-07（执行上下文）。
- 下游：CAP-09/10（快照与回放含定时器表）、CAP-27（后台任务挂载与停机次序）、CAP-13（Meta 日历广播）。

## 9. 非目标

- 不做 cron 表达式解析（运维定时用简单间隔 + `ServerCron` 适配层；复杂日历是 Features 层）。
- 不做分布式延时队列（跨进程延迟消息属 CAP-13/消息中间件）。
- 不做高精度实时调度（帧级以下精度无意义——模拟粒度是 tick）。

## 10. 验收证据

- 快照往返：建 1000 个定时器 → 快照恢复 → 触发序列逐位一致。
- 回放重算：含定时器的会话回放哈希一致。
- 到期风暴：同 tick 100 个到期按创建序执行。
- 隔离测试：后台任务故障注入不影响 tick 时延（时延分布基准）。

## 11. 审核决策点

- **Q1**：D1/D2 定时器状态进快照 + 注册制回调身份（无委托序列化）——同意？这是回放与迁移成立的前提。
- **Q2**：D3 墙钟语义只能经"后台任务 → 提交命令"单向进入模拟——同意？
- **Q3**：D6 同 tick 到期按创建序稳定执行——同意？
- **Q4**：后台任务停机次序固定为"先于模拟 drain 收敛"——同意？
