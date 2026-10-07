# CAP-12 AuthoritativeProfile · 设计草稿

> 状态：**待审核** | 能力域：C 运行配置 | 目标工程：`Share/GameFramework.Profiles` | 前置：CAP-08、CAP-16
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

客户端输入 + 服务端权威的**通用组合**：不绑定具体传输（WebSocket/私有 UDP）、不绑定具体存储（文件/数据库）、不绑定产品。它定义"一个可信服务端 + 一批不可信客户端"拓扑下，框架各能力的标准装配方式与边界合同。

**MMO 驱动力**：
- 这是 MMO 服务端的直接基座：会话生命周期、checkpoint、断线重连、补播、限速鉴权在本层成型，HydraOnlineProfile（CAP-13）在其上再叠三界。
- 客户端预测与权威对账的默认策略在此落地，保证手感与公平可兼得。

**通用性**：竞技游戏、生存联机、房间制游戏全部落在这一层；只有真正多 World/多进程编排才需要 CAP-13。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `AuthoritativeProfile` | 服务端侧组合器：权威模拟 + 会话管理 + transport + 可选 checkpoint |
| `ISessionGateway` | 会话生命周期合同：接入、认证后绑定 Subject、断连、重连、踢出 |
| `ISessionEnvelope` | 会话边界头：`WorldSessionKey` 等价物（本层即 `SessionKey`）、连接身份、fence |
| `ReconciliationPolicy` | 预测对账策略参数（对账窗口、回退深度上限） |
| `CheckpointPolicy` | 快照周期、保留数量、触发条件 |

## 3. 合同形状

```csharp
public sealed class AuthoritativeProfile : IProfile
{
    public AuthoritativeProfile(AuthoritativeOptions options);
}

public sealed record AuthoritativeOptions
{
    public required ITransportFactory Transport { get; init; }        // CAP-16
    public IPersistenceAdapter? Persistence { get; init; }            // CAP-15，可选 checkpoint
    public CheckpointPolicy? Checkpoint { get; init; }
    public ReconciliationPolicy Prediction { get; init; } = new();
    public required ISessionAuthenticator Authenticator { get; init; }// 会话认证（token → SubjectId）
    public RateLimitPolicy RateLimit { get; init; } = new();          // 命令限速（提交边界前）
}

/// <summary>会话网关：网络世界与模拟世界之间的门。</summary>
public interface ISessionGateway
{
    /// <summary>连接到达 → 认证 → 绑定 SubjectId → 注入模拟（重建或新建会话状态）。</summary>
    ValueTask<SessionHandle> AcceptAsync(ITransportConnection connection, CancellationToken ct);
    ValueTask KickAsync(SessionHandle session, string reason);
    event Action<SessionHandle, SessionEndedReason>? OnSessionEnded;
}

public interface ISessionAuthenticator
{
    /// <summary>把传输层凭证解析为稳定 SubjectId；失败抛 AuthenticationException。</summary>
    ValueTask<SubjectId> AuthenticateAsync(IReadOnlyDictionary<string, string> credentials, CancellationToken ct);
}

public sealed record RateLimitPolicy(int MaxCommandsPerSecondPerSubject = 60, int MaxInFlightPerSubject = 32);

public sealed record CheckpointPolicy(TimeSpan Interval, int KeepCount = 3, bool SnapshotOnDrain = true);

public sealed record ReconciliationPolicy(int MaxRollbackTicks = 8, TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(2));
```

**会话数据流**：

```text
客户端连接 → transport 接入 → Authenticator（token→SubjectId）
  → SessionGateway 建立 SessionHandle（会话级事件通道绑定 EventSequence 游标）
  → 客户端命令：transport → 解码（CAP-03）→ 会话头附加 → 限速 → ICommandBus.Submit
     → AuthorityModel(ServerAuthoritative) 两段校验 → owner 队列 → tick 执行
  → 事件：channel → SessionHandle 按 EventSequence 游标增量推送
断线：SessionHandle 悬置（保留游标与限速状态）→ 重连带游标 → 从 checkpoint + 事件补播 → 恢复
```

## 4. 关键设计决策（含推荐）

**D1｜会话是边界对象，不是模拟对象（推荐）**：`SessionHandle`（连接、游标、限速计数）活在网关侧，**不进 `SimulationState`**——模拟只认 SubjectId。断线重连不触碰模拟状态；玩家"在线状态"对玩法可见的部分必须通过命令/事件表达（如 `PlayerLeftCommand`），不能让模拟偷看连接。

**D2｜重连恢复 = checkpoint + 补播（推荐）**：重连时从客户端声明的事件游标起，服务端重发缺失事件；游标过旧（超出事件缓冲）则从 checkpoint 重同步全量状态（块级，CAP-09）。两种路径都是只读重放，模拟不受扰动。

