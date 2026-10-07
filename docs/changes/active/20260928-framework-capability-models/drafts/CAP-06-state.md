# CAP-06 状态容器与命名空间 · 设计草稿

> 状态：**待审核** | 能力域：B 运行内核 | 目标工程：`Share/GameFramework.Simulation` | 前置：CAP-01、CAP-03
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

模块各存各的状态（静态单例、散落的成员变量）会导致：快照漏字段（恢复后世界缺一块）、回放错位（状态变化没有对应输入）、归属不清（谁有权写这段状态没人知道）。

**MMO 驱动力**：
- Character Save：MMO 的持久化粒度是"玩家相关的那部分状态"，状态归属清晰才能切出这片。
- World 恢复：服务器崩溃后按 World 重建状态——快照必须完备且可分块。
- 热更边界（CAP-20）：替换模块前要确认它的状态已被排空/迁移——没有归属登记就做不到。

**通用性**：不同游戏的状态形态差异巨大（ECS 组件、对象图、棋盘格）；核心不能规定状态形态，只能规定**归属、快照出口、序列化纪律**。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `SimulationState` | 一次模拟的完整状态聚合；快照/哈希/恢复的根 |
| `StateNamespace` | 状态命名空间：全局唯一前缀，模块声明所有权（复用 CAP-00 descriptor 声明，Validate 查重） |
| `IStateContributor` | 模块的状态出口：捕获/恢复自己 namespace 的状态 |
| `IKeyValueState` | 核心提供的默认 KV 状态实现（模块可直接用，也可不用） |
| `StateHash` | 全状态摘要（CAP-09 计算） |

## 3. 合同形状

```csharp
/// <summary>模块实现此接口声明"我拥有哪些状态、如何进出快照"。</summary>
public interface IStateContributor
{
    /// <summary>本贡献者的命名空间（与 CAP-00 descriptor 中声明一致，Validate 校验）。</summary>
    StateNamespace Namespace { get; }

    /// <summary>捕获：把状态写入 writer。必须是纯函数式遍历，不读时钟/随机/外部 IO。</summary>
    void Capture(ISnapshotWriter writer);

    /// <summary>恢复：从 reader 读回状态。恢复后模块状态必须等价于捕获时。</summary>
    void Restore(ISnapshotReader reader);
}

public sealed record StateNamespace(string Value)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Value)
        && Value is ['A'..='Z' or 'a'..='z' or '.', ..];   // 字母开头，可含点分层
}

/// <summary>快照写入面：按 key 写类型化值；写入顺序不限（哈希前排序，见 CAP-09）。</summary>
public interface ISnapshotWriter
{
    void Write<T>(string key, in T value) where T : allows ref struct;
    ISnapshotWriter OpenSection(string name);   // 嵌套分段（对应模块内部子结构）
}

public interface ISnapshotReader
{
    T Read<T>(string key);
    bool TryRead<T>(string key, out T value);   // 读端宽容：旧档缺字段走默认值（同主版本）
    ISnapshotReader OpenSection(string name);
}

/// <summary>默认 KV 状态容器（可选使用）：模块间约定俗成的简单状态存放处。</summary>
public interface IKeyValueState
{
    T Get<T>(string key);
    void Set<T>(string key, T value);
    /// <summary>注册为 IStateContributor 的便捷包装：全量遍历导出。</summary>
    IStateContributor AsContributor(StateNamespace ns);
}
```

**组装**：`GameInstance` 创建 `SimulationState`，收集所有实现了 `IStateContributor` 的模块（经 CAP-00 物化后解析）；`SimulationState` 对外只暴露 `IEnumerable<IStateContributor>` 与快照/哈希入口，不暴露散装读写——跨模块读数据走 Projection（CAP-17）或 Port。

## 4. 关键设计决策（含推荐）

**D1｜模块贡献制而非框架全管 KV（推荐）**：
- 框架全管 KV：归属清晰但把状态形态焊死成 KV，ECS/对象图类游戏被迫扭曲。
- 模块贡献制（推荐）：框架只管"谁拥有、怎么进出快照"，状态内部形态自由（模块可以内嵌自己的 ECS/棋盘）。代价：快照完备性靠纪律——用"快照后恢复哈希一致"测试（见验收）把纪律变成门禁。

