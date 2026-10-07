# CAP-19 健康与降级（运行期） · 设计草稿

> 状态：**待审核** | 能力域：E 运维能力 | 目标工程：`Share/GameFramework.PluginHost`（扩展既有 `ModuleHealthRegistry`） | 前置：CAP-00（启动期已落地）
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

启动期健康已落地（`HostState` 状态机、`ModuleHealthRegistry`、必需失败回滚/可选 Degraded、22 用例覆盖）。**运行期**仍是空白：模块跑起来之后挂了、变慢了、降级了，宿主没有统一的观察与上报面——运维只能翻日志。

**MMO 驱动力**：
- 负载摘除：World 不健康要能从网关路由摘除（CAP-13 的前置信号）。
- 降级可观测：可选模块 Degraded 后，"哪些功能没了、为什么"必须可查询（客服与运营依赖）。
- 容量证据：tick 耗时、队列深度、拒绝率的趋势数据是扩容依据。

**通用性**：单机游戏一样需要（崩溃上报、性能遥测）；合同最小化，上报后端（监控平台）是 adapter。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IRuntimeHealthProbe` | 模块可选实现的运行期自检（周期调用） |
| `ModuleHealthReport`（扩展） | 启动期报告 + 运行期状态（Degraded 原因、指标） |
| `IHealthSink` | 健康观测出口：快照查询 + 变更推送 |
| 指标 | 内置计数器（命令拒绝率、队列深度、tick 耗时、事件积压）+ 模块自定义 |

## 3. 合同形状

```csharp
/// <summary>模块可选实现。宿主按配置周期调用；异常按该模块 Degraded 处理。</summary>
public interface IRuntimeHealthProbe
{
    /// <summary>快而纯：不等待 IO、不锁长资源。超时视为失败。</summary>
    HealthProbeResult Probe(HealthProbeContext context);
}

public sealed record HealthProbeResult(
    HealthState State,                    // Healthy / Degraded / Faulted
    string? Detail,                       // Degraded/Faulted 必填：为什么
    IReadOnlyDictionary<string, double>? Metrics = null);

/// <summary>宿主侧健康观测出口。</summary>
public interface IHealthSink
{
    /// <summary>当前全量快照（负载均衡器/运维轮询用）。</summary>
    HealthSnapshot Current();
    /// <summary>状态变更推送（Degraded↔Healthy 转换时）。</summary>
    event Action<HealthTransition>? OnTransition;
}

public sealed record HealthSnapshot(
    HostState Host,
    IReadOnlyList<ModuleHealthEntry> Modules,
    HealthSummary Summary,               // 就绪/降级/故障计数
    DateTimeOffset SampledAt);           // 观测时间戳（墙钟只出现在运维面，不进模拟）

public sealed record ModuleHealthEntry(
    ModuleId Module, HealthState State, string? Detail,
    IReadOnlyDictionary<string, double> Metrics,
    TimeSpan Uptime, int RestartCount);  // RestartCount：模块级重启次数（CAP-20 热更相关）

