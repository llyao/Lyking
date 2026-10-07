# CAP-25 客户端资源更新器 · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目标工程：`GameFramework.Adapters.Client.Update` | 前置：CAP-18、CAP-16、CAP-21
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

客户端启动时：检查版本 → 下载差异 → 校验 → 应用 → （可选）加载热更程序集 → 进入游戏。没有统一管线：下载器各写各的、校验漏做、更新到一半断网状态混乱、热更程序集与资源版本错配。

**MMO 驱动力**：
- 不发版修内容：MMO 高频内容更新全靠资源热更；程序集热更（逻辑修正）是第二通道。
- 强制版本一致性：服务器 revision（CAP-18）与客户端必须匹配——更新器是握手前的第一道门。
- 弱网体验：断点续传、差分下载、校验失败重试是 MMO 客户端基本盘。

**通用性**：单机游戏更新器退化为主体资源包一次下载；竞技/联机全流程。合同按全流程设计，单机裁剪使用。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IUpdatePipeline` | 四阶段管线：检查 → 下载 → 校验 → 应用 |
| `UpdatePolicy` | 强制/可选更新、Wi-Fi 限制、重试策略 |
| 差分下载 | 按 manifest 块哈希只下缺失/变化的块（复用 CAP-18 块结构） |
| 程序集热更挂点 | 热更程序集在资源应用后、GameInstance 启动前加载（CAP-20 D6 客户端挂点） |
| 更新任务状态机 | 幂等可恢复：每阶段完成即持久化进度 |

## 3. 合同形状

```csharp
public interface IUpdatePipeline
{
    /// <summary>全流程入口。可重入：中断后再次调用从持久化进度继续。</summary>
    ValueTask<UpdateResult> RunAsync(UpdatePolicy policy, IProgress<UpdateProgress>? progress, CancellationToken ct);
}

public enum UpdatePhase { Check, Download, Verify, Apply, Done }

public sealed record UpdateProgress(UpdatePhase Phase, long BytesDone, long BytesTotal, double SpeedBytesPerSec, string? CurrentItem);

public sealed record UpdateResult(
    bool Success,
    ContentRevision ClientRevision,      // 应用后的客户端内容版本（CAP-18）
    string? Error,
    UpdateOutcome Outcome);

public enum UpdateOutcome { UpToDate, Updated, UpdatedWithAssemblyReload, RequiresRestart, Cancelled, Failed, ServerVersionMismatch }

public sealed record UpdatePolicy
{
    public required string ManifestUrl { get; init; }        // 内容清单地址（CAP-18 发布产物）
    public bool OnlyOnWifi { get; init; }
    public bool ForceUpdateOnMismatch { get; init; } = true; // 与服务器握手 revision 不符即强制
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryBackoffBase { get; init; } = TimeSpan.FromSeconds(2);
    public string? HotUpdateAssemblyRoot { get; init; }      // 程序集热更根路径（null = 不启用）
}

/// <summary>程序集热更挂点（客户端；机制实现按平台，如 HybridCLR）。</summary>
public interface IHotAssemblyLoader
{
    /// <summary>在资源应用后、Profile 组装前调用。加载失败 = 客户端拒绝进入。</summary>
    ValueTask LoadAsync(string assemblyRoot, ContentRevision revision, CancellationToken ct);
}
```

**执行流程（固化）**：

```text
启动 → IUpdatePipeline.RunAsync
  → Check：拉服务端 manifest（CAP-18 产物），与本地清单对比 → 差异块集合
  → Download：差异块下载（断点续传：块级进度持久化）
  → Verify：全块哈希校验（manifest 为准，坏块重下）
  → Apply：原子应用（新清单写入 + 旧版本保留一档，对齐 CAP-18 回滚理念）
  → HotAssembly（可选）：IHotAssemblyLoader.LoadAsync
  → 完成 → CAP-21 宿主启动 → CAP-16 握手（revision 终验）