**D2｜Capture/Restore 是唯一状态出口（推荐）**：禁止反射扫描私有字段做自动快照（脆弱、绕过归属、性能不可控）。自动化的诱惑很大，但反射扫字段一旦遇类型变更就静默丢数据，宁可显式。

**D3｜KV 默认实现的存在感（推荐轻量）**：提供 `IKeyValueState` 作为"简单场景别造轮子"的默认件，值类型必须可被 CAP-03 编解码；它是 `IStateContributor` 的一个内置实现，不是特权通道。避免各样例第一周就写出 10 种私有状态仓库。

**D4｜跨模块读：只读投影 vs 共享只读对象**：本能力不做跨模块读——跨模块读取走 Port/Facade（构造注入对方的只读接口）或 Projection（CAP-17）。把"读"也收进状态容器会重新制造耦合；Port 的类型安全与依赖显式性是顶层 3.8 既定方向。

## 5. 不变量

1. 模块只写自己 `StateNamespace` 内的状态；违反即架构违规（运行期无法廉价检测，靠 Capture 边界 + 测试把守）。
2. `StateNamespace` 全局唯一，Validate 阶段查重（CAP-00 已落地语义）。
3. `Capture` 禁止读时钟、随机、网络、文件（纯遍历）；违反在测试中暴露（capture 两次结果不一致）。
4. 快照完备性：捕获→恢复→再捕获，两次捕获字节级一致；全实例哈希一致。
5. 恢复失败不污染：Restore 先在草稿区进行或可整体放弃，恢复失败实例进入显式故障态，不做半恢复。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| Namespace 冲突 | Plan/Validate 启动失败（已落地） |
| Restore 读到无法解码的字段 | 同主版本：TryRead 默认值 + 诊断；跨主版本：整次恢复失败（要求先迁移，CAP-03） |
| Capture 中抛异常 | 快照失败，实例继续运行（快照是旁观者），诊断带 namespace |
| 恢复后哈希不一致 | CAP-09 分歧信号，进入 CAP-10 定位；测试环境直接 fail |

## 7. 使用方案

- **U1 Headless 测试**：每个模块的 contributor 有独立测试：capture→restore→capture 逐字节一致。
- **U2 单机**：存档 = SimulationState 快照；读档 = 迁移（CAP-03）+ Restore。
- **U3 联机权威**：checkpoint 周期性 Capture；玩家相关状态切片按 namespace 前缀切出（Character Save 的材料）。
- **U4 在线多 World**：每 World 一个 `SimulationState`；Meta 状态与 Live 状态分属不同实例，投影衔接（CAP-13）。

## 8. 与其他能力的关系

- 依赖：CAP-00（贡献与校验）、CAP-03（值可编解码）。
- 下游：CAP-07（管线与状态同生命周期）、CAP-09（快照/哈希遍历 contributors）、CAP-15（持久化搬运快照）、CAP-20（热更前确认 namespace 状态已处理）。

## 9. 非目标

- 不内置 ECS、实体组件系统（样例 MinimalAction 用最小自定义实体表验证边界；ECS 策略是顶层待确认项）。
- 不做状态变更订阅/脏标记广播（表现投影 CAP-17 决定读取方式；通用 dirty 机制等有真实第三模块需求再立项）。
- 不做跨实例状态同步（那是 CAP-12/16）。

## 10. 验收证据

- 完备性测试：全部模块 contributor 快照往返哈希一致（漏一个字段立即红）。
- 隔离测试：模块 A 的 Capture 不能触碰 B 的数据（以 B 快照前后哈希不变断言）。
- 纯度测试：Capture 期间注入时钟/随机探针，触发即失败。
- KV 实现的编解码往返测试（配合 CAP-03 Golden）。

## 11. 审核决策点

- **Q1**：D1 模块贡献制（状态形态自由）而非框架全管 KV——同意？这决定后续所有模块写状态的姿势。
- **Q2**：D2 禁止反射自动快照、强制显式 Capture/Restore——同意？
- **Q3**：D3 提供 KV 默认实现——同意轻量提供，还是干脆不提供（逼所有模块自定义）？
- **Q4**：D4 状态容器不做跨模块读（走 Port/Projection）——同意？
