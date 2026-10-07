# CAP-22 UI 框架 · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目标工程：合同进 `GameFramework.Contracts`（UI 面最小），实现进 `GameFramework.Adapters.Client.*` | 前置：CAP-14、CAP-17、CAP-21
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

UI 是客户端事故密度最高的子系统：窗口关了逻辑没停、资源开了没放、层级互相遮挡、UI 直接读模拟内部状态（耦合到无法测试）。需要统一的窗口生命周期、栈与层级、数据流向纪律。

**MMO 驱动力**：
- 长会话 UI 稳定性：MMO 界面多、叠层深（背包+聊天+组队+地图同时开），栈/层级模型必须是确定的。
- 全局通道：断线重连遮罩、公告弹窗、维护通知——框架级全局 UI 状态，不允许各窗口各自为政。
- 数据只来自投影：服务器真相经 CAP-17 投影到 UI，UI 永远不猜状态。

**通用性**：单机/联机共用；合同只管生命周期与数据流，具体控件体系（UGUI/UI Toolkit/自绘）是实现。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IUIWindow` | 窗口生命周期合同：加载、打开参数、焦点、关闭 |
| `UIStack` | 窗口栈：打开顺序、返回键语义、互斥组 |
| `UILayer` | 渲染层级：HUD < Panel < Popup < Toast < Loading < Global |
| `IUiDataBinding` | 窗口与投影的绑定接口（轻约定） |
| 全局通道 | 框架级 UI 状态：重连遮罩、公告、Toast |

## 3. 合同形状

```csharp
public interface IUIWindow where TWindow : IUIWindow
{
    /// <summary>窗口资源键（CAP-14 AssetKey），打开时经 scope 加载、关闭时随租约释放。</summary>
    AssetKey ResourceKey { get; }
    /// <summary>打开参数（类型化；打开路径唯一）。</summary>
    ValueTask OnOpenAsync(UIOpenContext context, CancellationToken ct);
    ValueTask OnFocusAsync(bool gained);
    ValueTask OnCloseAsync(UICloseReason reason);
    UILayer Layer { get; }
    bool Interactable { get; }              // 栈顶以下自动失焦置 false（实现默认）
}

public enum UICloseReason { Requested, BackNavigation, ReplacedByStack, ForceKicked, ScopeDisposed }

/// <summary>UI 管理面（客户端模块实现；挂在 IClientHost 下）。</summary>
public interface IUIManager
{
    ValueTask<TWindow> OpenAsync<TWindow>(UIOpenOptions options, CancellationToken ct) where TWindow : IUIWindow;
    ValueTask CloseAsync(IUIWindow window, UICloseReason reason);
    ValueTask CloseAllAsync(UILayer? layer, UICloseReason reason);   // 场景切换/踢出用
    UIStack Stack { get; }
    IGlobalUiChannel Global { get; }         // 重连遮罩、公告、Toast 的唯一入口
}

public sealed record UIOpenOptions(UILayer LayerOverride, bool Exclusive = false, string? Group = null);

/// <summary>数据流向：窗口订阅投影版本（CAP-17 D1），在 RenderAsync 中刷新。</summary>
public interface IUiDataBinding<TReadModel> where TReadModel : class
{
    /// <summary>返回 true 表示自上次刷新后投影有变化（版本比较）。</summary>
    bool TryConsumeUpdate(IProjection projection, out TReadModel model);
}
```

**数据流纪律**：

```text
IProjection（CAP-17，只读）
   → IUiDataBinding.TryConsumeUpdate（版本比较，脏了才刷）
   → 窗口刷新（表现帧内，CAP-21 帧泵驱动）
