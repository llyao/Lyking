# CAP-30 日志与遥测 · 设计草稿

> 状态：**待审核** | 能力域：G 服务器核心模块（合同两侧通用） | 目标工程：合同进 `GameFramework.Contracts`，导出实现进 `GameFramework.Adapters.Observability.*` | 前置：CAP-19、CAP-02
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

"刚才发生了什么"的记录面。与 CAP-19 指标（数值聚合）分工：**日志 = 离散事件记录**（谁在何时做了什么、拒绝了什么、为什么）。没有统一日志合同：每个模块自己 Console.WriteLine、没有上下文关联（一条玩家操作的服务端全链路拼不起来）、服务器日志无法与客户端日志对账。

**MMO 驱动力**：
- 链路关联：一次玩家操作 = 命令（OperationId）→ 权威校验 → 管线执行 → 事件 → 持久化，跨模块跨进程——`OperationId`/`EventSequence`（CAP-02）必须自动织入日志上下文，人肉传参必然漏。
- 申诉取证与安全审计：交易/掉落/PVP 的审计要求日志结构化可检索。
- 双端对账：客户端与服务端日志共用格式与字段约定（LogScope 同一合同），排障时能对齐。

**通用性**：客户端崩溃日志与服务器审计日志同一合同、不同 sink；Headless 测试同样输出（测试失败时上下文齐全）。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ILogger` / `ILoggerFactory` | 分类日志器（按模块/子系统命名） |
| `LogScope` | 结构化上下文字段集（自动织入：ModuleId、InstanceId、SubjectId、OperationId…） |
| `LogEvent` | 一条结构化事件：级别、类别、模板、字段、时间戳 |
| `ILogSink` | 导出面：控制台/文件/网络（adapter） |
| 审计事件 | 安全敏感操作的结构化子类（必填字段集） |

## 3. 合同形状

```csharp
public enum LogLevel { Trace, Debug, Info, Warn, Error, Fatal }

public interface ILogger
{
    string Category { get; }
    bool IsEnabled(LogLevel level);
    /// <summary>结构化写日志：模板 + 命名字段（不拼字符串进模板）。</summary>
    void Log(LogLevel level, string template, IReadOnlyDictionary<string, object?>? fields = null);
    /// <summary>异常便捷重载（含堆栈与分类字段）。</summary>
    void Log(LogLevel level, Exception exception, string message);
}

public interface ILoggerFactory
{
    ILogger Get(string category);
    /// <summary>压入作用域字段：期间所有日志自动携带（异步流经 ambient 传递）。</summary>
    LogScope BeginScope(LogScopeFields fields);
}

public sealed class LogScopeFields
{
    public LogScopeFields Set(string key, object? value);
    // 约定保留键（自动织入点，见 D3）：
    // "module" / "instance" / "subject" / "operation" / "event-seq" / "world" / "session"
}

public interface ILogSink : IAsyncDisposable
{
    /// <summary>结构化事件投递。Sink 自行决定格式化（JSON 行等）与目标。</summary>
    ValueTask EmitAsync(LogEvent @event, CancellationToken ct);
}

public sealed record LogEvent(
    LogLevel Level, string Category, string Template,
    IReadOnlyDictionary<string, object?> Fields,
    DateTimeOffset Timestamp,        // 墙钟时间戳：日志属运维面（CAP-19 D4 同理），合法
    long? LogicalTick);              // 模拟侧事件可选携带逻辑 tick（两端时间并存）
