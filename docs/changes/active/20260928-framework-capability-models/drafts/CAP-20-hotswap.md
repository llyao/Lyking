# CAP-20 热更边界 · 设计草稿

> 状态：**待审核** | 能力域：E 运维能力 | 目标工程：`Share/GameFramework.PluginHost` + `GameFramework.Profiles` | 前置：CAP-00、CAP-18
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

哪些东西可以运行时替换、哪些必须版本化发布；没有边界的热更会把状态迁移和回滚变成隐式行为——半套注册、半套资源、状态悬空，全是生产事故形态。

**MMO 驱动力**：
- 运营活动/玩法 Feature 的不停服上线与下线（MMO 的日常）。
- 内容包热切换（CAP-18 current projection）已是既定路径，代码级热更需要同一套安全点纪律。
- 客户端热更（Unity 侧 HybridCLR 等）与服务器侧替换是两种机制——顶层 3.6 已判定它们不能混谈，本能力聚焦**服务器侧/自宿主侧**的运行时替换边界；客户端程序集热更由平台 adapter 承接，共享同一份"可热更清单"理念。

**通用性**：单机游戏基本只有版本化发布；本能力的合同按"默认全部版本化、白名单可热更"设计——关闭热更 = 空白名单，退化自然发生。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| 可热更清单 | descriptor 声明 `HotSwappable=true` 的模块（Feature/Tool 类） |
| 版本化发布清单 | Core/Contracts/Snapshot schema/Simulation 基础模块——**禁止**热更 |
| 安全点（swap point） | 全量 Drain → 逆拓扑停止 → 替换 → 按拓扑重启 的窗口 |
| `IHotSwapPlan` | 一次热更的声明：替换哪些模块版本、预期状态迁移 |
| 状态迁移 | 被替换模块的 namespace 状态在版本间的迁移（复用 CAP-03 `IStateMigrator`） |

## 3. 合同形状

```csharp
/// <summary>descriptor 扩展字段（ModuleDescriptor 追加，CAP-00 合同演进走追加兼容）。</summary>
public sealed record HotSwapPolicy
{
    public bool HotSwappable { get; init; }              // 默认 false
    public bool RequiresStateMigration { get; init; }    // 替换时是否有状态要迁移
    public string? MigrationContract { get; init; }      // 迁移器 TypeId（CAP-03）
}

/// <summary>热更计划：宿主校验通过才执行。</summary>
public interface IHotSwapPlanner
{
    /// <summary>声明一次替换：目标模块 + 新版本实现工厂 + 迁移器。</summary>
    HotSwapPlan Plan(IReadOnlyList<ModuleReplacement> replacements);
}

public sealed record ModuleReplacement(
    ModuleId Target,
    IGameModule NewImplementation,
    IStateMigrator? Migration);                          // RequiresStateMigration=true 时必填

public sealed record HotSwapPlan(
    IReadOnlyList<ModuleReplacement> Replacements,
    HotSwapValidationResult Validation);

public interface IHotSwapCoordinator
{
    /// <summary>执行计划。全程失败原子：任何一步失败回滚到旧集合。</summary>
    ValueTask<HotSwapResult> ExecuteAsync(HotSwapPlan plan, CancellationToken ct);
}

public sealed record HotSwapResult(
    bool Success,
    string? Error,
    IReadOnlyList<ModuleId> Swapped,
    /// <summary>回滚发生时列出失败步骤（诊断）。</summary>
    IReadOnlyList<string>? RollbackSteps);
```

**安全点流程（固化，与 CAP-00 停机序列同构）**：

```text
ExecuteAsync(plan)
  → 校验：目标模块全部 HotSwappable=true；依赖闭包内无版本化模块被牵连；
          迁移器覆盖所有 RequiresStateMigration 目标；新集合 Plan/Validate 全过（复用 CAP-00）
  → 安全点进入：全实例 Drain（拒新收旧，CAP-07 语义）
  → 逆拓扑序 Stop 目标模块（依赖正序 Drain 已完成，逆拓扑释放）
  → 状态迁移：旧 namespace 状态 → 迁移器 → 新版本 Capture 格式
  → 新版本物化 + Start（依赖正序）→ Ready
  → 任何一步失败：按逆序回滚已执行步骤，恢复旧版本集合，报告 RollbackSteps
```

## 4. 关键设计决策（含推荐）

**D1｜白名单制，默认全禁（推荐，顶层 3.7/3.8 规则 1 既定）**：Core、Contracts、Snapshot schema、Simulation 基础模块（时钟/随机/管线/快照）永久版本化发布——它们被其他一切引用，热更它们 = 热更世界本身。Feature/Tool 模块可声明白名单。

