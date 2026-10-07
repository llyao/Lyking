# CAP-29 会话路由网关（Gateway） · 设计草稿

> 状态：**待审核** | 能力域：G 服务器核心模块 | 目标工程：`Share/GameFramework.ServerHost.Gateway` | 前置：CAP-12、CAP-16、CAP-27
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

拓扑从"单进程单 World"长到"单进程多 World"乃至"多进程多 World"时：连接怎么找到该去的 World、World 挂了会话怎么走、广播怎么扇出、玩家怎么跨 World 搬家。CAP-12 的 `ISessionGateway` 是**单实例**会话门；本能力是**拓扑层**：把多个 World 实例编排在路由面之后，让"客户端只认一个入口"成立。

**MMO 驱动力**：
- 分线/分区分服：同玩法多 World 实例，玩家选线或自动分配。
- World 维护与故障：单 World drain（CAP-27）期间其会话要能被引导/迁移，其余玩家无感。
- 跨 World 广播（全服公告、聊天频道）与跨 World 交互（邮寄、交易行）的路由承载。

**通用性**：房间制联机（房间=World）、大世界分线（World=线）同构。**边界**：Hydra 三界（Meta/Live/Show）映射仍归 CAP-13 后置——本能力只做"多 World 路由"这一通用面，不引入三界词汇。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IGatewayRouter` | 路由面：会话/命令 → World 目标 |
| `WorldRegistry` | World 实例清单 + 健康 + 负载（同进程内为内存表；跨进程经注册 adapter） |
| `IWorldHandle` | 路由面对一个 World 的引用（进程内直引；跨进程为代理） |
| `BroadcastFanout` | 广播扇出：全服/频道 → 多 World 投递 |
| `SessionMigration` | 会话迁移：旧 World 排空保存 → 新 World 加载恢复（CAP-12/15 组合） |

## 3. 合同形状

```csharp
public interface IGatewayRouter : IAsyncDisposable
{
    /// <summary>分配 World：按策略选目标（选线/负载/指定）。返回 WorldHandle。</summary>
    ValueTask<IWorldHandle> AssignAsync(SessionPlacementRequest request, CancellationToken ct);

    /// <summary>帧转发：路由面不解释载荷（CAP-16 D1 延伸到拓扑层）。</summary>
    ValueTask RouteCommandAsync(SessionHandle session, TransportFrame frame, CancellationToken ct);

    WorldRegistry Registry { get; }
    event Action<SessionHandle, IWorldHandle>? OnSessionAssigned;
}

public sealed record SessionPlacementRequest(
    SubjectId Subject,
    string? PreferredWorld,          // 选线
    PlacementPolicy Policy);

public enum PlacementPolicy { Preferred, LeastLoaded, Sticky }   // Sticky：断线重连回原 World

public interface IWorldHandle
{
    WorldId World { get; }
    WorldRuntimeState State { get; }    // Starting/Ready/Draining/Stopped（映射 CAP-27）
    int SessionCount { get; }
    /// <summary>进程内：直接投递；跨进程：经 transport 代理（实现同一接口）。</summary>
    ValueTask DeliverAsync(TransportFrame frame, CancellationToken ct);
    ValueTask<SessionMigrationResult> MigrateSessionInAsync(SessionMigrationPayload payload, CancellationToken ct);
}

/// <summary>会话迁移：排空保存（旧 World）→ 加载恢复（新 World），材料走 CAP-15。</summary>
public sealed record SessionMigrationPayload(SubjectId Subject, string LogicalKey, long Fence);
public sealed record SessionMigrationResult(bool Success, string? Error);
```

**路由流程**：

```text
连接 → CAP-12 认证 → Router.AssignAsync（策略选 World）
  → WorldHandle.Deliver（进程内直调其 ISessionGateway.Accept / 帧转发）
