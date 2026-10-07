# CAP-15 持久化 · 设计草稿

> 状态：**待审核** | 能力域：D 平台适配 | 目标工程：合同进 `GameFramework.Contracts`，实现进 `GameFramework.Adapters.Persistence.*` | 前置：CAP-09、CAP-03
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

状态如何在进程重启、设备切换、多人存档间存活。关键风险是出现**第二权威**：MongoDB 文档模型和 Character Save 各自保存一份"真相"，互相对不上（顶层 3.6 硬风险）；或者存档写入与模拟推进并发进行，落下一半状态。

**MMO 驱动力**：
- Character Save：按玩家切出状态持久化，玩家在 World 间携带（配合 CAP-12/13）。
- 存档 barrier：停服/换 World 前的保存收敛点——"这个时刻之后的状态一定已落盘"。
- fence：防止旧进程（脑裂、僵尸实例）用旧数据覆盖新存档。

**通用性**：单机 = 文件存档 + fence 同样适用（防双开互踩）；回合制结算落库、竞技短局无持久化，合同按需装配。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IPersistenceAdapter` | 持久化门面（capability 名 `persistence`）；实现：文件 / SQLite / 云存储 / 文档库 |
| `PersistenceRecord` | 持久化单元：快照块 + fence + 元数据 |
| `Fence` | 单调令牌：每次实例生命周期递增；旧 fence 拒绝写入 |
| 存档 barrier | Drain 协程中的保存收敛点：所有 owner 排空后、Stop 前执行 |
| 切片 | 按 namespace 前缀从快照切出的子集（Character Save 的材料） |

## 3. 合同形状

```csharp
public interface IPersistenceAdapter
{
    /// <summary>保存一个持久化单元（快照块或切片）。原子：成功前旧值完好。</summary>
    ValueTask SaveAsync(PersistenceRecord record, CancellationToken ct);

    /// <summary>读取最新 fence 的单元；无档返回 null。</summary>
    ValueTask<PersistenceRecord?> LoadAsync(PersistenceKey key, CancellationToken ct);

    /// <summary>列出某逻辑键下全部历史版本（诊断与回档用；实现可限制深度）。</summary>
    ValueTask<IReadOnlyList<PersistenceKey>> ListVersionsAsync(string logicalKey, CancellationToken ct);

    /// <summary>可持久化的逻辑键清单（恢复前的完整性预检）。</summary>
    ValueTask<IReadOnlyList<string>> EnumerateKeysAsync(string prefix, CancellationToken ct);
}

public readonly record struct PersistenceKey(string LogicalKey, long Fence);

public sealed record PersistenceRecord(
    PersistenceKey Key,
    string Namespace,                       // 来源命名空间（对应 CAP-09 块）
    int SchemaVersion,
    ReadOnlyMemory<byte> Data,              // CAP-03 编码的块/切片数据
    StateHash Hash,                         // 块级校验
    long SavedLogicalTime,                  // 保存时的逻辑时间（诊断）
    string SavedByInstance);                // 保存者 InstanceId（诊断 + fence 判定依据）

/// <summary>fence 发放者：实例启动时领取本生命周期 fence。</summary>
public interface IFenceIssuer
{
    /// <summary>领取当前 fence（= 存储中该键最大 fence + 1；首档为 1）。</summary>
    ValueTask<FenceLease> AcquireAsync(string logicalKey, CancellationToken ct);
}

public sealed class FenceLease(long Value) : IDisposable
{
    public long Value { get; } = Value;
    public void Dispose();                  // 正常停机归还（adapter 标记实例已关闭）
}
```

**barrier 语义（编排约定，宿主/Profile 执行）**：

```text
Drain 开始 → 拒绝新输入 → owner 队列排空
  → barrier：TakeSnapshot → 按 namespace 切片/整块 → SaveAsync × N（fence = 本生命周期值）
  → 全部成功 → 进入 Stop
  → 任一失败 → 显式降级上报（内存真相仍在，实例继续或按策略停机）