**D2｜热更期间整个实例暂停（推荐简单版）**：安全点 = 实例级 Drain。备选"只 Drain 目标模块的 owner 子集"（部分流量不停）作为后续优化——第一版全实例暂停，正确性优先，暂停窗口通常是毫秒级（Feature 模块没有长队列）。若某 Feature 有长尾队列，那是它自己 Drain 慢，如实暴露。

**D3｜状态迁移强制声明（推荐）**：`RequiresStateMigration=true` 而 `Migration=null` → 计划校验直接失败。迁移器复用 CAP-03 `IStateMigrator`（同主版本内 = 字段级适配，跨主版本 = 显式链）——热更和存档迁移是同一套演进纪律，不造第二套。

**D4｜失败原子回滚（推荐）**：替换过程中任何失败 → 逆序回滚 → 旧集合完整恢复（旧实现对象在回滚完成前不释放）。"半套注册"在流程上不可能出现（对齐 CAP-18 发布门哲学）。

**D5｜与内容热切的关系（推荐分层）**：内容包切换（CAP-18）不需要 Drain（数据只读、无状态绑定，运行对象经租约刷新）；代码模块热更需要安全点。两者都在 CAP-20 的"可替换清单"治理下，但机制不同、安全点不同——文档与测试都要防止混用。

**D6｜客户端程序集热更（HybridCLR 等）不进本合同（推荐）**：那是平台加载机制，边界责任 = 保证"可热更清单 + 版本协商"理念一致（客户端热更版本不满足服务端要求即拒绝连接，复用 CAP-16 握手）。机制实现留 Client/Unity 阶段（顶层待确认项）。

## 5. 不变量

1. 未声明 `HotSwappable` 的模块，热更计划校验直接拒绝（默认禁，无豁免通道）。
2. 安全点内不存在第三种状态：要么旧集合完整运行，要么新集合完整运行。
3. 热更不改变快照兼容承诺：替换前后的快照在各自版本语义内可恢复（迁移器保证跨版本）。
4. 回滚后系统必须与热更前逐位一致（哈希不变）——回滚是"什么都没发生"，不是"发生了一半"。
5. 热更次数与耗时进 CAP-19 指标（`health.probe` 与 `hotswap.*`），`RestartCount` 相应更新。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 目标含不可热更模块 | 计划校验拒绝，列出违规目标 |
| 迁移器缺失/迁移失败 | 计划中止于状态迁移步；回滚旧版本；旧状态完好 |
| 新版本 Validate 失败（依赖/能力冲突） | 计划校验阶段拒绝（未进入安全点） |
| Start 新版本失败 | 回滚：Stop 新版本（逆拓扑）→ Start 旧版本 → 恢复运行 |
| Drain 超时 | 中止热更（安全点未建立，不替换），报告积压 owner |

## 7. 使用方案

- **U1**：不适用（版本化发布为主）；热更流程本身有 headless 测试。
- **U2 单机**：版本化发布；白名单为空。
- **U3 联机权威**：服务端 Feature 热更走安全点；客户端热更版本经握手校验（D6）。
- **U4 在线多 World**：按 World 滚动热更（逐 World 安全点，全局部分可用）；与 CAP-18 内容灰度协同编排。

## 8. 与其他能力的关系

- 依赖：CAP-00（生命周期与 Validate）、CAP-03（状态迁移）、CAP-07（Drain 语义）、CAP-09（回滚一致性哈希判据）、CAP-18（内容替换分层）、CAP-19（指标）。
- 承接：`20260928-module-manager-tidy` 非目标移交的"逆拓扑 Drain 安全点"。

## 9. 非目标

- 不做任意 DLL 装卸器/插件市场（加载机制属宿主部署；本合同管纪律）。
- 不做热更中的增量状态搬运（整 namespace 迁移起步）。
- 不定义客户端 HybridCLR 集成（Client 阶段立项）。

## 10. 验收证据

- 安全点流程测试：替换成功路径（哈希延续）、迁移路径、回滚路径（回滚后哈希与热更前一致）。
- 白名单违规拒绝测试；迁移器缺失拒绝测试。
- 长队列 Drain 超时中止测试。
- 指标接线测试（hotswap.* 计数）。

## 11. 审核决策点

- **Q1**：D2 热更 = 实例级全量暂停（不做部分 owner 子集热更）——同意？
- **Q2**：D3 热更状态迁移复用 CAP-03 迁移器（不造第二套）——同意？
- **Q3**：D4 失败原子回滚 + 回滚后哈希必须与热更前一致——同意？
- **Q4**：D6 客户端程序集热更（HybridCLR）不进本合同、只共享清单理念——确认？
