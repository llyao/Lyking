# CAP-24 场景与表现对象 · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目标工程：`GameFramework.Adapters.Client.*` | 前置：CAP-14、CAP-17、CAP-21
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

模拟世界（EntityId + 组件状态）如何变成可见世界（场景 + 实体表现对象）。核心矛盾：模拟有权威节奏，渲染有自己的帧率；实体成百上千（MMO 同屏），表现对象必须池化复用；实体出现/消失必须严格由投影驱动，不能表现层自己轮询模拟。

**MMO 驱动力**：
- 同屏实体规模：MMO 城镇/战场同屏数百实体，逐个 Instantiate 是性能灾难——池化与分帧同步是生存条件。
- World 切换：跨地图/跨 World = 场景级卸载重建（CAP-14 World scope），必须原子、无泄漏。
- 预测表现：本地预测实体（CAP-08 负值 ID 段）与权威实体的绑定切换。

**通用性**：2D/3D/回合制差异全在实现；合同只定"投影 → 表现对象"的驱动关系与池化纪律。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `ISceneLoader` | 场景加载：场景也是资源（AssetKey），挂 scope |
| `IEntityViewRegistry` | EntityId → 表现对象的登记表 |
| `IViewPool` | 按 AssetKey 池化表现对象 |
| `ProjectionSyncLoop` | 同步循环：比较投影版本，增删改表现对象（CAP-21 帧泵内分帧执行） |
| 表现对象 view | 绑定 EntityId 的平台对象（Unity 的 GameObject + 组件集） |

## 3. 合同形状

```csharp
public interface ISceneLoader
{
    /// <summary>加载场景到 scope；返回句柄，卸载 = 释放句柄（scope 级联兜底）。</summary>
    ValueTask<SceneHandle> LoadAsync(AssetKey sceneKey, AssetScope scope, CancellationToken ct);
}

public sealed class SceneHandle : IDisposable
{
    public AssetKey Key { get; }
    public void Dispose();       // 归还；场景对象随 CAP-14 语义回收
}

/// <summary>表现对象池：按资源键复用。对象状态由绑定器在取出时重置（防残留）。</summary>
public interface IViewPool
{
    ValueTask<IViewLease> RentAsync(AssetKey prefabKey, CancellationToken ct);
    /// <summary>预热带（场景加载后按预期实体量预热）。</summary>
    ValueTask WarmUpAsync(AssetKey prefabKey, int count, CancellationToken ct);
}

public interface IViewLease : IDisposable
{
    object ViewObject { get; }   // 平台对象（合同不解释；绑定器强转具体类型）
    AssetKey Key { get; }
}

/// <summary>同步循环：投影驱动的表现对象增删改（分帧预算内执行）。</summary>
public interface IProjectionSyncLoop
{
    /// <summary>每表现帧调用。预算：本帧最多处理的实体变更数（分帧防卡）。</summary>
    void Sync(IProjection projection, int changeBudget);
    int PendingChanges { get; }  // 积压监控（CAP-19 指标源）
}

/// <summary>实体绑定器：玩法注册——如何按投影 read model 配置/驱动一个表现对象。</summary>
public interface IEntityViewBinder
{
    AssetKey PrefabKey(EntityTypeId type);
    void OnSpawn(object view, EntityReadModel model);   // 取出后初始化
    void OnDespawn(object view);                        // 归还前重置（清残留状态）
    void OnUpdate(object view, EntityReadModel model, FrameContext frame);  // 每表现帧
}
```

**驱动关系（合同核心）**：

```text
IProjection.Version 变化（CAP-17）
  → IProjectionSyncLoop.Sync（帧泵调用，changeBudget 分帧）
      → 新实体：IViewPool.Rent（按 IEntityViewBinder.PrefabKey）→ OnSpawn → Registry 登记
      → 消失实体：OnDespawn → Registry 注销 → 租约归还池
      → 存续实体：OnUpdate（位置插值、动画状态）
  → 场景切换：CloseAll UI（CAP-22）→ SceneHandle.Dispose → World scope 释放（CAP-14 级联）
```

## 4. 关键设计决策（含推荐）

**D1｜投影驱动、绝不轮询模拟（推荐，与 CAP-17 D1 同源）**：表现对象的存在性完全由投影实体集合决定。模拟不知道表现存在（依赖方向单向）；表现不猜实体状态。

