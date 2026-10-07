# CAP-10 回放 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Simulation` | 前置：CAP-07、CAP-09
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

从初始状态重建任意一段过程，并指出两份执行从哪里开始分歧。这是"世界是否确定"的最终裁判，也是排障效率的分水岭：有回放，玩家一句"刚才掉线前我明明捡到了"可以从事故变成可重现的用例；没有回放，只能靠日志猜。

**MMO 驱动力**：
- 申诉与审计：复盘一段争议操作（交易、掉落、PVP 结果）。
- 会话恢复：服务器切换/重启用 checkpoint + 补播重建会话（与 CAP-12/15 配合）。
- 反作弊取证：输入序列 + 世界演化完整可查。

**通用性**：单机回放/观战是玩家可见功能；测试环境的确定性断言（哈希比较）是回放合同的最小子集。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IReplayRecorder` | 挂在管线上记录输入流 |
| `IReplayStream` | 一段可重放的输入序列（读面） |
| 回放输入 | 初始快照 + 命令序列（含时钟节奏与随机策略），别无他物 |
| `IReplayDiffer` | 比较两份执行，报告首个分歧 |
| `ReplayMode` | 记录随机消费与否（CAP-05 D5 的模式选择落点） |

## 3. 合同形状

```csharp
public enum ReplayRandomMode { ReseedOnly, RecordConsumption }

public interface IReplayRecorder : IAsyncDisposable
{
    /// <summary>绑定到 GameInstance；Submit 与 clock step 经由 recorder 包装自动记录。</summary>
    void RecordCommand(in RecordedCommand command);
    void RecordTickBoundary(long completedTick);
    /// <summary>ReplayRandomMode.RecordConsumption 时记录每次随机消费结果。</summary>
    void RecordRandomDraw(string streamName, in RandomDraw draw);
    ValueTask<IReplayStream> CompleteAsync(CancellationToken ct);
}

public readonly record struct RecordedCommand(long Tick, CommandEnvelopeMetadataOnly Metadata, TypeId CommandType, ReadOnlyMemory<byte> Payload);
public readonly record struct RandomDraw(string StreamName, long SequenceInStream, int ValueDrawn);

public interface IReplayStream
{
    ReplayHeader Header { get; }               // 起点快照引用（或内嵌）、模式、起止 tick、源实例
    IAsyncEnumerable<ReplayEntry> ReadAsync(CancellationToken ct);
}

public sealed record ReplayHeader(
    ISimulationSnapshot? InitialSnapshot,      // 内嵌或外部引用（外置时为 ContentReference）
    ReplayRandomMode RandomMode,
    long StartTick, long EndTick,
    string ProducerVersion);                   // 记录端框架版本（诊断用，不参与重放）

/// <summary>重放驱动：给一个干净的 GameInstance 喂流。</summary>
public interface IReplayPlayer
{
    /// <summary>重放到 EndTick；strict=true 时任何偏差立即失败。</summary>
    ValueTask<ReplayOutcome> PlayAsync(IReplayStream stream, GameInstanceOptions options, CancellationToken ct);
}

public sealed record ReplayOutcome(
    bool Completed, long ReplayedTicks, StateHash FinalHash,
    DivergencePoint? Divergence);