/// <summary>内置指标面：各能力按约定写入（管线写拒绝率、快照写耗时……）。</summary>
public interface IMetricsCollector
{
    void Increment(string name, long delta = 1, IReadOnlyDictionary<string, string>? tags = null);
    void Gauge(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
    /// <summary>耗时直方图（tick 耗时、快照耗时）。</summary>
    void Histogram(string name, double valueMilliseconds, IReadOnlyDictionary<string, string>? tags = null);
}
```

**内置指标（各能力接线）**：`commands.rejected{reason}`（CAP-07/08）、`queue.depth{owner}`、`tick.duration_ms`（CAP-04 宿主计时）、`events.backlog`（CAP-07 订阅者积压）、`snapshot.duration_ms`、`persistence.failures`（CAP-15）、`transport.connections / reconnects`（CAP-16）、`health.probe.failures`。

**降级语义（沿用并延伸已落地规则）**：

| 状态 | 进入条件 | 出口 |
|---|---|---|
| Healthy | 探测通过 | — |
| Degraded | 可选模块 Start 失败（已有）/ 探测返回 Degraded / 依赖的适配器故障 | 探测恢复 → Healthy |
| Faulted | 必需模块运行期故障（管线 owner 停摆未恢复）| 模块级重启（策略见 D3）或实例停机 |

## 4. 关键设计决策（含推荐）

**D1｜探测在模块、汇总在宿主（推荐）**：模块最懂自己的健康（连接池水位、内部队列），宿主只做周期调度、超时裁决与汇总。探测超时上限（默认 2s）全局配置，超时 = Degraded("probe timeout")——慢探测本身就是不健康。

**D2｜指标面最小化（推荐）**：`Increment/Gauge/Histogram` 三原语，无维度模型库依赖；导出（Prometheus/自定义）属 adapter。热路径写入 O(1)（内部原子计数），绝不做字符串格式化——tags 只在低频路径使用。

**D3｜运行期故障的重启策略（推荐：默认不自动重启）**：必需模块运行期 Faulted → 实例进入 Drain → 停机（fail-closed，与启动期哲学一致）；**可选**模块 Faulted → Degraded 停用。自动模块级重启（带状态恢复）是危险能力（半套状态、代际混乱），第一版不做，把"重启"留给实例级（进程管理器拉起 → checkpoint 恢复，CAP-12/15 路径）。`RestartCount` 字段为未来模块级热重启预留观测。

**D4｜墙钟只出现在运维面（推荐）**：`HealthSnapshot.SampledAt` 用真实时间（运维对照日志）；模块探测与指标内部不读墙钟（CAP-04 纪律不破例）。

**D5｜降级原因留存（推荐）**：每次状态转换记录（时间、原因、指标快照）环形保留最近 N 条（默认 100）——"为什么降级"必须在事后可查，不能只靠当时日志被滚动掉。

## 5. 不变量

1. 健康观测是旁观者：探测与指标写入不阻塞模拟热路径；探测异常不影响模块业务状态机。
2. Degraded 是显式状态：不存在"静默半坏"——要么 Healthy 要么有原因的 Degraded/Faulted。
3. 必需模块 Faulted ⇒ 实例走向停机（不隐瞒、不自动续命）。
4. 墙钟不出现在模拟内部指标；逻辑时间进指标 tag 由模块自担（如 `tick` 维度）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 探测超时/异常 | 该模块 Degraded（带原因），继续调度（下次周期再探） |
| 指标写入并发 | 原子操作；不丢不锁 |
| 健康快照与实际状态竞争 | 快照一致性到"单模块原子"粒度；全局一致性靠 SampledAt 标注（运维语义足够） |
| 上报 adapter 故障 | 环形缓冲继续积累；上报恢复后补发最近快照（尽力而为，运维面可容忍缺口） |

## 7. 使用方案

- **U1 Headless 测试**：注入假探针驱动状态转换断言；指标计数断言。
- **U2 单机**：崩溃前健康快照随崩溃上报（adapter）。
- **U3 联机权威**：健康快照暴露给负载均衡（`Current()` 轮询）；拒绝率异常告警。
- **U4 在线多 World**：World 级健康聚合；摘除路由的信号源（CAP-13）。

## 8. 与其他能力的关系

- 依赖：CAP-00（启动期基础、生命周期钩子）、CAP-07/08/09/15/16（指标接线点）。
- 下游：CAP-13（摘除/调度信号）、CAP-20（RestartCount 与热更观测）、运维 adapter（监控对接）。

## 9. 非目标

- 不做告警规则引擎（监控平台职责）。
- 不做分布式追踪（trace id 贯穿属后续观测专项；`OperationId` 已提供关联键）。
- 不做自动故障转移编排（实例级由部署层；World 级属 CAP-13）。

## 10. 验收证据

- 探针状态转换测试（Healthy→Degraded→Healthy、超时、Faulted 停机路径）。
- 指标并发正确性测试；热路径零分配基准（可重复）。
- 转换历史环形留存测试；上报断连恢复补发测试。
- 与既有 22 用例回归共存（不破坏启动期语义）。

## 11. 审核决策点

- **Q1**：D3 默认不做模块级自动重启（必需模块故障 → 实例停机；可选 → Degraded）——同意？这是"自动续命 vs fail-closed"的立场题。
- **Q2**：D2 指标面只做三原语、导出走 adapter——同意？
- **Q3**：D5 转换历史环形留存（默认 100 条）——同意？
- **Q4**：探测周期默认值（建议 30s，可按模块覆盖）——确认？