运行中：命令帧按 SessionKey→World 绑定直投；绑定不漂移（粘性）
World drain：Router 停止分配新会话 → 存量会话等待自然结束或走 MigrateSessionOut
跨 World 广播：BroadcastFanout 逐 World 投递（World 内按 CAP-12 事件游标语义到达）
```

## 4. 关键设计决策（含推荐）

**D1｜路由面零状态零解释（推荐）**：网关不存游戏状态、不解码载荷——它只有"会话↔World 绑定表 + World 清单"。网关崩溃重启 = 重建绑定表（客户端重连即可），游戏无损。一切状态在 World 与存储里。

**D2｜粘性绑定 + 显式迁移（推荐）**：会话与 World 绑定后不漂移（除非显式迁移）；负载均衡只发生在**新会话分配**时。运行中动态搬会话（再均衡）必须走 `SessionMigration` 全流程（排空→保存→加载），禁止"悄悄换 World"——状态撕裂不可承受。

**D3｜迁移 = 存档材料搬运（推荐）**：`MigrateSession` = 旧 World barrier 保存该 Subject 的 namespace 切片（CAP-15 D3 Character Save 材料 + fence）→ 新 World 领新 fence 加载 → 客户端透明重绑（同一连接，事件游标续传）。跨进程迁移同流程，材料经存储中转（不做进程间直传状态——存储是仲裁者）。

**D4｜广播语义 = 至少一次 + 游标去重（推荐）**：跨 World 扇出可能重复（扇出中 World 重启重收），靠接收端 `EventSequence` 游标去重（CAP-12 已有）——网关不做精确一次（分布式成本不值），重复无害化。

**D5｜路由表来源分阶段（推荐）**：第一版 = 进程内 WorldRegistry + 静态配置（单进程多 World 即满足分线服）；跨进程 = 注册 adapter（配置中心/注册中心）预留接口不实现。避免第一版就背服务发现复杂度。

**D6｜World 故障的会话出口（推荐）**：World 进程/实例故障 → 其会话由 Router 置为"可重分配"；客户端重连走 Sticky 失败 → 重新 Assign（新 World）→ CAP-12 checkpoint 恢复。玩家体感 = 重连一次；不丢超过 checkpoint 窗口的进度（CAP-15 fence 保证无脏写）。

## 5. 不变量

1. 网关持有零游戏状态：绑定表可随时重建（D1）。
2. 会话同时只绑定一个 World；迁移是原子切换（旧解绑成功前新 World 不开始收帧）。
3. 迁移材料的 fence 纪律与 CAP-15 一致：旧 World 写档、新 World 领新 fence，无双写窗口。
4. 广播不保证精确一次；接收端去重（D4）。
5. 路由面不解释载荷；World 内部协议变化对网关透明。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 分配时无可用 World | 请求失败带原因（满载/维护中）；客户端重试策略自定 |
| World 在会话途中 drain | 新命令被拒（Draining）；存量按 CAP-27 流程收敛或迁移 |
| 迁移中途失败 | 材料仍在存储（fence 保护）；会话留在旧 World 或重试迁移；无中间态 |
| 网关自身重启 | 绑定表丢失 → 全部会话视为断连 → 客户端重连重建（短暂全量重连是接受的成本） |
| 广播部分失败 | 记录失败 World 清单重试；最终以游标对账 |

## 7. 使用方案

- **U1**：进程内多 World 全链路（分配/粘性/迁移/故障重分配），headless 可测。
- **U2**：不适用（单机无网关）。
- **U3**：单 World 时 Router 退化为直通（合同兼容，实现可为 CAP-12 网关本尊）。
- **U4**：分线/多 World 全能力；跨进程拓扑时本合同是 World host 节点与网关节点之间的约定（CAP-13 后续在此之上做三界映射）。

## 8. 与其他能力的关系

- 依赖：CAP-12（单实例会话门、恢复）、CAP-15（迁移材料 + fence）、CAP-16（帧与重连）、CAP-27（World 生命周期与 drain）。
- 下游：CAP-13（三界映射与跨服编排）、CAP-19（World 健康进 Registry）、CAP-30（路由决策日志）。

## 9. 非目标

- 不做匹配/排队系统（Features 层）。
- 不做跨服玩法事务（交易行等属 Features/服务编排）。
- 不做三界（Meta/Live/Show）映射——CAP-13。

## 10. 验收证据

- 分配策略测试（Preferred/LeastLoaded/Sticky）。
- 会话迁移端到端：排空→保存→加载→游标续传，中途故障注入无中间态。
- World 故障重分配测试：Sticky 失败→新 World→checkpoint 恢复。
- 广播去重测试：模拟扇出重复，接收端游标过滤。
- 网关重启恢复测试：绑定表重建、会话重连收敛。

## 11. 审核决策点

- **Q1**：D2 粘性绑定 + 运行中迁移必须走全流程（禁止动态悄悄搬会话）——同意？
- **Q2**：D3 迁移材料经存储中转（不做进程间直传）——同意？
- **Q3**：D4 广播至少一次 + 游标去重（放弃精确一次）——同意？
- **Q4**：D5 第一版只做进程内路由 + 静态配置，跨进程注册留接口——同意？
- **Q5**：网关重启 = 全量重连（接受短暂中断换取网关无状态简单性）——同意？
