# CAP-01 身份模型 · 设计草稿

> 状态：**待审核** | 能力域：A 契约与身份 | 目标工程：`Share/GameFramework.Contracts` | 前置：CAP-00
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

跨系统对象引用需要稳定身份。MMO 里的实体会被存档、跨 World 迁移、断线重连、被热更代码重新加载——只要身份是"内存指针"或"可复用的数组下标"，这些场景必然出现串话（A 玩家的操作落到 B 玩家身上）与幽灵引用。

**MMO 驱动力**：
- 长生命周期：身份要能存档、能跨进程恢复（服务器重启后 EntityId 还能指回同一实体）。
- 代际安全：实体销毁后 ID 可能被复用，异步回调拿到旧 ID 必须能识别"这是上一代的实体"。
- 多 World：不同 World 的实体可能同号，身份要能表达"哪个 World 的哪个实体"。
- 权威边界：客户端不能自造实体身份，否则服务端无法判定输入对象。

**通用性**：单机游戏只需要"本地递增 + 存档携带"；框架合同必须同时容纳两种强度，强度由 Profile 选择，不由核心强制。

## 2. 术语与核心对象

| 对象 | 含义 | 归属 |
|---|---|---|
| `EntityId` | 模拟世界内实体身份 | 核心合同 |
| `InstanceId` | 一次 GameInstance 运行的身份 | 核心合同 |
| `CommandId` | 单条命令的身份 | 核心合同 |
| `EventSequence` | 事件流内的单调序号 | 核心合同 |
| `ContentRevision` | 内容包版本（与 CAP-18 对应） | 核心合同 |
| `Generation` | 身份代际，实体复活/重建时递增 | 核心合同 |
| `WorldRef` | （WorldId, EntityId）复合引用，跨 World 寻址 | 核心合同（只有 ID，无 World 对象） |
| `SubjectId` | 命令来源主体（玩家/系统/AI） | 核心合同（不解释为账号体系） |
| `CharacterId`、`NetId`、`WorldSessionKey` | 在线身份 | CAP-12/13 边界合同，**不进核心** |

## 3. 合同形状

```csharp
/// <summary>所有域 ID 的公共形态：强类型包装，编译期不可互换。</summary>
public interface IDomainId<TSelf> where TSelf : struct, IDomainId<TSelf>
{
    static abstract TSelf None { get; }
    bool IsValid { get; }          // None / 非法值返回 false
    string ToDisplayString();      // 调试与日志，格式稳定，不用于解析
}

public readonly record struct EntityId(long Value) : IDomainId<EntityId>
{
    public static EntityId None => default;
    public bool IsValid => Value > 0;   // 0 = None，负值非法
}

public readonly record struct InstanceId(long Value) : IDomainId<InstanceId>;
public readonly record struct CommandId(long Value) : IDomainId<CommandId>;
public readonly record struct EventSequence(long Value) : IDomainId<EventSequence>;
public readonly record struct ContentRevision(long Value) : IDomainId<ContentRevision>;

/// <summary>代际：0 表示从未分配；实体每次重建 +1。与 EntityId 组合成唯一key。</summary>
public readonly record struct Generation(int Value) : IDomainId<Generation>;

/// <summary>跨 World 引用。WorldId 本身也是 IDomainId，由宿主/Profile 分配。</summary>
public readonly record struct WorldRef(WorldId World, EntityId Entity, Generation Gen);

/// <summary>ID 分配器。U1 可注入确定性实现；U3/U4 权威端持有。</summary>
public interface IIdGenerator<TId> where TId : struct, IDomainId<TId>
{
    TId Next();
}

/// <summary>代际登记：实体表维护 (EntityId → Generation)，销毁时保留代际记录。</summary>
public interface IGenerationTracker
{
    Generation Current(EntityId entity);
    /// <summary>校验失败返回 false（迟到/过期输入），不抛异常。</summary>
    bool Validate(EntityId entity, Generation expected);
}
```

## 4. 关键设计决策（含推荐）

**D1｜ID 载体：类型化 long 包装（推荐）vs 裸 long vs 位段编码**
- 裸 long：零成本但可互换，串话 bug 编译期发现不了。
- 位段编码（高 16 位 WorldId + 低 48 位序号）：跨 World 路由不用查表，但上限锁死、调试不可读、World 数扩展受限。
- 推荐：类型化 `readonly record struct`，JIT 下零分配零拆箱；跨 World 用显式 `WorldRef` 复合，把"是否跨 World"变成类型问题。

