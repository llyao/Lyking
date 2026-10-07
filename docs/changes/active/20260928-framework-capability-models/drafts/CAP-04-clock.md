# CAP-04 时钟模型 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Kernel` | 前置：CAP-01
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

模拟推进需要可注入、可控制的时间。业务代码一旦读宿主墙钟（`DateTime.Now`、Unity `Time.time`），确定性、回放、跨宿主哈希全部失效——这是回放类能力的前置死结。

**MMO 驱动力**：
- 服务端权威 tick：世界按固定步长推进，全体玩家看到同一世界状态序列。
- 断线补播：客户端按事件里的逻辑时间补播错过的 tick。
- 回合 MMO/策略：tick 概念可以退化为回合号，同一合同覆盖。

**通用性**：动作、回合制、放置类游戏对"时间"的需求差异极大；核心合同只定义"逻辑时间从哪来"，三种模式（fixed/variable/turn）作为合同内的显式选择。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ISimulationClock` | 逻辑时间的只读视图：tick、累计逻辑时间、模式 |
| `ClockMode` | Fixed（固定步长）/ Variable（可变步长）/ Turn（回合号） |
| `IClockDriver` | 推进器：谁能推进、按什么节奏推进（宿主/测试持有） |
| `LogicalTime` | long：Fixed/Variable 下为 tick 数，Turn 下为回合号；跨模式不比较 |
| 步长 `StepSize` | Fixed 模式的每 tick 时长；Variable 模式为上一 tick 实际时长 |

## 3. 合同形状

```csharp
public enum ClockMode { Fixed, Variable, Turn }

public interface ISimulationClock
{
    ClockMode Mode { get; }
    long Tick { get; }              // 已完成的 tick/回合数，从 0 起
    long LogicalTime { get; }       // = Tick（第一版两者相等，分开命名留演进空间）
    TimeSpan StepSize { get; }      // Fixed: 配置值；Variable: 上一步实际值；Turn: TimeSpan.Zero
    bool IsRunning { get; }         // Drain 后为 false
}

/// <summary>推进器。测试直接调用；宿主按 profile 节奏调用；业务模块拿不到。</summary>
public interface IClockDriver
{
    ISimulationClock Clock { get; }
    /// <summary>推进一步。Variable 模式必须传入步长；Fixed/Turn 忽略参数。</summary>
    ValueTask<StepResult> AdvanceAsync(CancellationToken ct);
    ValueTask<StepResult> AdvanceAsync(TimeSpan variableStep, CancellationToken ct);

    /// <summary>Drain：停止接受推进请求，等待在途步完成。</summary>
    ValueTask DrainAsync(CancellationToken ct);
}

public readonly record struct StepResult(long CompletedTick, TimeSpan StepUsed);
```

**获取方式**：`IClockDriver` 经 CAP-00 Contribute 由内核模块贡献；业务模块构造注入 `ISimulationClock`（只读视图）——类型系统上拿不到推进器。

## 4. 关键设计决策（含推荐）

**D1｜三种模式同一合同（顶层 4.3 既定）**：`ClockMode` 区分，`LogicalTime` 语义随模式注释说明；不做 `IFixedClock`/`ITurnClock` 拆分接口——拆分的收益（禁止误用 Variable 步长）低于类型爆炸成本，模式错误在 StepResult 断言与测试中暴露。

**D2｜推进归属：宿主独占（推荐）**。Fixed 模式宿主可以追赶补 tick（`AdvanceAsync` 循环调用），也可以让"一渲染帧 = 一 tick"；这是宿主节奏，内核不内置自动泵——备选"内核自带 tick 泵线程"被拒绝：MMO 服务端要自己的调度节奏（与网络 I/O 交错），Unity 客户端要跟随渲染帧，泵放核心必然猜错。

**D3｜时间起点与暂停（推荐）**：`Tick` 从 0 起，快照携带 Tick 值恢复后继续；不提供 `Pause()`——暂停是宿主停止调用 Advance（简单、无隐藏状态）。Variable 模式的步长上限由 Profile 配置（防止单步过大跳帧穿透碰撞检测），内核校验上限并在超限时 clamp + 诊断。

**D4｜真实时间只做观测（推荐）**：`ISimulationClock` 不暴露墙钟；需要"现实超时"（如玩家 30 秒不出牌）的场景，由 adapter 以外部命令（`KickIdleCommand`）形式进入模拟——保持模拟只依赖逻辑时间。

## 5. 不变量

1. 业务模块只能注入 `ISimulationClock`（只读）；`IClockDriver` 只出现在宿主与测试组装根。
2. `LogicalTime` 单调不减；Drain 后 Advance 拒绝。
3. Fixed/Turn 模式忽略自定义步长参数并返回诊断（不静默收下）。
4. 快照（CAP-09）必须包含 `(Mode, Tick, StepSize)`；恢复后哈希一致要求三者一致。
5. Variable 步长超过 Profile 上限时 clamp 并记诊断。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| Drain 后 Advance | 返回失败 StepResult（`IsRunning=false` 诊断），不抛异常 |
| Variable 步长 ≤ 0 | 参数错误，立即抛 `ArgumentOutOfRangeException`（编程错误） |
| 模式与参数不匹配 | 忽略多余参数 + 记诊断（宽容，因为测试样板常带参调用） |
| 时钟倒退（不可能路径） | RuntimeFault |

## 7. 使用方案

- **U1 Headless 测试**：`AdvanceAsync` 循环手推；断言 `Tick` 与事件里 `LogicalTime` 一致。
- **U2 单机**：Fixed（动作/帧同步感）或 Variable（跟随渲染帧）；回合制用 Turn。
- **U3 联机权威**：服务端 Fixed 固定节奏 tick；客户端时钟只做表现插值，不进模拟。
- **U4 在线多 World**：每个 LiveWorld 一个独立时钟实例；MetaWorld 通常 Turn/低频 Fixed；时钟节奏由 Live host 合同决定（顶层 4.6）。

## 8. 与其他能力的关系

- 依赖：CAP-01（无 ID 需求，仅类型纪律）、CAP-00（贡献与生命周期）。
- 下游：CAP-02（EventMetadata.LogicalTime）、CAP-05（随机序列与 tick 绑定）、CAP-09/10（快照与回放记录 Tick）、CAP-07（事件顺序 = tick 顺序）。

## 9. 非目标

- 不做帧率平滑、时间缩放（慢动作）——那是表现层对 Advance 节奏的控制。
- 不做定时器/调度器能力（"每 5 tick 触发"由模块自行基于 Tick 判断；通用调度器若有真实需求另行立项，不预设）。

## 10. 验收证据

- 三模式推进测试：Fixed 连续推进 N 步 Tick=N；Variable 步长 clamp；Turn 忽略步长。
- Drain 拒绝测试；快照恢复后 Tick 延续测试。
- 确定性测试：同一步长序列推进两个实例，事件流逐位一致（配合 CAP-09 哈希）。

## 11. 审核决策点

- **Q1**：D1 三模式合一合同（而非拆分接口）——同意？
- **Q2**：D2 内核不自带 tick 泵，推进节奏全归宿主——同意？
- **Q3**：D4 不提供 Pause，模拟只依赖逻辑时间、"现实超时"走外部命令——同意？
- **Q4**：Variable 步长上限 clamp 行为——接受"clamp + 诊断"，还是要求"超限即失败"？
