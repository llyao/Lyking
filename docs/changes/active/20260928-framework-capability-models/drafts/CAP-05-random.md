# CAP-05 可记录随机源 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Kernel` | 前置：CAP-01
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

掉落、暴击、随机刷怪必须可复现。业务直接调 `System.Random` 或语言内建随机：换机器结果不同（实现随运行时版本变化）、回放对不上、联机两端各自掷骰（结果不一致），bug 无法重现。

**MMO 驱动力**：
- 权威端掷骰：只有服务端能决定掉落，客户端表现层可以本地随机但结果不进模拟。
- 回放与审计：同一种子 + 同命令序列必须重建同一世界；申诉复查依赖此性质。
- 多系统隔离：战斗和掉落各自用独立随机流，避免"修了战斗随机数顺序，掉落全变"。

**通用性**：单机游戏一样需要"种子 → 可分享的世界"（随机地图种子已是行业惯例）；合同不区分单机联机。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IRandomSource` | 单个随机流：可取整数/浮点/字节 |
| `IRandomStreamRegistry` | 按流名分发独立随机流 |
| 实例种子 | GameInstance 创建时的根种子，随快照/存档保存 |
| 流名 | 稳定字符串（如 `"combat.crit"`、`"loot.drop"`），注册制 |
| 派生 | 流的初始状态 = Hash(实例种子, 流名, 流内代际) |

## 3. 合同形状

```csharp
public interface IRandomSource
{
    /// <summary>[minInclusive, maxExclusive)。范围非法抛参数异常。</summary>
    int NextInt(int minInclusive, int maxExclusive);
    /// <summary>[0, 1) 双精度。注意：浮点结果受 IEEE 二进制表示约束，见 D3。</summary>
    double NextDouble();
    /// <summary>填充缓冲。用于洗牌等场景。</summary>
    void NextBytes(Span<byte> buffer);
    /// <summary>流标识（诊断与回放记录用）。</summary>
    string StreamName { get; }
}

public interface IRandomStreamRegistry
{
    /// <summary>按稳定流名取流。同流名同实例返回同一实例；未注册流名按 D4 策略处理。</summary>
    IRandomSource GetStream(string streamName);
    /// <summary>模块在 Contribute 阶段声明所需流名（供校验与文档生成）。</summary>
    void DeclareStreams(ReadOnlySpan<string> streamNames);
}
```

**派生算法（核心内置、锁定实现）**：

```text
初始化：SplitMix64(seed ^ StreamNameHash(streamName)) 生成 256 位状态
生成器：xoshiro256**（+ 常数回跳检查）
StreamNameHash：FNV-1a 64（稳定，不依赖 .NET 字符串哈希）
整数映射：拒绝采样（无取模偏置）
```

## 4. 关键设计决策（含推荐）

**D1｜不用 `System.Random`（推荐）**：其内部算法随 .NET 版本变化（.NET 6 起实现更换），跨宿主哈希一致性会碎。核心锁定 xoshiro256** + SplitMix64 播种：算法公开、可移植（Unity/Mono/.NET 同结果）、统计质量足够游戏用途。备选 PCG 也合格，xoshiro 胜在实现更短。

**D2｜流隔离与注册（推荐）**：每消费域独立流，流间推进互不影响。流名在 Contribute 阶段声明，Validate 阶段查重（同流名两模块声明 → 启动失败，复用 CAP-00 诊断机制）——防"两个系统共用 loot 流"。

**D3｜浮点随机（推荐保留但降级）**：`NextDouble` 保留，但与 CAP-03 D5 呼应：进模拟状态的浮点有跨宿主确定性风险，推荐模块用 `NextInt` 自己映射（如万分率）；`NextDouble` 主要服务表现层。是否干脆删掉 `NextDouble`？——保留：删掉会逼所有人手写映射，反而滋生第三方随机调用。

**D4｜未声明流名的运行期访问（推荐 fail-fast in dev, degradable in prod）**：Debug/CI 构建抛异常（逼开发者去声明）；Release 构建 `GetStream` 返回一个"影子流"（由流名直接派生，不冲突已注册流）并记诊断。理由：线上不能因为遗漏声明崩服，但必须可观测。

**D5｜随机消费是否进回放流（推荐：默认不记，按需记录）**：两种流派——(a) 每次消费都写入回放流，回放时重放消费结果，完全不依赖算法一致；(b) 回放只记种子与流名，回放时重算。推荐 (b)：体积小，且算法已锁定；(a) 作为 CAP-10 的可选模式（`ReplayMode.RecordRandom`）供"算法升级后回放老录像"场景。合同上 CAP-10 决定，这里只保证"种子与流内状态可从快照恢复"。

## 5. 不变量

1. 业务模块禁止调用框架外随机（`System.Random`、`UnityEngine.Random`、时间种子技巧）；依赖方向由代码审查约定 + analyzer 门禁（S3 交付项）保证。
2. 同一 `(实例种子, 流名)` 的序列在任意宿主逐位一致。
3. 流内状态可从快照恢复（快照含流状态，配合 CAP-09 的模块状态贡献）。
4. 流名全局唯一；声明冲突在 Validate 失败。
5. `NextInt` 范围参数非法立即抛参数异常；`maxExclusive == minInclusive` 合法且恒返回 minInclusive（空区间语义）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 未声明流名 | Dev 抛异常；Release 返回影子流 + 诊断计数 |
| 流名重复声明 | Validate 阶段启动失败 |
| 种子缺失（快照无种子） | 恢复失败，带诊断（快照格式错误） |

## 7. 使用方案

- **U1 Headless 测试**：固定种子注册表；断言 `NextInt` 序列逐位等于金标准值（锁定算法防意外变更）。
- **U2 单机**：根种子 = 玩家输入或时间；随存档保存种子与各流状态。
- **U3 联机权威**：种子由权威端生成并通过快照/初始同步下发；客户端表现层随机不经此合同。
- **U4 在线多 World**：每 World 独立根种子（`Hash(全局种子, WorldId)`）；World 间流隔离天然成立。

## 8. 与其他能力的关系

- 依赖：CAP-00（声明与校验时机）。
- 下游：CAP-09（流状态进快照）、CAP-10（回放策略 D5）、CAP-06（模块通过状态贡献保存流状态）。

## 9. 非目标

- 不做加密安全随机（洗牌防作弊预测需要时，在 CAP-08 权威端用 CSPRNG 混入额外熵——另行评估，第一版不进合同）。
- 不做权重表/掉落表工具（属 Features 层）。

## 10. 验收证据

- 跨宿主一致测试：同种子在两种 .NET 目标框架下序列逐位一致。
- 金标准序列测试；流隔离测试（流 A 消费不影响流 B）。
- 快照恢复测试：快照→恢复→继续取值序列与不间断路径一致。

## 11. 审核决策点

- **Q1**：D1 锁定 xoshiro256** + SplitMix64——同意，或指定其他算法（PCG/MWCG）？
- **Q2**：D4 未声明流名"dev fail-fast + release 影子流"——接受双策略，还是要求全环境 fail-fast？
- **Q3**：D3 保留 `NextDouble`（推荐表现层用）——同意？
- **Q4**：D5 回放默认记种子重算、可选记消费结果——同意把这个模式选择交给 CAP-10？