**D2｜EntityId 复用策略：不复用（推荐）+ 代际双保险 vs 复用**
- 不复用：64 位单调序号在单 World 生命周期内不会耗尽；存档与日志天然无歧义。
- 复用 + 代际：省 ID 空间但要求所有消费方校验代际。
- 推荐：默认不复用，`Generation` 仍保留——它同时服务于"实体重建"（同一逻辑位置的实体重新生成，如副本重置）与热更后实例对照。

**D3｜分配器归属：权威端独占（推荐）**
- U2 单机：本地分配器即权威。U3/U4：只有权威端持有 `IIdGenerator<EntityId>`，客户端收到的是投影，引用即可。
- 客户端预测产生的临时实体使用独立的 `PredictedEntityId` 空间（负值段），权威确认后映射为真 ID——此机制在 CAP-08 草稿展开，核心合同只约定"负值段保留给预测"。

## 5. 不变量

1. ID 由所属域的权威分配器创建，其他模块只传递、只引用，不制造、不解析内部结构。
2. 各域 ID 类型互不隐式转换；`IDomainId` 不提供 `long` 隐式转换运算符。
3. 异步回调使用实体前必须 `IGenerationTracker.Validate`；失败按迟到输入拒绝（可观察、记日志，不抛异常）。
4. `None`（全零）对所有域合法且只表示"无"；序列化禁止出现负值。
5. `ToDisplayString` 格式全局稳定（如 `ent:00000042`），可用于日志检索，不用于反解。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 代际校验失败 | 返回 false，调用方拒绝处理；框架层记录计数器（可观测） |
| 分配器耗尽（64 位用尽） | 实践中不可能；发生即 RuntimeFault，宿主 fail-fast |
| 序列化遇到非法值（负数） | CAP-03 解码层拒绝并报诊断 |
| 客户端提交携带服务端未分配的 EntityId | CAP-08 权威模型在提交边界拒绝 |

## 7. 使用方案

- **U1 Headless 测试**：注入 `SequentialIdGenerator`（从 1 递增），断言可预测；代际测试覆盖"销毁→重建→旧回调被拒"。
- **U2 单机**：本地分配器；存档携带 `(EntityId, Generation)` 即完整恢复。
- **U3 联机权威**：服务端分配；客户端命令中的 EntityId 在提交边界校验存在性与归属（Subject 拥有权校验属 CAP-08）。
- **U4 在线多 World**：World 内 EntityId 唯一；跨 World 用 `WorldRef`；Meta 侧持久身份（CharacterId）由 CAP-13/15 边界映射到 `(WorldId, EntityId)`。

## 8. 与其他能力的关系

- 下游全部：CAP-02（元数据字段）、CAP-06（状态键）、CAP-07（命令/事件身份）、CAP-09/10（快照与回放基线）。
- 上游：CAP-00（`ModuleId` 已按同思路落地，`StateNamespace` 校验机制复用其 Validate 阶段）。

## 9. 非目标

- 不定义账号系统、uid 语义（那是 Profile/Features 层）。
- 不做 ID 反查服务（按 ID 找对象走 CAP-06 状态容器与 owner 索引）。
- 不做分布式 ID 生成器（snowflake 等）；多权威端分配属 CAP-13 World 边界，届时按需引入。

## 10. 验收证据

- 编译期：ID 互不赋值的类型测试（编译失败即通过的反向用例以脚本断言）。
- 运行期：代际拒绝、None 语义、序列化往返（配合 CAP-03）三组测试。
- Demo：ModuleDemo 扩展一段"实体销毁→迟到回调被拒"轨迹输出。

## 11. 审核决策点

- **Q1**：D1 载体——同意"类型化 long 包装 + 显式 WorldRef"，还是偏好位段编码（说明你的 World 数量预期）？
- **Q2**：D2——同意 EntityId 不复用 + 代际双保险？
- **Q3**：客户端预测 ID 用负值段保留——是否接受这一核心合同约定？
- **Q4**：`SubjectId` 保持在核心层"不解释为账号"——同意？