public readonly record struct DivergencePoint(long Tick, string Kind, string Detail, StateHash? Expected, StateHash? Actual);
```

**记录挂载**：Recorder 包装 `ICommandBus.Submit` 与 `IClockDriver.AdvanceAsync`（装饰器），业务与宿主代码无感；测试可直接用未包装总线。

## 4. 关键设计决策（含推荐）

**D1｜回放 = 快照 + 命令流，记录即时钟节奏（推荐）**：Variable 模式的每步步长必须记录（`RecordTickBoundary` 附带步长）；Fixed/Turn 只记边界。任何"重放时从环境猜"的信息都不允许——这是确定性紀律的执行器。

**D2｜随机模式默认 ReseedOnly（推荐，接 CAP-05 D5）**：默认只记种子，重放重算随机（流小、依赖算法锁定）；`RecordConsumption` 模式每笔消费入流，用于"算法升级后仍要回放旧录像"或"怀疑随机实现有 bug"的场景。两种模式在流头声明，混用按流头为准。

**D3｜分歧定位：首个偏差即停（推荐）**：比较两份流（或重放结果 vs 期望哈希）时，报告**第一个**不一致的 tick 与类型（命令缺失/事件序列漂移/哈希漂移），不再继续——后续差异全是第一个的涟漪，继续比较制造噪音。哈希检查点：按 tick 间隔（流头配置）插入期望哈希，缩小定位窗口。

**D4｜流存储格式复用快照容器（推荐）**：回放文件 = 快照容器格式（CAP-09）+ 追加段（命令流）；外置大快照用引用（`ContentReference`，由 CAP-15/18 承载存储位置）。避免再造一种容器格式。

**D5｜记录默认关闭、显式开启（推荐）**：Recorder 是装饰器，不默认挂载。MMO 全量记录命令流存储/IO 成本高；生产环境按 World/按会话采样开启（运营决策）。但 U1/U2 的确定性测试必须强制开启——Profile 配置化。

## 5. 不变量

1. 同一 stream 在算法锁定前提下重放结果逐位确定；`PlayAsync(strict)` 失败即框架确定性 bug 或流损坏。
2. 重放不产生"新类型副作用"：重放实例的对外事件投递默认静默（或投给测试收集器）——重放不向真实网络/存档写东西。
3. 流头与内容自洽：`StartTick` 与初始快照 Tick 一致；`RandomMode` 与内容一致。
4. 记录与重放使用同一管线路径（同一 Submit/Advance 包装），不允许旁路。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 重放遇未知命令 TypeId | 失败并指出流位置与 TypeId（流版本与代码不匹配） |
| 期望哈希不匹配 | 产出 `DivergencePoint`，strict 模式终止；非 strict 继续并汇总所有检查点差异 |
| 初始快照缺失/不可解析 | 流无效，拒绝重放 |
| 记录中途崩溃 | 流尾部不完整：恢复策略 = 丢弃最后一个未闭合 tick（写流时按 tick 边界刷盘） |

## 7. 使用方案

- **U1 Headless 测试**：录制一次完整会话作为 fixture；CI 中重放 + 终态哈希断言；人为篡改流验证分歧定位。
- **U2 单机**：观战/回放功能直接复用（记录开关在游戏设置里）。
- **U3 联机权威**：按会话采样记录；客服申诉调流重放；哈希检查点做服务器间状态比对。
- **U4 在线多 World**：World 级记录（流 = World 的命令序列）；服务器迁移 = 目标机重放到切换点接管。

## 8. 与其他能力的关系

- 依赖：CAP-07（挂载点）、CAP-09（起点快照与哈希检查点）、CAP-05（随机模式）、CAP-03（流数据编码）。
- 下游：CAP-12（会话恢复 = checkpoint + 补播）、CAP-15/18（外置快照引用）、CAP-19（记录异常上报）。

## 9. 非目标

- 不做视频式回放（画面录制是表现层功能）。
- 不做流压缩与切片索引（存储优化后续按需）。
- 不做在线实时镜像重放（影子服）——那是运营部署形态，不是合同。

## 10. 验收证据

- 录制-重放终态哈希一致测试（两样例必跑）。
- 分歧定位测试：篡改流中一条命令 → 报告精确 tick 与类型。
- 随机双模式测试：ReseedOnly 与 RecordConsumption 各自往返一致；算法替换场景下 RecordConsumption 流仍可重放。
- 崩溃恢复测试：流截断在 tick 中间 → 丢弃尾 tick 后仍可重放。

## 11. 审核决策点

- **Q1**：D1 时钟节奏入流（Variable 步长逐条记录）——同意？
- **Q2**：D2 随机默认 ReseedOnly、RecordConsumption 可选——同意？
- **Q3**：D3 首个分歧即停 + 按间隔哈希检查点——同意？
- **Q4**：D5 记录默认关闭、按 Profile/采样开启，但测试环境强制——同意？
