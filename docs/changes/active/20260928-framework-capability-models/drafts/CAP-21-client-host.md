# CAP-21 客户端宿主（ClientHost） · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目标工程：`GameFramework.Adapters.Client.*`（Unity 实现进 `Client/`） | 前置：CAP-00、CAP-04、CAP-11
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

框架核心不知道 Unity（G1 既定），但 Unity 是客户端的物理世界：`MonoBehaviour` 生命周期（Awake/Start/Update/OnApplicationPause/OnDestroy）、渲染帧、前后台切换都真实存在。没有宿主桥，Unity 代码会渗进核心（无法 Headless 测试）或核心无法被 Unity 驱动。

**MMO 驱动力**：
- 前后台切换：手机切后台再回来，模拟与连接如何处理（挂起？追帧？重连？）必须有一致的宿主策略。
- 帧率与 tick 解耦：120Hz 屏幕、30fps 降级、服务器 tick 独立——驱动节奏必须集中管理。
- 多端（PC/手机/云游戏）共用同一套核心，宿主桥是每端唯一需要重写的件。

**通用性**：Godot/Web/编辑器宿主实现同一合同；单机与联机客户端差异只体现在 PauseBehavior 参数。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IClientHost` | 平台宿主桥：把平台生命周期事件翻译为框架动作 |
| `HostFramePump` | 帧驱动：表现帧回调 → clock driver（CAP-04 D2 宿主职责的客户端形态） |
| `PauseBehavior` | 前后台策略：单机暂停模拟 / 联机保持连接只停表现 |
| `ClientHostOptions` | 目标帧率、tick 节奏、追帧上限 |

## 3. 合同形状

```csharp
public interface IClientHost : IAsyncDisposable
{
    /// <summary>启动宿主并组装 GameInstance（内部走 CAP-11 流程）。</summary>
    ValueTask<GameInstance> StartAsync(IProfile profile, GameInstanceOptions options, CancellationToken ct);

    GameInstance? Instance { get; }
    IProjectionAccess Projection { get; }        // 转 CAP-17 表现会话的门（组合到 PresentationSession）

    /// <summary>平台调用：前台/后台切换。策略按 ClientHostOptions.PauseBehavior。</summary>
    ValueTask OnApplicationPauseAsync(bool paused);
    /// <summary>平台调用：退出前清理（映射 Drain→Stop，带超时兜底强停）。</summary>
    ValueTask OnApplicationQuitAsync();
}

public sealed record ClientHostOptions
{
    public PauseBehavior Pause { get; init; } = PauseBehavior.SuspendSimulation;
    public int MaxCatchUpTicksPerFrame { get; init; } = 5;   // 后台回来追帧上限，超限跳到当前
    public bool AllowBackgroundTick { get; init; }           // 移动端后台是否继续（联机常开）
}

public enum PauseBehavior
{
    SuspendSimulation,      // 单机：后台停 Advance，回前台追帧（上限内）
    KeepSimulationRunning,  // 联机：模拟（收服务器事件）继续，仅停表现渲染
}
```

**Unity 侧形态（实现约定，不进合同）**：一个 `GameFrameworkComponent`（DontDestroyOnLoad）持 `IClientHost`；`Update` → `HostFramePump.Pump(renderDelta)`；`OnApplicationPause` → `OnApplicationPauseAsync`；桥内零业务逻辑。

## 4. 关键设计决策（含推荐）

**D1｜核心零平台类型，桥是唯一翻译层（推荐，G1 落地件）**：`IClientHost` 合同本身平台中立（纯 C# 事件方法），Unity/Godot 各自实现。禁止核心引用 `MonoBehaviour`；analyzer 门禁同 CAP-14。

**D2｜追帧上限与跳帧语义（推荐）**：后台回来一次性最多补 `MaxCatchUpTicksPerFrame` 步，超出部分直接跳到当前时间（Variable/Fixed 同理）——MMO 客户端追帧无意义（真相在服务器），单机跳帧优于卡死。追帧上限防"挂一晚上回来卡 10 分钟"。

**D3｜前后台双策略参数化（推荐）**：单机 `SuspendSimulation`（省电、防偷跑）；联机 `KeepSimulationRunning`（保持收事件与连接心跳，只停渲染）。联机后台被系统杀进程的兜底 = 重启走重连（CAP-12），宿主不做奇迹。

**D4｜单实例默认（推荐）**：一个客户端进程一个 `GameInstance`（多开是进程级事）。`StartAsync` 二次调用抛异常——显式拒绝优于隐藏单例。

## 5. 不变量

1. 平台类型只存在于宿主实现程序集；核心与 Profiles 引用不到。
2. 表现帧率与模拟 tick 解耦：`HostFramePump` 是两者唯一换算点。
3. 退出路径必经 Drain→Stop（与 CAP-00 停机序列一致），超时兜底强停并上报。
4. 宿主不产生业务行为：它只翻译生命周期，不做游戏逻辑。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 组装失败（Validate 拒绝） | 宿主把诊断呈给用户界面（可读错误页），不留半启动实例 |
| 追帧溢出 | 跳帧 + 记指标（跳帧率是性能信号，进 CAP-19） |
| 退出超时 | 强停 + 崩溃标记（下次启动提示异常退出） |
| 后台被系统终止 | 无回调；恢复走重连/存档路径 |

## 7. 使用方案

- **U1**：不适用（Headless 无客户端宿主）；宿主逻辑本身有纯 C# 单测（假帧泵）。
- **U2 单机**：SuspendSimulation + 追帧。
- **U3 联机权威**：KeepSimulationRunning + 重连兜底。
- **U4**：同 U3；ShowWorld 挂在 Projection 门上（CAP-13/17）。

## 8. 与其他能力的关系

- 组合：CAP-11 Profile + CAP-04 帧驱动 + CAP-17 表现会话。
- 下游：CAP-22/23/24/26（UI/输入/场景/音频挂在宿主上）；CAP-19（跳帧/退出指标）。

## 9. 非目标

- 不做崩溃采集 SDK 与上报协议（平台 adapter 接第三方，如 Bugly/Sentry）。
- 不做云游戏串流、多窗口（平台形态另立）。

## 10. 验收证据

- 假帧泵单测：追帧上限、跳帧、双策略切换。
- Unity 冒烟：进入/切后台/回前台/退出全轨迹日志（样例工程）。
- 依赖门禁：核心程序集扫描无平台类型。

## 11. 审核决策点

- **Q1**：D2 追帧上限 + 超限跳帧（而非无限补帧）——同意？
- **Q2**：D3 双策略参数化（单机暂停 / 联机只停表现）——同意？
- **Q3**：D4 严格单 GameInstance、二次启动显式拒绝——同意？
