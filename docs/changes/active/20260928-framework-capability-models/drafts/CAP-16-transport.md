# CAP-16 网络传输 · 设计草稿

> 状态：**待审核** | 能力域：D 平台适配 | 目标工程：合同进 `GameFramework.Contracts`，实现进 `GameFramework.Adapters.Transport.*` | 前置：CAP-02
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

消息如何在进程间可靠有序流动，而不把某个传输库（WebSocket、TCP 封装、KCP、QUIC）焊进核心。核心风险：handler 直接改模拟（已由 CAP-07/08 纪律挡住）与传输细节（心跳、重连、分帧）泄漏进业务。

**MMO 驱动力**：
- 长连接会话：断线重连、事件游标补播（CAP-12）依赖传输层提供"连接生命周期 + 有序字节流"两个原语。
- 混合流量：可靠通道（交易、聊天）与不可靠通道（高频位置）并存是 MMO 常态；合同要留出通道区分而不提前实现。
- 运维：连接级指标（RTT、重连率）是 CAP-19 的数据源。

**通用性**：U1 用内存 loopback（测试全链路）；局域网联机与在线联机只换 transport 实现，Profile 不变。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ITransportFactory` | 传输实现入口（capability 名 `transport`） |
| `ITransportConnection` | 单条连接：发送、接收、关闭与状态 |
| `TransportFrame` | 帧：传输头（会话/路由字段）+ 载荷（CAP-03 编码的 envelope） |
| `TransportHeader` | 会话身份字段唯一合法出现处（CAP-02 D 边界） |
| 握手 | 连接建立时的版本协商与认证材料传递 |
| loopback | 内存实现：无真实 IO，全部语义照常（含断连模拟） |

## 3. 合同形状

```csharp
public interface ITransportFactory
{
    /// <summary>服务端：监听。返回连接接受序列。</summary>
    ValueTask<ITransportListener> ListenAsync(TransportEndpoint endpoint, CancellationToken ct);
    /// <summary>客户端：连接。</summary>
    ValueTask<ITransportConnection> ConnectAsync(TransportEndpoint endpoint, CancellationToken ct);
}

public readonly record struct TransportEndpoint(string Scheme, string Host, int Port);
// Scheme 例：loopback / ws / tcp / kcp —— 具体支持面由实现声明

public interface ITransportListener : IAsyncDisposable
{
    IAsyncEnumerable<ITransportConnection> AcceptAsync(CancellationToken ct);
}

public interface ITransportConnection : IAsyncDisposable
{
    ConnectionState State { get; }
    /// <summary>发送一帧。载荷已由调用方编码（CAP-03）；帧头由调用方填充。</summary>
    ValueTask SendAsync(TransportFrame frame, CancellationToken ct);
    /// <summary>接收一帧。连接关闭时序列结束。</summary>
    IAsyncEnumerable<TransportFrame> ReceiveAsync(CancellationToken ct);
    /// <summary>主动关闭（带原因；对端可读到）。</summary>
    ValueTask CloseAsync(string reason, CancellationToken ct);
    ConnectionMetrics Metrics { get; }          // RTT、收发字节、重连计数（CAP-19 数据源）
}

public sealed record TransportFrame(
    TransportHeader Header,
    ReadOnlyMemory<byte> Payload);

public sealed record TransportHeader
{
    /// <summary>会话路由标识。服务端据此找 SessionHandle（CAP-12）；重连时携带旧值。</summary>
    public required string SessionKey { get; init; }
    /// <summary>帧类型：Handshake / Command / EventPush / Control / Ping。</summary>
    public required TransportFrameKind Kind { get; init; }
    /// <summary>握手与版本协商材料（认证 token、协议版本表）。</summary>
    public IReadOnlyDictionary<string, string>? Handshake { get; init; }
}

