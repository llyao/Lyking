# CAP-09 快照与状态哈希 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Simulation` | 前置：CAP-06
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

保存、恢复、断点续传、跨宿主一致性校验都依赖两件事：**状态的完整基线**（快照）与**可比较的摘要**（哈希）。没有哈希，"两个服务器跑出的世界一样吗"只能靠人眼；没有快照，存档/checkpoint/回放起点全都无从谈起。

**MMO 驱动力**：
- 服务器故障恢复：崩溃后从最近 checkpoint 重建 World，玩家损失限制在几分钟。
- 多 World 部署：同一份内容 + 同一命令序列必须在任何宿主上产出同一世界（反作弊、审计、确定性测试的基础）。
- 存档 fence：快照与持久化（CAP-15）配合，保证"保存了什么就能恢复什么"。

**通用性**：单机存档 = 快照；回合制结算 = 快照；格斗回滚 = 快照 + 高频小快照。合同一套，粒度由使用者定。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ISimulationSnapshot` | 状态基线：元数据 + 分块数据 + 随机流状态 + 时钟位置 |
| `IStateHasher` | 全状态摘要计算器；跨宿主可比较 |
| `StateHash` | 摘要值 + 算法标识（算法本身也要版本化） |
| 分块 chunk | 每 `StateNamespace` 一块；快照可按块传输/存储/校验 |
| `SnapshotFormat` | 快照容器格式（manifest + chunks）版本 |

## 3. 合同形状

```csharp
public interface ISimulationSnapshot
{
    SnapshotMetadata Metadata { get; }          // CAP-02 定义
    /// <summary>按命名空间分块；块内为该 namespace contributor 的 Capture 输出。</summary>
    IReadOnlyList<SnapshotChunk> Chunks { get; }
    /// <summary>恢复入口：目标实例的所有 contributor 依次 Restore。</summary>
    ValueTask RestoreIntoAsync(ISimulationState target, CancellationToken ct);
}

public sealed record SnapshotChunk(
    StateNamespace Namespace,
    int SchemaVersion,                 // 块数据版本（CAP-03）
    ReadOnlyMemory<byte> Data,
    StateHash ChunkHash);              // 块级校验，传输与存储层免全量比对

/// <summary>哈希器：稳定、跨宿主、增量友好。</summary>
public interface IStateHasher
{
    /// <summary>算法标识随哈希值一起输出（快照/日志自描述）。</summary>
    StateHash Compute(IEnumerable<IStateContributor> contributors);
}

public readonly record struct StateHash(string Algorithm, Memory<byte> Value)
{
    public override string ToString() => $"{Algorithm}:{Convert.ToHexString(Value)}";
}
```

**快照容器格式（manifest + chunks）**：

```text
SnapshotFile
  ├─ header：格式版本、SnapshotMetadata、chunk 清单（namespace、版本、长度、块哈希）
  ├─ chunk × N：命名空间数据（CAP-03 编码）
  └─ tail：整文件哈希
