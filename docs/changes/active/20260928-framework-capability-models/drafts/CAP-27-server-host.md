# CAP-27 服务器宿主（ServerHost） · 设计草稿

> 状态：**待审核** | 能力域：G 服务器核心模块 | 目标工程：`Share/GameFramework.ServerHost` | 前置：CAP-00、CAP-11/12、CAP-15
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。
>
> 对应顶层任务清单："建立 Server Host、transport adapter、readiness、drain 和 stop"。

## 1. 解决什么问题

服务器进程的完整生命周期：配置加载 → 依赖就绪 → readiness → 运行（一个或多个 GameInstance/World）→ 收到停机信号 → 优雅排空 → 存档 barrier → 退出。没有这层：信号处理各写各的、强杀丢档、readiness 与实际可用性脱节（K8s 把流量打给还没就绪的实例）。

**MMO 驱动力**：
- 滚动更新与缩容：容器编排（K8s 类）靠 readiness/终止宽限期工作——宿主必须按契约行事。
- 多 World 进程：一个进程承载多个 World 实例（分区/多线服），逐 World 独立 drain 与摘除。
- 停服维护：维护窗口 = 全量 drain + 存档 barrier（CAP-15）+ 退出；崩溃恢复链路的起点。

**通用性**：非 MMO 服务端（房间服、匹配服）同一宿主；单进程单实例是默认退化形态。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IServerHost` | 进程编排根：组装 Profile、管理一个或多个 GameInstance |
| `ServerHostOptions` | 配置源、实例清单、宽限期、退出码 |
| `ReadinessState` | 就绪状态：NotReady → Ready → Draining → Stopped（对 CAP-00 HostState 的进程级扩展） |
| 终止流程 | SIGTERM/Ctrl+C → 拒新 → drain → barrier → exit(0) |
| 退出码语义 | 0 正常 / 非 0 分类失败（编排系统据此决定重启策略） |

## 3. 合同形状

```csharp
public interface IServerHost : IAsyncDisposable
{
    /// <summary>加载配置 → 组装实例 → 启动 → 进入运行循环。阻塞至停机信号触发完成。</summary>
    ValueTask<int> RunAsync(CancellationToken ct);   // 返回退出码

    ReadinessState Readiness { get; }
    IReadOnlyList<GameInstance> Instances { get; }
    event Action<ReadinessState>? OnReadinessChanged;

    /// <summary>编程式触发停机（运维命令、自检失败）。</summary>
    ValueTask<int> ShutdownAsync(string reason, CancellationToken ct);
}

public sealed record ServerHostOptions
{
    public required IProfile Profile { get; init; }             // AuthoritativeProfile 等
    public required ServerConfigSource ConfigSource { get; init; }  // 文件/环境/配置中心 adapter
    /// <summary>单实例或实例清单；null = 单 World 默认实例。</summary>
    public IReadOnlyList<WorldSlotOptions>? WorldSlots { get; init; }
    public TimeSpan TerminationGracePeriod { get; init; } = TimeSpan.FromSeconds(30);
    public bool EnableConsoleSignalHandler { get; init; } = true;   // SIGTERM/Ctrl+C
}

public sealed record WorldSlotOptions(string WorldName, long? Seed, WorldSlotPolicy Policy);
public sealed record WorldSlotPolicy(bool StartOnBoot = true, bool RestartOnFault = false); // 默认不自动重启（CAP-19 D3 同立场）
```

**终止流程（固化）**：

```text
SIGTERM / ShutdownAsync(reason)
  → Readiness = Draining（编排系统摘流量：readiness 探针失败）
  → 逐 World：Drain（拒新收旧，CAP-07）→ 存档 barrier（CAP-15，含 checkpoint）
  → 全部成功 → Readiness = Stopped → exit(0)
  → 宽限期到仍有在途 → 记录未排空清单 → checkpoint 抢救 → exit(非 0: 1=超时, 2=barrier 失败)