public enum TransportFrameKind { Handshake, Command, EventPush, Control, Ping }
```

**可靠性语义**：核心合同要求 `Command` 与 `EventPush` 帧**可靠有序**（单连接内 FIFO，不丢不重——由具体实现保证：TCP/WebSocket 天然如此，UDP 类实现须自行补齐或声明为不可靠通道并在 `Kind` 上区分）。控制帧（Ping/Close）不承诺顺序。

## 4. 关键设计决策（含推荐）

**D1｜帧不是消息合同（推荐）**：`TransportFrame.Payload` 是不透明字节（CAP-03 envelope 的编码结果）；传输层不解码、不校验业务字段。传输只对帧头负责——会话身份只出现在这里，与 CAP-02 的分层严格对应。

**D2｜握手承载认证与版本协商（推荐）**：`Kind=Handshake` 的首帧双向交换：客户端给凭证与协议版本表，服务端回选定版本与拒绝原因。认证逻辑委托 `ISessionAuthenticator`（CAP-12）——传输层不做鉴权决策，只搬运材料。握手未完成前，Command 帧一律拒绝。

**D3｜重连是传输头字段不是新 API（推荐）**：重连 = 新连接的 Handshake 帧携带旧 `SessionKey`；服务端匹配悬置会话（CAP-12）。合同不定义"Reconnect()"方法——重连在协议层就是"新连接 + 旧会话头"，各实现一致。

**D4｜loopback 是第一公民实现（推荐）**：内存实现与真实传输通过同一合同；`CloseAsync`/断连/乱序注入皆可模拟——U1 的断线测试全靠它。它还是"传输中立"的编译期证明：样例不引任何网络库。

**D5｜背压与发送队列（推荐显式）**：`SendAsync` 在连接发送缓冲满时返回失败（`SendBufferFull`）而不无限排队——应用层决定丢弃/降频/踢出。MMO 大厅广播风暴的第一道闸。

**D6｜不可靠通道留枚举位（推荐暂不实现）**：`TransportFrameKind` 预留扩展（如 `UnreliableCommand`）；第一版只有可靠通道。高频位置同步在"可靠有序"下先用（现代带宽下 MMO 回合/慢节奏动作可行）；真需求出现再立 unreliable 通道，避免第一版就做双通道状态机。

## 5. 不变量

1. 传输层不解释 Payload；业务字段只存在于 envelope（CAP-02/03 分层）。
2. 会话身份只出现在 `TransportHeader`；envelope 内不含网络身份（顶层 4.4 既定）。
3. Command/EventPush 帧单连接内可靠有序；实现违约即实现 bug（合同测试可检出）。
4. 握手完成前，非 Handshake 帧一律拒绝并断开。
5. 连接关闭后 `SendAsync` 失败（`ConnectionClosed`）；不抛出意外异常类型。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 连接中断 | `ReceiveAsync` 序列结束 + `State=Closed`；上层（网关）进入会话悬置流程 |
| 发送缓冲满 | `SendAsync` 返回 `SendBufferFull` 失败；连接不自动关闭 |
| 握手版本不兼容 | 握手响应带双方版本诊断；连接关闭（CAP-03 混服协商语义） |
| 帧解码失败（坏数据） | 断开连接（不静默丢帧——有序流一旦有洞，后续全是错的） |
| 服务端过载 | 先限速（CAP-12 网关），仍超限则踢出；传输层自身不做智能降载 |

## 7. 使用方案

- **U1 Headless 测试**：loopback 全链路（含断连、重连、缓冲满注入）；样例无任何网络依赖。
- **U2 单机**：不适用（无传输；loopback 仅测试用）。
- **U3 联机权威**：WebSocket（或项目选定传输）实现；`ITransportFactory` 由 AuthoritativeProfile 配置注入。
- **U4 在线多 World**：gateway 前置（路由到 World host adapter）；本合同仍是 host 与 gateway 之间的约定（顶层 4.6"必须接入 Hydra gateway/host adapter"落点）。

## 8. 与其他能力的关系

- 依赖：CAP-02（信封分层）、CAP-03（版本协商）。
- 下游：CAP-12（会话网关、限速、重连）、CAP-19（连接指标）、CAP-13（gateway 编排）。

## 9. 非目标

- 不做加密与 TLS 配置（实现层职责；合同要求实现支持传输加密即可，不进核心）。
- 不做协议压缩、粘包优化的通用实现（各实现内部）。
- 不做服务发现与负载均衡（部署层；gateway 属 CAP-13）。

## 10. 验收证据

- loopback 契约测试：有序性、关闭语义、缓冲满、握手拒绝、重连头。
- WebSocket 实现通过同一契约测试套件（传输中立的可执行证明）。
- 帧头字段审计测试：envelope 编码结果不含会话字段（分层回归）。

## 11. 审核决策点

- **Q1**：D1/D2 传输只管帧头与握手、认证委托给 SessionAuthenticator——同意？
- **Q2**：D3 重连 = 新连接 + 旧 SessionKey 头（不做专用 Reconnect API）——同意？
- **Q3**：D5 发送缓冲满返回失败而非自动排队——同意？
- **Q4**：D6 第一版只做可靠通道，不可靠通道留枚举位——同意？
- **Q5**：U3 首选 WebSocket 作为第一个真实传输实现（跨平台、Unity 支持好、调试容易）——同意，还是首版就要 UDP/KCP？