**D3｜限速在网关不在权威模型（推荐分工）**：连接级速率（洪水防御）属网关（纯工程问题）；玩法级合法性（"移动速度超物理上限"）属 AuthorityModel 执行校验（业务问题）。两处都拒绝但拒绝码不同，排障不混淆。

**D4｜预测对账默认"仅状态确认"（推荐起步）**：第一版预测策略 = 客户端本地预测 + 权威事件确认，分歧时按权威状态**整段重同步**（不做逐帧回滚重演）。MaxRollbackTicks 只是"超过 N tick 未确认即强制重同步"的阈值。逐帧 rollback 留给后续格斗/RTS 需求——MMO 中整段重同步已够用且实现量小一个量级。

**D5｜checkpoint 策略默认开（推荐）**：给 Persistence 时默认每 60s checkpoint + Drain 时必存；`KeepCount` 保留多份支持回退。无 Persistence 的 AuthoritativeProfile 合法（纯内存权威，如竞技短局），但启动时警告"无 checkpoint 的长会话有丢失风险"。

**D6｜会话迁移粒度 = Subject（推荐）**：本层不做 World 间玩家迁移（那是 CAP-13）；会话绑定单实例。跨实例需求（换服、跨区）在网关层表现为"旧会话正常结束 + 新会话正常建立"，状态搬运走 CAP-15 的存档。

## 5. 不变量

1. 服务端是唯一权威：客户端只能提交命令与消费事件；客户端状态是投影 + 预测。
2. 会话身份（SessionKey、连接）不进入 `SimulationState`；进模拟的只有 SubjectId 化的命令。
3. 每个会话的事件投递按 `EventSequence` 有序且不重复（网关保证；重连补播同样有序）。
4. 断线不结束模拟对 Subject 的认知（短期悬置）；踢出/超时才产生 `PlayerLeft` 类事实命令——超时时长是 Profile 参数。
5. checkpoint 之间的内存状态丢失窗口是显式配置值（`Interval`），不是隐藏行为。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 认证失败 | 连接关闭 + 原因；不进入模拟 |
| 限速触发 | 命令拒绝（`RejectedValidation: rate-limited`），计数上报 CAP-19；持续触发可踢 |
| 游标过旧 | 触发 checkpoint 全量重同步路径；同步期间会话只收不 发新事件（缓冲到完成） |
| checkpoint 写失败 | 保留上一份（CAP-09 原子性）+ 健康降级上报；不阻塞模拟 |
| transport 断连 | 会话悬置计时；模拟继续（其他玩家不受影响） |
| 服务器崩溃 | 重启后从 checkpoint 恢复全部会话状态；未 checkpoint 的输入丢失窗口如实告知运营 |

## 7. 使用方案

- **U1**：不适用（LocalProfile 载体）；但其服务端内核开发期在 LocalProfile 下调试。
- **U2**：不适用（LocalProfile 载体）。
- **U3 联机权威**：本 Profile 的载体。两样例先用 LocalProfile 跑稳，再接本 Profile 验证同输入哈希一致（顶层 4.6 阶段验收）。
- **U4**：不适用（HydraOnlineProfile 载体）；本层输出的会话/补播/checkpoint 语义是 CAP-13 的构件。

## 8. 与其他能力的关系

- 组合：CAP-07/08(ServerAuthoritative)/09/10 + CAP-15(可选) + CAP-16(必须) + CAP-17(可选客户端表现)。
- 上游语义来源：CAP-01（Subject/预测 ID）、CAP-02（会话头边界）、CAP-05（权威端随机）。
- 被 CAP-13 复用：三界中的 ServerLive 即本 Profile 的扩展形态。

## 9. 非目标

- 不做大厅/匹配/房间系统（Features 层）。
- 不做多进程 World 编排与玩家跨服迁移（CAP-13）。
- 不定义账号服务器/token 格式（`ISessionAuthenticator` 是接口，实现属产品）。
- 不做逐帧 rollback 预测（D4，整段重同步起步）。

## 10. 验收证据

- 同输入序列：LocalProfile 与 AuthoritativeProfile（loopback transport）终态哈希一致。
- 断线重连：游标补播路径与 checkpoint 重同步路径各自的恢复正确性测试。
- 限速/认证失败/游标过旧三组拒绝路径测试。
- checkpoint 周期 + Drain 强制快照测试；崩溃恢复（杀进程后从 checkpoint 恢复）测试。

## 11. 审核决策点

- **Q1**：D1 会话是边界对象不进模拟状态——同意？这决定"在线状态"必须以命令/事件表达。
- **Q2**：D4 预测起步用"整段重同步"、不做逐帧 rollback——同意？（MMO 够用、量小；格斗/RTS 后续立项）
- **Q3**：D3 限速分两层（网关速率 / 权威合法性）——同意分工？
- **Q4**：D5 无 Persistence 的 AuthoritativeProfile 合法但警告——接受，还是强制要求配持久化？