```

**哈希算法（锁定实现）**：逐 contributor 按 `Namespace` 字典序遍历 → 块内按 key 字典序遍历 → xxHash3-128 流式叠加（含 key 与长度作域分隔）。**排除项**：不哈希任何浮点（见 D3）、不哈希分配相关元数据（时间戳、来源备注）。

## 4. 关键设计决策（含推荐）

**D1｜哈希内容 = Capture 输出（推荐）**：哈希直接建立在 CAP-06 的 `Capture` 字节流上，不引入第二套状态遍历——快照完备性与哈希完备性天然同源，"哈希一致"就蕴含"快照可互恢复"。

**D2｜key 排序消除顺序漂移（推荐）**：`Capture` 写出顺序理论上稳定（同一模块同一逻辑），但排序遍历让哈希对实现顺序重排免疫，代价是哈希时排序（缓存 per-namespace 的排序 key 清单，热路径无排序）。快照文件本身仍按 Capture 原序存（写快），只有哈希走排序视图。

**D3｜浮点零参与（推荐，硬规则）**：浮点跨宿主不可复现（FMA/指令集/编译器差异），进哈希等于哈希无效。CAP-03 D5 已要求模拟状态定点/整数化；本能力把"浮点字段出现在哈希输入"定义为构建期错误（analyzer 检查 Capture 中的 float/double 写入）。

**D4｜增量哈希与块级校验（推荐）**：块哈希让"哪个模块状态漂了"一查便知（逐块比对）；全量哈希 = 块哈希的有序叠加，不必重读数据。为 MMO checkpoint 传输（只传变化的块）留出路径，但第一版不做增量快照（delta）本身。

**D5｜快照原子性（推荐）**：写快照 = 先写临时文件 + fsync + 原子改名（持久化介质上，配合 CAP-15）；内存中构造完整 `ISimulationSnapshot` 对象再落盘。恢复（CAP-06）失败可整体放弃。半套快照在文件系统层不可能出现。

**D6｜哈希算法版本化（推荐）**：`StateHash.Algorithm` 字符串标识算法与参数（`xxh3-128/v1`）；算法升级 = 新标识，跨算法哈希比较直接判不一致并报诊断（不猜）。

## 5. 不变量

1. 相同初始状态 + 时钟位置 + 种子 + 命令序列 ⇒ 相同 `StateHash`（跨宿主、跨平台，浮点零参与）。
2. 快照自足：恢复不依赖外部上下文；元数据足以判断版本兼容（CAP-03 校验）。
3. 块哈希与全量哈希关系恒成立：全量 = 有序叠加(块哈希)。
4. `RestoreIntoAsync` 成功后实例哈希 = 快照记录的哈希。
5. 快照必须含时钟位置（CAP-04）与全部随机流状态（CAP-05）——漏项即快照无效（校验拒绝）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 块哈希不匹配 | 恢复前拒绝该块；诊断到 namespace 粒度 |
| 元数据版本不兼容 | 按 CAP-03 拒绝或要求迁移；无迁移链则失败 |
| 落盘失败（磁盘满等） | 临时文件清理，旧快照不受影响（原子性保证）；向调用方报错 |
| 恢复后哈希不一致 | 环境判定为分歧：测试 fail；运行时上报 CAP-19 并可触发 CAP-10 定位 |
| 哈希算法不识别 | 比较直接判不一致 + 诊断（旧存档升级需迁移哈希或重算） |

## 7. 使用方案

- **U1 Headless 测试**：任意 tick 快照 → 恢复 → 继续推进 N tick → 与不间断路径哈希一致；分块校验故意破坏一块验证定位。
- **U2 单机**：存档即快照文件；自动存档按 tick 间隔或玩家触发。
- **U3 联机权威**：checkpoint 定期落盘（持久化 adapter）；重连从 checkpoint + 事件补播恢复。
- **U4 在线多 World**：每 World 独立快照；块级传输支撑 World 迁移（把玩家 namespace 块搬到目标 World）。

## 8. 与其他能力的关系

- 依赖：CAP-03（块数据编解码）、CAP-04/05（时钟与随机流入快照）、CAP-06（Capture 数据源）。
- 下游：CAP-10（回放起点与分歧检测）、CAP-15（持久化搬运快照）、CAP-12（checkpoint/预测对账状态）、CAP-18（ContentRevision 进元数据）。

## 9. 非目标

- 不做增量快照/delta 编码（块级粒度已覆盖多数需求；delta 等有真实带宽痛点再立项）。
- 不做快照压缩（存介质层自由压缩，合同不管）。
- 不做快照加密与防篡改签名（CAP-15 存档层按需加签）。

## 10. 验收证据

- 快照-恢复-续跑哈希一致测试（核心证据，所有样例必跑）。
- 跨宿主哈希一致测试（两种目标框架跑同一命令序列）。
- 块哈希定位测试：篡改单块 → 恢复拒绝并指认 namespace。
- 浮点入侵测试：Capture 写浮点 → analyzer/测试失败。
- 原子性测试：落盘中断（模拟 IO 故障）后旧快照完好。

## 11. 审核决策点

- **Q1**：D1/D2 哈希建立在 Capture 字节流 + 排序视图上——同意？
- **Q2**：D3 浮点零参与哈希作为构建期硬错误——同意？
- **Q3**：D4 第一版只做块级粒度、不做 delta 快照——同意？
- **Q4**：D6 哈希算法标识版本化、跨算法判不一致——同意？
- **Q5**：锁定 xxHash3-128 作为第一版算法——同意，或有其他偏好（Blake3 更强但实现重）？
