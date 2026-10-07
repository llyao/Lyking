# CAP-11 LocalProfile · 设计草稿

> 状态：**待审核** | 能力域：C 运行配置 | 目标工程：`Share/GameFramework.Profiles` | 前置：B 域全部
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

单机与测试需要一条**最小完整闭环**：本地权威、本地时钟、无网络、可选文件存档。它是框架的"第一用户"——如果 LocalProfile 组装困难，说明核心合同过度设计；所有样例先用它跑稳，再接 AuthoritativeProfile。

**MMO 驱动力**（为何 MMO 也需要它）：
- 服务端 World 的开发与测试同样在 LocalProfile 下进行（headless、确定性、快迭代），MMO 只是后挂的权威拓扑。
- 离线模式/单机玩法区（大厅、副本单刷）直接复用。

**通用性**：LocalProfile 就是"各类型游戏的公共底座"——动作、回合制、放置类不接 CAP-12/13 也能完整成游戏。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `LocalProfile` | 能力组合器：声明 Local 用况的模块清单与参数 |
| `GameInstance` | 一次可运行模拟的编排根（本能力正式定义其组装流程） |
| `GameInstanceOptions` | 种子、时钟模式、记录开关、存档路径等启动参数 |
| 假 adapter 集 | 内存 loopback、假资源加载器、可选文件持久化 |

## 3. 合同形状

```csharp
public sealed class LocalProfile : IProfile
{
    public LocalProfile(LocalProfileOptions options);
}

public sealed record LocalProfileOptions
{
    public ClockMode Clock { get; init; } = ClockMode.Fixed;
    public TimeSpan FixedStep { get; init; } = TimeSpan.FromMilliseconds(16.666);
    public TimeSpan? MaxVariableStep { get; init; }            // Variable 模式上限
    public long? RootSeed { get; init; }                       // null = 运行时生成并回读
    public bool RecordReplay { get; init; }                    // 测试必须 true
    public ReplayRandomMode RandomMode { get; init; } = ReplayRandomMode.ReseedOnly;
    public string? SnapshotDirectory { get; init; }            // null = 禁用文件存档
    public bool UseMemoryLoopback { get; init; } = true;       // 保留正式 command 入口的假传输
}

/// <summary>Profile 合同：把一组模块与参数组装为可启动的 GameInstance。</summary>
public interface IProfile
{
    /// <summary>返回本 profile 的模块清单（含内核模块与业务模块）。</summary>
    IReadOnlyList<IGameModule> BuildModules(GameInstanceOptions options);
    /// <summary>组装声明：让 Validate 阶段能校验能力完整性（如 clock/authority 必须有人提供）。</summary>
    CompositionRequirements Requirements { get; }
}

/// <summary>组装与运行编排（Kernel 提供，Profile 调用）。</summary>
public sealed class GameInstance : IAsyncDisposable
{
    public static async ValueTask<GameInstance> StartAsync(
        IProfile profile, GameInstanceOptions options, CancellationToken ct);
    public GameInstanceId Id { get; }
    ISimulationClock Clock { get; }
    ICommandBus Commands { get; }
    IReplayRecorder? Recorder { get; }      // RecordReplay=true 时非空
    ISimulationSnapshot TakeSnapshot();
    ValueTask DrainAndStopAsync(CancellationToken ct);
}
```

**组装流程**（固化，等于顶层 3.7 加载流程在单机场景的实例化）：

```text
GameInstance.StartAsync
  → profile.BuildModules → CAP-00 Describe → Contribute → Plan → Validate → Apply(物化容器)
  → 校验能力完整性（clock / authority / random / state 缺提供者即失败）
  → 创建 SimulationState、绑定管线与 AuthorityModel(Local)
  → Start（依赖正序）→ HostState.Ready
```

## 4. 关键设计决策（含推荐）

**D1｜Profile 是组合器不是开关堆（推荐）**：Profile 输出模块清单与要求（`CompositionRequirements`），走 CAP-00 的同一条 Plan/Validate 路径——单机与联机的差异是"清单不同"，不是"核心里 if(profile)"。这保证 4.6 矩阵中"同 Simulation 可替换 HostAdapter"。