```

**审计事件约定**：交易/掉落/权限变更/封禁类操作必须以 `LogEvent` + 固定字段集（`audit.kind`、`subject`、`operation`、`before`/`after` 摘要）记录为 `Warn` 以上级别——审计字段完整性有测试把守（见验收）。

## 4. 关键设计决策（含推荐）

**D1｜结构化优先、模板+字段（推荐）**：模板字符串 + 命名字段，sink 决定渲染成人读或 JSON 行。禁止"只有拼接字符串"的日志 API——字段化才可检索可聚合。

**D2｜日志与指标分工明确（推荐）**：数值趋势 → CAP-19 `IMetricsCollector`；离散事件与原因 → 本能力。同一条信息不双写（拒绝计数是指标；"拒绝原因分布变化"需要时用带 reason 字段的 Warn 日志，两面对账）。

**D3｜上下文自动织入（推荐）**：`BeginScope` 压入的字段经 async 流（AsyncLocal 等价物）自动跟随——管线在命令执行上下文自动压入 `operation/subject/instance`，业务代码零传参获得全链路关联。这是 CAP-02 元数据投资的红利兑现。

**D4｜级别纪律（推荐约定）**：Trace=开发期细节；Debug=诊断细节（生产默认关）；Info=状态转换（启动/连接/World 就绪）；Warn=可恢复异常与审计事件；Error=功能受损（Degraded）；Fatal=进程将死。生产默认级别 Info，可按类别动态调（运维工具，非热更）。

**D5｜墙钟与逻辑时间并存（推荐）**：`Timestamp`（墙钟，运维对账）+ `LogicalTick`（可选，模拟事件带）——回放对账时按逻辑时间，运维检索时按墙钟。两者共存不冲突（CAP-04 D4 只约束模拟内部）。

**D6｜同步写、异步发（推荐）**：`Log()` 调用同步返回（写入有界内存缓冲），sink 异步刷盘——热路径零 IO 等待；缓冲满按级别丢弃（先丢 Trace/Debug，永不丢 Warn 以上）。崩溃时尽力 flush（与崩溃上报 adapter 协作）。

## 5. 不变量

1. 核心与模块只依赖 `ILogger`/`ILoggerFactory` 合同；具体 sink 与格式在 adapter。
2. Scope 字段自动跟随异步流；业务不手动透传关联键。
3. 模板不含插值结果（字段化），模板本身稳定可枚举（便于文档化与采样）。
4. Warn 以上永不静默丢弃（缓冲丢弃顺序 D6）。
5. 审计事件字段集完整（缺字段 = 测试失败）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| sink 故障（磁盘满/网络断） | 缓冲环形覆盖低级别事件；恢复后继续；健康上报 |
| 字段序列化异常（怪对象） | 降级为 `ToString()` + 字段名标注；不抛出——日志故障不能反噬业务 |
| 模板重复注册（不同模块同模板不同字段） | 允许（模板按类别隔离）；采样统计可见 |

## 7. 使用方案

- **U1**：内存 sink 断言事件序列与 scope 织入；失败上下文完整性（测试报错附带最近日志）。
- **U2 单机**：文件 sink 滚动 + 崩溃 flush。
- **U3 联机权威**：JSON 行 sink → 采集管道；审计事件专项检索。
- **U4 在线多 World**：按 World/实例分流的 sink 标签；跨进程链路靠 OperationId 串联。

## 8. 与其他能力的关系

- 依赖：CAP-19（指标分工边界、健康事件源）、CAP-02（关联键来源）。
- 下游：全部模块（消费面）；CAP-27（日志初始化早于一切）、CAP-30 与崩溃上报 adapter 协作。

## 9. 非目标

- 不做分布式追踪 Span 树（OpenTelemetry 接入属 adapter 扩展；本合同字段已含关联键）。
- 不做日志检索与存储平台（部署层）。
- 不做日志脱敏引擎（敏感字段规范是项目纪律，合同提供字段级控制点）。

## 10. 验收证据

- scope 织入测试：异步链路中关联键全程携带。
- 缓冲丢弃顺序测试：缓冲满时 Trace 先丢、Warn 永存。
- 审计字段完整性测试：审计类调用缺字段即红。
- sink 故障注入：业务零感知、恢复续写。
- 性能基准：热路径 Log 单次分配低于阈值（可重复）。

## 11. 审核决策点

- **Q1**：D3 自动织入（AsyncLocal 类 ambient scope）——接受这种隐式传递吗？（备选：显式传 LogContext 参数，啰嗦但无魔法）
- **Q2**：D6 缓冲满按级别丢弃（Warn 以上永不丢）——同意？
- **Q3**：D4 级别纪律与生产默认 Info——确认？
- **Q4**：审计事件作为日志子类（固定字段集 + 测试把守）——同意，还是需要独立审计通道？
