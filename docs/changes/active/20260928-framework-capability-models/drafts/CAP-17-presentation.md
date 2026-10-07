# CAP-17 表现会话 · 设计草稿

> 状态：**待审核** | 能力域：D 平台适配 | 目标工程：合同进 `GameFramework.Contracts`，实现进各平台 adapter（Unity 等） | 前置：CAP-07
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

客户端/编辑器如何消费模拟结果而不反向污染权威。没有这层合同：表现代码直接读模拟内部状态（紧耦合）、直接改状态（破坏权威）、模拟为表现妥协（比如为动画暂停逻辑 tick）。

**MMO 驱动力**：
- 服务端无表现，客户端全靠投影同步——投影的完备性与顺序就是客户端体验的下限。
- 多端（PC/手机/云游戏）同服：投影是同一份，表现各自实现。
- 断线补播（CAP-12）的落点就是投影重建。

**通用性**：Headless 完全无此层（编译期可选）；Web/编辑器/Unity 各自实现 read-model adapter；回合制与动作游戏的差异只是投影内容，不是合同。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IProjection` | 从权威状态/事件派生的只读视图（查询面） |
| `IProjectionBuilder` | 投影构建器：订阅事件通道，维护只读对象 |
| `IPresentationSession` | 表现会话：持有投影、驱动表现更新、把输入转为命令提交 |
| read model | 投影输出的具体形态（实体快照表、UI 状态对象） |

## 3. 合同形状

```csharp
/// <summary>只读投影查询面。实现必须线程安全（表现线程读取）。</summary>
public interface IProjection
{
    /// <summary>投影当前版本号 = 已消费的 EventSequence；表现层据此判断是否需要刷新。</summary>
    EventSequence Version { get; }
    /// <summary>按类型读取 read model（未构建返回 null，不抛）。</summary>
    TReadModel? Read<TReadModel>() where TReadModel : class;
}

/// <summary>投影构建：Contribute 阶段注册；运行于订阅者侧（不占模拟 tick）。</summary>
public interface IProjectionBuilder
{
    /// <summary>声明要订阅的事件类型（Validate 可查"声明了无人发布"）。</summary>
    void Subscribe(IEventChannelRegistry channels);
    /// <summary>初始全量构建（快照恢复/重连全同步后调用一次）。</summary>
    void BuildInitial(ISimulationSnapshot snapshot);
    IProjection Projection { get; }
}