**D2｜能力完整性启动即校验（推荐）**：`Requirements` 声明必需 capability（clock、authority、random、state 至少各一提供者）；缺失在 Validate 拒绝启动并列出缺失项——比"运行到一半 NullReference"好一个数量级。

**D3｜本地存档可选且默认关（推荐）**：`SnapshotDirectory=null` 时纯内存（测试快）；给路径即启用文件持久化（CAP-15 的文件 adapter）。单机游戏打开它，测试不打开。

**D4｜内存 loopback 也走正式入口（推荐，顶层 5 节既定）**：U1/U2 即使无网络，命令也从 `ICommandBus` 进、事件也从 channel 出——禁止"测试直接调处理器"的捷径，否则联机接不回去。loopback 只是把传输换成内存。

**D5｜种子回读（推荐）**：`RootSeed=null` 时运行时生成并写进首个快照/回放流头——"没设种子"的运行仍然可复现（从流头读种子）。

## 5. 不变量

1. LocalProfile 必须支持 Headless（顶层 4.6）——不依赖任何显示/输入设备。
2. 即使全本地，快照/回放/哈希入口全部可用且默认自洽（RecordReplay=true 时终态哈希断言可通过）。
3. Profile 不产生新失败路径：失败语义全部来自被组合能力（CAP-00/04/07/09）。
4. 核心程序集不引用 Profiles；Profiles 引用核心（依赖方向，顶层 4.2）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 必需 capability 无提供者 | Validate 拒绝启动，列出缺失能力名 |
| 存档目录不可写 | 启动失败（显式配置了就失败，不静默降内存） |
| 种子未定且熵源不可用 | 极端环境（无熵）启动失败并提示显式传种子 |
| Drain/Stop/快照失败 | 透传对应能力语义 |

## 7. 使用方案

- **U1 Headless 测试**：`RecordReplay=true` + `RootSeed=固定` + 内存全部；样例测试模板固化这组参数。
- **U2 单机**：Fixed/Variable 时钟 + 文件存档 + 表现会话（可选 CAP-17）。
- **U3**：不适用（AuthoritativeProfile 载体）；但其服务端模拟内核开发期在 LocalProfile 下调试。
- **U4**：不适用（HydraOnlineProfile 载体）。

## 8. 与其他能力的关系

- 组合：CAP-04/05/06/07/08(Local)/09/10 + CAP-15(可选文件) + CAP-17(可选表现)。
- 验收样例：`MinimalAction`（动作命令）、`TurnBased`（回合时钟）两样例 × 本 Profile（顶层 4.5）。
- 被引用：AuthoritativeProfile 的服务端内核开发同样以本 Profile 为调试底座。

## 9. 非目标

- 不做"一键打包发布"配置（构建管线属 Tool）。
- 不内置存档 UI/自动存档策略（节奏由宿主调 TakeSnapshot）。
- 不做性能优化特殊路径（本 Profile 与联机共用同一核心热路径）。

## 10. 验收证据

- 两样例（MinimalAction/TurnBased）× LocalProfile：Headless 测试全绿 + snapshot/replay + 状态哈希一致（顶层 4.6 阶段验收）。
- 能力缺失启动拒绝测试；无存档/有存档两种模式往返测试。
- `dotnet run --project GameFramework.ModuleDemo` 扩展输出 LocalProfile 全轨迹。

## 11. 审核决策点

- **Q1**：D1 Profile 输出模块清单、与单机联机共用 Plan/Validate 路径——同意？
- **Q2**：D2 `CompositionRequirements` 能力完整性启动校验——同意？
- **Q3**：D4 内存 loopback 也强制走 ICommandBus 正式入口（禁止测试直调处理器捷径）——同意？这条纪律测试代码会略显繁琐。
- **Q4**：D5 种子未设时运行时生成并随流头回读——同意？