```

## 4. 关键设计决策（含推荐）

**D1｜持久化只是快照的搬运工（推荐，顶层既定）**：adapter 不解释数据、不建立自己的对象模型——存的就是 CAP-09 的块/切片字节。"文档库里的玩家对象"由存储 adapter 自行序列化组织（如把切片映射进文档字段），但**权威判定永远以模拟状态为准**，加载即恢复、无字段级合并。

**D2｜fence 防脑裂（推荐）**：`SaveAsync` 携带 fence，小于当前存储 fence 的写入被拒绝。场景：旧进程没死透、双开客户端、迁移期间新旧实例并存——旧实例写档被 fence 挡住。实现上 fence 存在存储侧元数据里（与数据同事务）。

**D3｜切片按 namespace 前缀（推荐）**：Character Save 的实现 = 快照块中 namespace 前缀匹配（如 `player.{subjectId}.`）的块集合。核心提供 `SliceSnapshot(snapshot, prefix)` 工具；不做更细的字段级切片（块是最小单元，CAP-09 已定）。

**D4｜文件/SQLite/云三种参考实现（推荐范围）**：文件 adapter（U2 单机 + 测试）、SQLite adapter（U3 checkpoint + 中小型 MMO 存档）、文档库 adapter（对接现有 MongoDB 类设施，**字段映射在 adapter 内**）。云对象存储（S3 式）按部署需要再加。第一版必须交付前两种，第三种按项目需求。

**D5｜恢复优先级：fence 最高者（推荐）**：`LoadAsync` 默认取最大 fence；`ListVersionsAsync` 支持显式回档（运营操作，带二次确认的运维工具，不是运行时路径）。

**D6｜保存节流与合批（推荐 adapter 责任）**：高频保存（每 tick 存档荒谬）由调用方节奏控制（checkpoint 策略、barrier 时机），adapter 不做智能合并——合同保持"每次 Save 语义独立"。只有文档库实现内部可做写缓冲（进程内聚合 N ms 再落库，崩溃丢失窗口如实声明）。

## 5. 不变量

1. 持久化不制造第二权威：加载 = 恢复（CAP-06 Restore），无合并、无补丁、无"以库为准"。
2. fence 单调；旧 fence 写入拒绝并报诊断（不是异常路径，是明确的拒绝结果——SaveAsync 返回结果对象而非抛异常，见 Q3）。
3. Save 原子性：成功前旧值完好；实现可用临时文件/事务/条件写实现，合同不管手段。
4. barrier 之后的模拟推进 = 新的未保存窗口，窗口大小是显式策略值。
5. 存档可移植：同一记录在任何实现间可迁移（格式 = CAP-03 编码 + 通用头，实现不加密不加私货；加密属存储配置）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| Save 失败（磁盘满/断连） | 返回失败 + 诊断；内存真相不变；barrier 上报降级（CAP-19） |
| fence 过期写入 | 拒绝（`FenceOutdated` 结果，附当前 fence 值）——这是保护不是错误 |
| Load 时块哈希不匹配 | 拒绝加载该块；可尝试 ListVersions 回退上一 fence |
| 恢复后哈希不一致 | CAP-09 分歧路径（测试 fail / 运行时上报） |
| fence 领取失败（存储不可达） | 实例拒绝启动（fail-closed：无 fence 的写入权是危险状态） |

## 7. 使用方案

- **U1**：无持久化（LocalProfile `SnapshotDirectory=null`）。
- **U2 单机**：文件 adapter + fence（双开保护）；存档 = 整快照按块存。
- **U3 联机权威**：SQLite/文档库 adapter + checkpoint（CAP-12 策略）+ fence（多进程部署防脑裂）。
- **U4 在线多 World**：Character Save = namespace 切片 + fence；World 迁移 = 目标实例领 fence 后加载切片。

## 8. 与其他能力的关系

- 依赖：CAP-09（快照/切片材料）、CAP-03（编码与迁移）、CAP-01（InstanceId 标识保存者）。
- 下游：CAP-12（checkpoint/断线恢复）、CAP-13（Character Save）、CAP-19（保存失败降级上报）。

## 9. 非目标

- 不做 ORM、不做字段级增量持久化（块是原子）。
- 不做跨玩家事务（交易类跨主体事务属 Features/服务编排层，若需要以"中央服务 + 命令"实现，不走模拟存档）。
- 不做备份策略与冷热分层（部署运维）。

## 10. 验收证据

- 文件 adapter：存-载往返、fence 拒绝旧写、损坏块拒绝 + 版本回退。
- barrier 时序测试：排空→保存→Stop 顺序断言；保存失败时实例状态观察。
- 双开测试：第二实例领 fence 后，第一实例写档被拒。
- Character Save 切片测试：按前缀切片→恢复→哈希与全量路径一致。

## 11. 审核决策点

- **Q1**：D1 持久化零解释、加载即恢复（拒绝 ORM 化的对象合并）——同意？这是与 GameFrameX 类文档模型最大的分歧点。
- **Q2**：D2 fence 写入存储侧元数据（与数据同事务）——同意？
- **Q3**：Save 失败/fence 过期返回**结果对象**而非抛异常（拒绝是常态路径）——同意？
- **Q4**：D4 第一版交付文件 + SQLite 两种实现，文档库按项目需求——同意？