窗口动作 → ICommandSubmitter.Submit（CAP-17 D3 降权通道）
窗口资源 → AssetLease（scope = 窗口自身，关窗即还）
```

## 4. 关键设计决策（含推荐）

**D1｜栈 + 层级双模型（推荐）**：层级（Layer）决定渲染与覆盖关系（静态）；栈（Stack）决定返回键与焦点流（动态）。备选"只有层级"在移动端返回键语义上必然混乱——"按返回关哪个"必须由栈回答。

**D2｜窗口资源绑定窗口生命周期（推荐）**：每窗口一个隐式子 scope（CAP-14），关窗 = scope 释放 = 资源回收。UI 泄漏从"纪律问题"变成"结构不可能"（除非绕过 Manager 直接开资源，审计可查）。

**D3｜不内置数据绑定引擎（推荐）**：`IUiDataBinding` 只约定"版本比较 + 拉取 read model"的最小面；MVVM/响应式框架（UniRx、UniTask 绑定等）可后挂实现此接口。核心不绑具体绑定库——绑定引擎换代不影响合同。

**D4｜全局通道集中化（推荐）**：重连遮罩、Toast、公告由 `IGlobalUiChannel` 统一进出（事件来自 CAP-16 断连、CAP-12 踢出、运营公告事件）。禁止各功能窗口自行弹"断线了"——三个系统同时弹遮罩是 MMO 客户端经典事故。

**D5｜输入路由分工（推荐）**：UI 命中测试（点击在哪个窗口）属 UI 实现与 CAP-23 设备层的交界——合同约定：被 UI 消费的输入事件不再进入游戏动作映射（事件截断语义），避免"点按钮同时触发了技能"。

## 5. 不变量

1. UI 只读投影、只发命令：直连模拟状态的 UI 代码是架构违规（依赖门禁可查）。
2. 窗口资源生命周期 = 窗口生命周期（D2）；跨窗口共享资源显式挂上级 scope。
3. 同一窗口类型同时只允许一个实例（默认；参数可开多实例并显式命名）。
4. 关闭原因必达：`OnCloseAsync` 收到 reason——窗口能区分"用户关的"和"被踢关的"。
5. 返回键语义唯一由栈决定；HUD 层不参与返回栈。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| 窗口 OnOpenAsync 抛异常 | 窗口关闭 + 资源回收 + 错误上报；不阻塞其他窗口 |
| 投影 read model 缺失（投影未就绪） | 窗口显示加载态，不显示旧数据冒充新数据 |
| 重复打开同类型 | 返回已有实例并重入参数（幂等）；或按 options 报错——默认幂等 |
| 关闭时窗口已销毁（平台回收） | 幂等，资源回收照常（scope 兜底） |
| Global 通道冲突（公告撞公告） | 排队（栈内全局层），不互相顶掉 |

## 7. 使用方案

- **U1**：UI 逻辑（栈/层级/状态机）headless 单测（假窗口）；渲染面不测。
- **U2 单机**：全套；返回键/Esc 由栈处理。
- **U3 联机权威**：重连遮罩走 Global 通道（CAP-12/16 事件驱动）；服务器公告经事件通道进 Global。
- **U4**：同 U3；ShowWorld 的 UI 面是本合同的一个 Unity 实现。

## 8. 与其他能力的关系

- 依赖：CAP-14（窗口资源）、CAP-17（投影与命令通道）、CAP-21（宿主与帧泵）。
- 下游：CAP-23（输入截断语义）、CAP-26（UI 音效触发点）、CAP-25（更新进度 UI）。

## 9. 非目标

- 不做控件库、主题皮肤系统（实现层）。
- 不做 UI 自动化测试框架录制器（测试工具后续专项）。
- 不做公告/邮件的内容编排（运营 Features 层；全局通道只是展示口）。

## 10. 验收证据

- 栈/层级状态机单测：打开-焦点-返回-互斥-强制关闭全路径。
- 资源回收测试：开关 1000 次窗口，租约计数归零（配合 CAP-14 假加载器）。
- 数据流测试：投影版本不更新则 UI 不刷新（防止轮询习惯）。
- Global 通道互斥测试。

## 11. 审核决策点

- **Q1**：D2 窗口资源绑定窗口生命周期（每窗口隐式子 scope）——同意？
- **Q2**：D3 不内置绑定引擎、只留最小接口——同意？
- **Q3**：D4 全局通道集中化（重连/公告/Toast 唯一入口）——同意？
- **Q4**：重复打开默认幂等重入而非报错——接受默认行为吗？