**D2｜插值在表现层、状态在投影（推荐）**：移动平滑 = 表现帧内在两个权威位置间插值（目标位置来自投影）。模拟 tick 率 ≠ 渲染帧率的解耦就靠这条——表现层永远不把插值中间值写回任何状态。

**D3｜池化键 = 预制体 AssetKey（推荐）**：池按资源键分桶；`OnDespawn` 强制重置（绑定器责任）——池化 bug 的来源几乎都是残留状态，重置义务显式化并测试把守。

**D4｜场景切换 = scope 原子边界（推荐）**：新场景加载到新 scope → 就绪后一次性切换当前场景 → 旧 scope 释放（CAP-14 级联回收全部实体/资源）。不做"边删边建"的渐进切换（复杂且必然闪帧）；加载遮罩由 CAP-22 Global 通道承担。

**D5｜分帧预算显式化（推荐）**：`changeBudget` 把"一帧同步多少实体变更"做成参数——大量实体瞬移（传送、Boss 拉怪）时表现分帧消化，`PendingChanges` 积压可观测。宁可延迟一两帧显示，不可单帧卡死。

**D6｜预测实体占同一注册表（推荐）**：预测实体（负值 ID）与权威实体同表登记；权威确认后"负值 → 真值"换绑（对象复用，不闪现）。换绑失败（服务器不一致）走 CAP-08 分歧回调修正。

## 5. 不变量

1. 表现对象存在性 ⇔ 投影实体存在性（同一帧内一致；分帧只影响到达延迟）。
2. 池对象归还前必须经 `OnDespawn` 重置；重置遗漏在测试中暴露（两次租借状态断言）。
3. 场景与实体资源全部挂 scope；无"裸 Instantiate"路径（审计 + 门禁）。
4. 表现层不写任何模拟状态；插值/表现数值不出表现层。
5. `EntityId` 是绑定唯一键；一个 EntityId 同时最多一个 view。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 预制体加载失败 | 实体显示占位（框架默认占位件）+ 诊断；不阻塞其他实体 |
| 绑定器 OnSpawn/OnUpdate 抛异常 | 该实体降级为占位 + 错误上报；同步循环继续（单实体故障不拖垮整屏） |
| 换绑失败（预测 vs 权威） | 按 CAP-08 分歧处理：销毁预测 view、按权威重建 |
| 场景加载失败 | 加载遮罩呈现错误与重试；旧场景保持（不进入黑屏无路可退态） |
| 积压持续增长 | CAP-19 指标告警（预算过小或实体量超设计） |

## 7. 使用方案

- **U1**：假 view（记录型对象）驱动 SyncLoop 全逻辑测试：增删改、分帧、重置义务、占位降级。
- **U2 单机**：场景流式加载 + 池化全套。
- **U3 联机权威**：服务器投影驱动；预测实体换绑路径启用。
- **U4**：World 切换 = D4 原子边界；RuntimeSlice 资源经 World scope（CAP-18/14）。

## 8. 与其他能力的关系

- 依赖：CAP-14（池与场景资源）、CAP-17（投影与版本）、CAP-21（帧泵与预算）、CAP-08（预测换绑）。
- 下游：CAP-26（3D 音源挂 view）、CAP-22（加载遮罩/场景 UI）、CAP-19（PendingChanges 指标）。

## 9. 非目标

- 不做 LOD/遮挡剔除策略与寻路表现（渲染优化属平台实现）。
- 不做动画状态机合同（绑定器内部）。
- 不做粒子/特效编排系统（特效资源按 AssetKey 走池，语义同实体）。

## 10. 验收证据

- 假 view 全逻辑测试：增删改/分帧/占位降级/重置义务断言。
- 池化压力测试：万次租借归还零泄漏、状态无残留。
- 场景原子切换测试：切换途中旧 scope 完整、新 scope 就绪才切换。
- 帧预算基准：changeBudget 下单帧耗时上限可复现。

## 11. 审核决策点

- **Q1**：D1 投影驱动（表现对象存在性由投影唯一决定）——同意？
- **Q2**：D4 场景切换 = 新 scope 就绪后原子切换（不做渐进边删边建）——同意？
- **Q3**：D5 分帧预算作为合同参数 + 积压可观测——同意？
- **Q4**：D6 预测实体与权威实体同表、确认时换绑——同意？
