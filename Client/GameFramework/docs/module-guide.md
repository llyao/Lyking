# 模块开发指南

面向业务开发者的实战手册：如何用本框架写一个功能完整的游戏客户端模块。架构背景见[架构与分层](architecture.md)。

## 1. 一个模块的完整样子

```csharp
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;

[GameModule("签到", priority: 60, Dependencies = new[] { "GamePlay", "Network" })]
public sealed class SignInModule : ClientModule
{
    // —— 生命周期 ——

    public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken ct)
    {
        // 环境已由宿主预注入：Log、Resolve、TryResolve 在这里就可以用
        // （不需要也不必先调用 base.InitializeAsync）
        Log.Info("签到模块初始化");
        return default;
    }

    public override void Start()
    {
        // 所有模块初始化完成后执行；适合发起异步请求、打开常驻界面
        var timer = Resolve<TimerService>();
        timer.ScheduleRepeating(TimeSpan.FromHours(1), Refresh);
    }

    public override void Update(double deltaTime) { /* 每帧逻辑，保持轻量 */ }

    public override ValueTask ShutdownAsync(CancellationToken ct)
    {
        Log.Info("签到模块关闭（逆序：后启动的先关闭）");
        return default;
    }

    private void Refresh() { }
}
```

要点：

- **[GameModule] 特性**是模块的唯一身份声明：`Name`（缺省取类型名）、`Priority`（小者先初始化先更新）、`Dependencies`（依赖模块名）。`GameClientBuilder.WithModulesFrom(assembly)` 只会注册标注了特性的类型。
- **名字即依赖契约**：`Dependencies` 里的名字必须真实存在，写错启动即报 `ModuleOrderException`（含完整诊断）。
- **不要在 Update 里做每帧分配**：复用 `ListPool<T>` / `ReferencePool` / `ObjectPool<T>`。

## 2. 装配客户端（GameClientBuilder）

```csharp
var client = GameClientBuilder.Create()
    .WithSink(new UnityLogSink())                          // 或 WithLogger(...) 直指定
    .WithModule<TimerService>()                            // 想控制内置服务排序就显式登记
    .WithModule(new NetworkService(transport))             // 需要构造参数的模块传实例
    .WithModulesFrom(typeof(GameEntry).Assembly)           // 其余业务模块按特性扫描
    .ConfigureServices(services =>
    {
        services.Register<IAssetProvider>(new ResourcesAssetProvider());
        services.Register<IEventBus>(client0 => ...);      // 注意：Build 前拿不到 client，请用入参
    })
    .Build();
```

- `ConfigureServices` 在 `Build()` 时按注册顺序执行，早于任何模块的 `InitializeAsync`，模块初始化时即可 `Resolve`。
- **重复模块名在 `Build()` 阶段立即报错**，不用等到运行期。
- 显式登记 `TimerService`（同名内置服务）时以你登记的实例为准。

## 3. 界面：窗口 + UIFormLogic

```csharp
// 业务窗口继承 UIFormLogic，只写"什么时机做什么"
public sealed class ShopForm : UIFormLogic
{
    private BindableProperty<int> gold = new(0);

    protected override void OnOpen(object? userData)
    {
        // userData 是 Open 时携带的上下文（如 "FromMainCity"）
        gold.Bind((_, v) => RenderGold(v));       // Bind 立即回调一次，天然完成首帧渲染
    }

    protected override void OnPause(bool paused)
    {
        // 被更高层模态窗口压制/恢复时回调：暂停轮询、静音、停止输入
    }

    protected override void OnClose() { /* 解绑事件，Presenter.Detach() */ }
}

// 打开：层级、唯一性、模态都是描述符的一部分
var ui = client.Services.Resolve<UIWindowService>();
ui.Open(new UIWindowDescriptor("商店", UILayer.Main, unique: true), new ShopForm(), userData: "FromMainCity");
ui.Open(new UIWindowDescriptor("确认购买", UILayer.Popup, modal: true), new ConfirmForm());
```

窗口规则速查：

| 规则 | 行为 |
|---|---|
| 层级 | `Background(0) → Main(10) → Popup(20) → Guide(30) → Loading(40) → Toast(50)`，数值大者在栈顶 |
| 焦点 | 全局唯一：最高层栈顶窗口收到 `OnFocus(true)`，其余 `OnFocus(false)` |
| `unique: true` | 重复打开只置顶返回既有窗口，不新建视图 |
| `exclusive: true` | 打开时关闭所有更低层的窗口 |
| `modal: true` | 打开期间更低层窗口收到 `OnPause(true)`，关闭后恢复 `OnPause(false)` |

## 4. 实体：分组容量 + 实例池

```csharp
var entities = client.Services.Resolve<EntityService>();

entities.AddGroup("battle-skill-fx", capacity: 32);   // 容量上限，防特效失控
int fxId = entities.Show("fx-fireball", "battle-skill-fx", userData: castContext);
// 组满时自动回收"最早展示且未固定"的实体；主角等常驻实体用 pinned: true
int heroId = entities.Show("hero", "battle", pinned: true);

entities.Hide(fxId);          // 归池（Unity 侧实现可在此隐藏 GameObject 而不是销毁）
```