失败：按阶段恢复——下载失败续传；校验失败重下坏块；应用失败回滚旧版本
```

## 4. 关键设计决策（含推荐）

**D1｜清单即真相，无私有版本号（推荐）**：更新器只认 CAP-18 `ContentManifest`/`ContentRevision`——不自造"资源版本号"体系。服务器握手（CAP-16）与更新器（本能力）读同一个数，一致性由单一事实保证。

**D2｜块级断点续传（推荐）**：进度持久化到块粒度（完成一块记一块）；重试只补缺失块。备选"字节级续传"对大块更省但实现与校验复杂——块级已满足 MMO 内容粒度（单块通常 < 数 MB）。

**D3｜应用原子 + 保留一档旧版本（推荐）**：应用 = 切清单指针（对齐 CAP-18 current projection 语义）；旧版本保留一档用于启动失败回滚（新包把客户端更新到起不来的保险）。

**D4｜程序集热更在资源后、实例前（推荐顺序）**：程序集版本必须与资源 revision 匹配（清单携带程序集版本信息）；加载失败拒绝进入游戏（宁可让玩家重下，不可带错版本逻辑跑）。服务器侧无此问题（CAP-20 白名单走安全点）。

**D5｜差分策略在 adapter（推荐）**：合同只定义"按块对比清单"；是否做二进制差分（bsdiff 类）压缩传输是 adapter 优化。合同不绑差分算法。

## 5. 不变量

1. 客户端 revision 是贯穿更新器、握手、投影、快照的唯一版本事实（D1）。
2. 应用前校验全块哈希；坏块进不了应用阶段。
3. 更新流程可重入：任意阶段中断（断网/杀进程）后重跑，结果一致（幂等）。
4. 程序集热更版本与资源 revision 强绑定；错配拒绝启动。
5. 下载内容只进临时区；应用（切指针）前不影响当前可运行版本。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 下载中断/超时 | 按 MaxRetries 退避重试；重试耗尽 → Failed（保留进度，用户重试） |
| 块哈希不匹配 | 重下该块（最多 2 次）→ 仍坏 → Failed + 服务端告警线索（CDN 坏包） |
| 磁盘空间不足 | 检查阶段预检（需求空间 = 差异量 + 余量）；不足即提示，不下载到一半 |
| 应用后启动失败 | 下次启动检测到旧版本档 → 提示回滚（用户确认） |
| 服务器 revision 更新（下载中又发了新包） | 以启动时拉取的 manifest 为准完成本次；下次启动再追（不追求会话内二次更新） |

## 7. 使用方案

- **U1**：本地 HTTP 假服务器 + 临时目录跑全流程（检查/断点/坏块/回滚四组用例）。
- **U2 单机**：更新器可选（内置包即 UpToDate 直接过）。
- **U3 联机权威**：全流程 + 强制更新策略。
- **U4**：同 U3 + 程序集热更挂点启用。

## 8. 与其他能力的关系

- 依赖：CAP-18（manifest/revision 事实源）、CAP-14（应用后资源经租约读取）、CAP-16（握手终验）、CAP-21（宿主启动时序）。
- 下游：CAP-22（更新进度 UI 与 Global 遮罩）、CAP-20（服务端热更与客户端热更共享"可热更清单"理念）。

## 9. 非目标

- 不做 CDN 选型与调度（部署层；合同只消费 manifest URL）。
- 不做灰度发布编排（CAP-18 D5 服务端职责）。
- 不做通用下载器库（HTTP 细节在 adapter）。

## 10. 验收证据

- 本地假服务器端到端：UpToDate/更新/断点续传/坏块重下/应用失败回滚全路径。
- 幂等测试：每阶段随机杀进程后重跑，终态一致。
- 磁盘预检、Wi-Fi 策略、重试退避测试。
- 程序集错配拒绝启动测试。

## 11. 审核决策点

- **Q1**：D1 清单即真相、不自造资源版本号——同意？
- **Q2**：D2 块级（非字节级）断点续传——同意？
- **Q3**：D3 应用原子 + 本地保留一档旧版本（可回滚）——同意？
- **Q4**：D4 程序集热更失败即拒绝进入游戏（无降级）——同意？