```

## 4. 关键设计决策（含推荐）

**D1｜readiness 双探针分离（推荐）**：readiness（能不能接流量：模块 Ready、依赖可达、World 挂载完成）与 liveness（进程还活着：死锁检测由 CAP-19 探针兜底）。K8s 语义直接映射；两者混用是滚动更新事故的经典来源。

**D2｜退出码分类（推荐）**：0=干净停机；1=排空超时（已尽力保存）；2=存档 barrier 失败（可能丢数据，人工介入）；3=启动失败。编排系统按码决定"重启 or 告警 or 摘除"——退出码是编排系统的 API。

**D3｜单进程多 World，逐 World 独立生命周期（推荐）**：每 World 一个 GameInstance（独立 owner、时钟、快照），宿主编排它们的启动顺序与依赖（跨 World handoff 依赖的先启）。逐 World drain 支撑"单 World 维护、其他照跑"。跨进程 World 编排不在此层（CAP-13/29）。

**D4｜配置源抽象（推荐）**：`ServerConfigSource` 合同（文件/环境变量/配置中心 adapter 三实现起步），启动期一次性快照进宿主——运行期热改配置走命令/热更（CAP-20），不搞"配置热加载"暗道。

**D5｜崩溃后重启 = 进程级（推荐）**：WorldSlotPolicy 默认 `RestartOnFault=false`——World 故障让进程按 CAP-19 停机，由容器编排重启进程、走 checkpoint 恢复（CAP-12/15 路径）。进程内自动复活 World 是危险能力（半套状态），与 CAP-19 D3 同一立场；需要"World 级隔离故障域"时用多进程拓扑解决。

**D6｜信号处理最早安装（推荐）**：SIGTERM 处理在进程入口最先注册（早于一切可能卡死的初始化），保证任何阶段收到信号都能走完整终止流程或快速失败。

## 5. 不变量

1. 进程退出必经终止流程（信号或编程式二选一入口）；无直接 `Environment.Exit` 路径（审计把守）。
2. Readiness 迁移单向：NotReady → Ready → Draining → Stopped；Draining 后不可回 Ready（摘了流量再接回会撕裂会话）。
3. 存档 barrier 成功是 exit(0) 的必要条件；barrier 失败必须非 0 退出。
4. 每 World 生命周期独立可观测（CAP-19 按 World 维度出健康快照）。
5. 配置快照化：运行中读取的配置不因外部修改而改变。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 启动期依赖不可达（数据库/配置中心） | 有限重试 → 失败 exit(3)，诊断指明依赖项 |
| 某 World 启动失败 | 默认整进程失败 exit(3)（World 是必需件）；`StartOnBoot=false` 的槽位允许缺席 |
| 排空超时 | checkpoint 抢救 + exit(1)，未排空清单进日志/告警 |
| barrier 失败 | 立即停止后续步骤（不做半套保存）+ exit(2) |
| 信号风暴（连收多次） | 第一次进入 Draining；后续信号加速（跳过等待直接抢救退出） |

## 7. 使用方案

- **U1 Headless 测试**：进程内驱动 RunAsync + 编程式 Shutdown，断言终止流程与退出码（无需真信号）。
- **U2**：不适用（单机无 ServerHost）。
- **U3 联机权威**：单实例 + SIGTERM 优雅停机；K8s terminationGracePeriodSeconds > TerminationGracePeriod。
- **U4 在线多 World**：WorldSlots 多实例；逐 World 维护窗口；跨 World 依赖顺序编排。

## 8. 与其他能力的关系

- 组合：CAP-12（Profile）+ CAP-15（barrier）+ CAP-16（监听）+ CAP-19（健康快照）。
- 下游：CAP-28（后台任务挂在宿主）、CAP-29（多进程拓扑时宿主是 World host 节点）、CAP-30（日志在信号处理前初始化）。

## 9. 非目标

- 不做服务发现/注册中心（部署层）。
- 不做跨进程 World 调度与玩家路由（CAP-29）。
- 不做零停机交接（会话迁移）——那是 CAP-29 handoff + CAP-15 存档的组合应用。

## 10. 验收证据

- 进程内终止流程全路径测试：干净停机/排空超时/barrier 失败/启动失败四类退出码。
- 真 SIGTERM 集成测试（子进程发信号）。
- 多 World 独立 drain 测试：单 World 维护不影响其他 World 会话。
- readiness 探针时序测试：模块 Ready 前不返回就绪。

## 11. 审核决策点

- **Q1**：D2 退出码分类作为编排 API——同意这份码表（0/1/2/3）吗？
- **Q2**：D3/D5 多 World 进程内编排 + 故障不自动复活（进程级重启 + checkpoint 恢复）——同意立场？
- **Q3**：D4 配置启动期快照、无热加载暗道——同意？
- **Q4**：Draining 单向（摘除后不自动回 Ready）——确认？