Unity 侧实现 `IEntityInstanceProvider`：`Create(key)` 实例化 prefab 并返回逻辑组件（包装成 `IEntityLogic`），隐藏/显示由逻辑对象在 `OnShow/OnHide` 里驱动。

## 5. 游戏流程（Procedure）

```csharp
public sealed class CheckVersionProcedure : ProcedureState
{
    protected override void OnEnter(Fsm fsm)
    {
        // 进入时发起版本检查；不要在这里 await 长耗时任务
        CheckAsync();
    }

    private async void CheckAsync()
    {
        try
        {
            await DownloadVersionListAsync();
            ChangeState<DownloadPatchesProcedure>();     // 状态内部自主迁移
        }
        catch (Exception ex)
        {
            ChangeState<VersionErrorProcedure>();        // 失败也是一次状态迁移
        }
    }
}
```

- 流程机是 `Core/Fsms/Fsm` 的特化（`ProcedureModule`），一个游戏一条主流程；AI、加载管线等子流程用 `FsmService.CreateFsm("ai", ...)` 并存。
- 同状态重入会完整走 `OnExit → OnEnter`，迁移顺序固定为"先退出旧状态再进入新状态"。
- `Fsm.TimeInState` 是帧时间累计，流程内延时判断用它，不要用 `Task.Delay`（与暂停/倍速不兼容）。

## 6. 网络

```csharp
var transport = new KcpTransport("gateway.lyking.dev", 9000);   // 平台实现
var network = new NetworkService(transport, codec: new ShareProtocolCodec(),
    new NetworkOptions
    {
        HeartbeatInterval = TimeSpan.FromSeconds(5),   // 0 = 关闭心跳
        ReconnectMaxAttempts = 5,
        ReconnectBaseDelay = TimeSpan.FromSeconds(1),  // 指数退避 1s→2s→4s…
    });

network.Router.Register(Protocol.LoginResponse, body => OnLogin(Protocol.Decode(body)));
network.Router.OnUnhandled += (id, body) => Log.Warn($"未注册消息 {id}");
await network.ConnectAsync();
await network.SendAsync(Protocol.LoginRequest, payload);

// 断线（远端断开）自动重连；network.Close() 是主动关闭，不触发重连。
// 重连次数耗尽触发 ReconnectGaveUp，人工恢复调用 ConnectAsync()。
```

第一版内置的 `PrefixMessageCodec`（4 字节小端 id + body）只是演示约定；正式协议在 `Share/` 定义后实现 `IMessageCodec` 即可整体替换。

## 7. 计时、事件、数据表、引用池

```csharp
// 计时：帧时间驱动，回调在主线程
var handle = Timers.Schedule(TimeSpan.FromSeconds(3), ShowReward);        // 一次性
var loop   = Timers.ScheduleRepeating(TimeSpan.FromMinutes(1), SyncTime);  // 重复
handle.Cancel();

// 事件：跨模块解耦通信
Events.Subscribe<PlayerLevelUp>(evt => RefreshRedDots(evt.NewLevel));
Events.Publish(new PlayerLevelUp(42));

// 数据表：表格文本来自资源层，行类型自己解析
var table = tables.LoadFromText<ItemRow>("item", itemTableText);
var shop  = table.Where(r => r.Price < 100).OrderBy(r => r.Id);

// 引用池：高频小对象（弹道、飘字数据）
var msg = References.Rent<DamagePopup>();    // 实现 IReference，归还会自动 Clear
References.Return(msg);
```

## 8. 常见坑

| 坑 | 说明 |
|---|---|
| 在 `Update` 里 new 大对象 | 用 `ReferencePool`/`ListPool`；框架所有容器按主线程单线程设计 |
| 忘记 `Dependencies` | 模块间隐式依赖顺序不可靠，全部显式声明；顺序错了启动即报，不要靠 Priority 硬调 |
| 双重归还池对象 | `ReferencePool`/`ListPool` 会直接抛异常，不要 catch 后继续跑 |
| 在流程 `OnEnter` 里 await | 迁移在回调内完成，await 之后的代码时序不可控；起异步任务，完成后 `ChangeState` |
| 心跳依赖真实时钟 | 所有计时基于帧时间累计，后台切走/倍速场景下语义一致 |
| 模拟实现带上生产 | `LoopbackTransport`、`SimulationDownloadAgent` 等仅供测试与无头演示，生产必须替换接口实现 |
| IL2CPP 裁剪扫描失效 | `WithModulesFrom` 依赖反射，为被扫描程序集配置 link.xml 保留类型 |

## 9. 写完之后

1. 为模块的每个对外行为写一个 xunit 用例（参考 `Tests/` 现有用例的命名与结构）。
2. `dotnet test Client/GameFramework/Tests/Lyking.GameFramework.Client.Tests.csproj` 保持全绿。
3. 复杂流程先在 `Demo/` 里用模拟实现走通，再接 Unity 真实实现。
4. 涉及协议、存档的改动回到 `docs/changes/` 走变更流程（协议与存档是受控范围）。