/// <summary>表现会话：平台 adapter 实现（Unity/Godot/Web/编辑器）。</summary>
public interface IPresentationSession : IAsyncDisposable
{
    IProjection Projection { get; }
    ICommandBus Commands { get; }        // 唯一的反向通道：只能提交命令
    /// <summary>表现帧驱动（渲染帧率）；与模拟 tick 解耦。</summary>
    ValueTask RenderAsync(CancellationToken ct);
}
```

**依赖方向（合同核心）**：

```text
Simulation ──事件──▶ ProjectionBuilder ──▶ IProjection（只读）
IProjection ◀──读取── PresentationSession（Unity 适配层）
PresentationSession ──ICommandBus.Submit──▶ Simulation
（ICommandBus 接口类型被降权暴露：只含 Submit 族，不含管理方法——见 D3）
```

## 4. 关键设计决策（含推荐）

**D1｜投影版本 = 事件游标（推荐）**：`IProjection.Version` 就是最后消费的 `EventSequence`——CAP-12 断线补播、CAP-10 重放、表现脏检查三者共用同一标尺。表现层 `RenderAsync` 比较 Version 决定重建哪部分视图。

**D2｜初始全量 + 增量事件（推荐双轨）**：`BuildInitial` 从快照一次建全量；此后事件增量维护。禁止"表现层启动时自己扫描状态"——扫描路径一旦存在，就会有人拿它做常驻查询，模拟被表现绑架。

**D3｜命令通道降权暴露（推荐）**：`IPresentationSession.Commands` 的类型是 `ICommandBus` 的**只提交子接口**（`ICommandSubmitter`：只有 Submit），拿不到 Drain、拿不到 handler 注册。类型系统挡住"表现层顺手管理模拟"。

**D4｜投影线程模型：构建单线程、读多线程（推荐）**：事件投递在模拟侧线程完成后由 builder 串行构建（CAP-07 订阅者模型）；`IProjection` 读取面做不可变快照切换（写新版本对象、原子替换引用），表现线程任意时刻读到的是一致版本。Unity 主线程读、投影线程写的经典竞争就此消除。

**D5｜预测的落点在表现会话（推荐）**：客户端预测（CAP-08 D3）的表现合并逻辑属于 PresentationSession 实现：本地立即播放预测结果 + 权威投影到达后校正。核心合同不定义预测表现 API（各平台手感策略不同），只保证投影里能区分"权威已确认"与"预测未确认"（read model 上的标记由玩法层投影自行携带）。

**D6｜ShowWorld 是一个实现不是合同（推荐，顶层 4.5 既定）**：Presenter/Binder/RuntimeSlice 等 ShowWorld 词汇属于 Unity/Hydra 适配包。核心只认 `IPresentationSession`/`IProjection`——防止三界词汇渗入核心。

## 5. 不变量

1. 表现层对模拟的唯一合法动作 = `Submit`（命令）；一切"直接改"都是架构违规。
2. `IProjection` 只读：接口无写方法；实现返回的 read model 类型约定为不可变（analyzer/约定测试把守）。
3. 投影滞后不反馈进模拟：表现慢、丢帧、重建失败都不影响模拟推进。
4. Headless 构建不编译表现会话（依赖方向：核心不知道表现类型存在）。
5. 投影版本单调不减，且 ≤ 最新 `EventSequence`。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 事件消费（builder）抛异常 | 记诊断、跳过该事件、继续后续（投影容忍局部缺口，标记 Version 说明）；连续失败健康上报 |
| read model 与事件不匹配（类型漂移） | 构建期 Validate 提示（订阅了无人发布/发布了无人订阅均为警告级） |
| 表现层提交被拒 | 表现层自行处理（撤销预测动画等）；模拟侧已按 CAP-08 产出拒绝事件 |
| 全量重建失败 | 表现会话进入错误态（黑屏/占位），不影响模拟；重试重建 |

## 7. 使用方案

- **U1**：不适用（Headless 无表现会话；投影 builder 本身可独立测试）。
- **U2 单机**：Unity adapter 的 PresentationSession；本地渲染帧驱动。
- **U3 联机权威**：客户端 PresentationSession + 预测表现合并（D5）；服务端无此层。
- **U4 在线多 World**：ShowWorld = 表现会话的实现（顶层 2.1：投影消费方，不得写真相）。

## 8. 与其他能力的关系

- 依赖：CAP-07（事件通道订阅、命令提交）、CAP-09（BuildInitial 的快照材料）。
- 下游：CAP-12（补播后投影重建）、CAP-14（表现资源加载）、CAP-13（ShowWorld 实现层）。

## 9. 非目标

- 不定义 UI 框架、动画系统、音频（平台 adapter 内部）。
- 不做表现层状态持久化（会话结束即弃；需要持久的表现偏好走存档命令）。
- 不做投影网络传输优化（delta 压缩等属 transport/profile 后续优化，合同不变）。

## 10. 验收证据

- 投影 builder 单测：初始全量 + 事件增量的版本单调性、只读性（类型审计）。
- 线程安全测试：投影写线程 + 多读线程并发读一致性（快照切换）。
- 端到端：MinimalAction 样例接 Unity 前先以"headless 投影消费者"验证闭环（打印 read model 变化轨迹）。
- 依赖门禁：核心程序集不引用任何表现类型（编译期）。

## 11. 审核决策点

- **Q1**：D1 投影版本 = EventSequence 游标（补播/脏检查共用标尺）——同意？
- **Q2**：D3 表现层命令通道降权为只提交子接口——同意？
- **Q3**：D4 不可变快照切换的投影线程模型——同意？（这是 Unity 端实现前必须锁定的并发决策）
- **Q4**：D6 ShowWorld 词汇不进核心合同——确认？
