using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.Client;
using Lyking.GameFramework.Client.Core.Events;
using Lyking.GameFramework.Client.Core.Fsms;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.Procedures;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Core.Scheduling;
using Lyking.GameFramework.Client.Services.DataTable;
using Lyking.GameFramework.Client.Services.Entity;
using Lyking.GameFramework.Client.Services.Network;
using Lyking.GameFramework.Client.UI;

namespace Lyking.GameFramework.Client.Demo;

/// <summary>演示用服务上下文：真实项目通常由 GameEntry 静态门面承担同样的角色。</summary>
public static class DemoContext
{
    public static IServiceRegistry? Services { get; set; }
}

// ===================== 游戏流程 =====================

/// <summary>启动流程：展示闪屏 0.1 秒后交给加载流程。</summary>
public sealed class SplashProcedure : ProcedureState
{
    protected override void OnEnter(Fsm fsm) => Console.WriteLine("[流程] 闪屏展示。");

    protected override void OnUpdate(Fsm fsm, double deltaTime)
    {
        if (fsm.TimeInState >= 0.1)
        {
            fsm.ChangeState<LoadingProcedure>();
        }
    }
}

/// <summary>加载流程：帧推进 0.3 秒后进入主城。</summary>
public sealed class LoadingProcedure : ProcedureState
{
    protected override void OnEnter(Fsm fsm) => Console.WriteLine("[流程] 开始加载（数据表 + 实体预热）。");

    protected override void OnUpdate(Fsm fsm, double deltaTime)
    {
        if (fsm.TimeInState >= 0.3)
        {
            fsm.ChangeState<MainCityProcedure>();
        }
    }
}

/// <summary>主城流程：打开主界面。</summary>
public sealed class MainCityProcedure : ProcedureState
{
    protected override void OnEnter(Fsm fsm)
    {
        Console.WriteLine("[流程] 进入主城。");
        var ui = DemoContext.Services!.Resolve<UIWindowService>();
        ui.Open(new UIWindowDescriptor("主界面", UILayer.Main), new LogView("主界面"));
    }
}

// ===================== 业务模块 =====================

/// <summary>演示模块：装配窗口服务，加载道具表，展示/隐藏实体。</summary>
[GameModule("GamePlay", priority: 10)]
public sealed class GamePlayModule : ClientModule
{
    private UIWindowService? _windows;
    private EntityService? _entities;

    public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
    {
        _windows = new UIWindowService(environment.Logger);
        _entities = new EntityService(new DemoEntityProvider());
        _entities.AddGroup("npc", capacity: 2);
        environment.Services.Register(_windows);
        environment.Services.Register(_entities);
        DemoContext.Services = environment.Services;
        return base.InitializeAsync(environment, cancellationToken);
    }

    public override async ValueTask ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_entities != null)
        {
            await _entities.ShutdownAsync(cancellationToken);
        }

        await base.ShutdownAsync(cancellationToken);
    }
}

/// <summary>演示模块：加载阶段读数据表、展示实体；主城阶段由流程驱动 UI。</summary>
[GameModule("Loader", priority: 20, Dependencies = new[] { "GamePlay" })]
public sealed class LoaderModule : ClientModule
{
    /// <summary>演示数据行：解析必须有界可变，不能用 record。</summary>
    private sealed class ItemRow : IDataRow
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public int Price { get; private set; }

        public bool ParseRow(IReadOnlyList<string> cells)
        {
            if (cells.Count < 3 || !int.TryParse(cells[0], out int id) || !int.TryParse(cells[2], out int price))
            {
                return false;
            }

            Id = id;
            Name = cells[1];
            Price = price;
            return true;
        }
    }

    public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
    {
        var tables = new DataTableService();
        var table = tables.LoadFromText<ItemRow>("item",
            "id\t名称\t价格\n1\t长剑\t100\n2\t盾牌\t80\n3\t药水\t30");
        environment.Services.Register(tables);
        foreach (ItemRow row in table.Rows)
        {
            Log.Info($"[数据表] 道具 {row.Id}: {row.Name}（{row.Price} 金）");
        }

        return base.InitializeAsync(environment, cancellationToken);
    }

    public override void Start()
    {
        var entities = Resolve<EntityService>();
        int guard = entities.Show("npc-guard", "npc", userData: "城门岗哨");
        int cat = entities.Show("npc-cat", "npc");
        Log.Info($"[实体] 展示守卫 #{guard} 与猫 #{cat}（组容量 2）。");
    }
}

/// <summary>演示模块：窗口栈与焦点，用计时器在 0.25 秒后自动关闭弹窗。</summary>
[GameModule("UiDemo", priority: 30, Dependencies = new[] { "GamePlay" })]
public sealed class UiDemoModule : ClientModule
{
    public record PopupClosed(string WindowName);

    public override void Start()
    {
        var windows = Resolve<UIWindowService>();
        var timer = Resolve<TimerService>();
        windows.Open(new UIWindowDescriptor("公告弹窗", UILayer.Popup, modal: true), new LogView("公告弹窗"));
        Log.Info("打开模态公告弹窗，0.25 秒后自动关闭。");
        timer.Schedule(TimeSpan.FromSeconds(0.25), () =>
        {
            windows.Close("公告弹窗");
            Resolve<IEventBus>().Publish(new PopupClosed("公告弹窗"));
        });
    }
}

/// <summary>演示模块：环回网络，连接后收一条"服务器"下发消息并回发。</summary>
[GameModule("NetDemo", priority: 40, Dependencies = new[] { "GamePlay" })]
public sealed class NetDemoModule : ClientModule
{
    private const int PingMessageId = 42;

    private readonly LoopbackTransport _transport = new();
    private readonly NetworkService _network;
    private bool _delivered;

    public NetDemoModule()
    {
        _network = new NetworkService(_transport, options: new NetworkOptions
        {
            HeartbeatInterval = TimeSpan.FromSeconds(1),
        });
    }

    public override async ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
    {
        await _network.InitializeAsync(environment, cancellationToken);
        _network.Router.Register(PingMessageId, body =>
            Log.Info($"[网络] 收到服务器消息：{Encoding.UTF8.GetString(body.Span)}"));
        await base.InitializeAsync(environment, cancellationToken);
    }

    public override void Start()
    {
        Log.Info("[网络] 连接环回服务器。");
        _ = _network.ConnectAsync();
    }

    public override void Update(double deltaTime)
    {
        if (!_delivered && _network.IsConnected)
        {
            _delivered = true;
            _transport.Deliver(new PrefixMessageCodec().Frame(PingMessageId, Encoding.UTF8.GetBytes("ping")));
        }

        _network.Update(deltaTime);
    }

    public override ValueTask ShutdownAsync(CancellationToken cancellationToken)
    {
        Log.Info("[网络] 关闭连接。");
        _network.Close();
        return base.ShutdownAsync(cancellationToken);
    }
}

/// <summary>演示模块：订阅跨模块事件。</summary>
[GameModule("MetricsDemo", priority: 50, Dependencies = new[] { "UiDemo" })]
public sealed class MetricsDemoModule : ClientModule
{
    public override ValueTask InitializeAsync(ModuleEnvironment environment, CancellationToken cancellationToken)
    {
        environment.Services.Resolve<IEventBus>().Subscribe<UiDemoModule.PopupClosed>(
            evt => Log.Info($"[指标] 弹窗 {evt.WindowName} 已关闭，可统计停留时长。"));
        return base.InitializeAsync(environment, cancellationToken);
    }
}

// ===================== 演示视图与实体 =====================

/// <summary>窗口逻辑：业务窗口继承 UIFormLogic 的样板。</summary>
public sealed class LogView : UIFormLogic
{
    private readonly string _name;

    public LogView(string name) => _name = name;

    protected override void OnOpen(object? userData) => Console.WriteLine($"[视图] {_name} 打开（数据：{userData ?? "无"}）");

    protected override void OnFocus(bool focused) => Console.WriteLine($"[视图] {_name} {(focused ? "获得焦点" : "失去焦点")}");

    protected override void OnPause(bool paused) => Console.WriteLine($"[视图] {_name} {(paused ? "被模态暂停" : "恢复")}");

    protected override void OnClose() => Console.WriteLine($"[视图] {_name} 关闭");
}

/// <summary>实体提供器：真实项目在 Unity 侧实例化 prefab；这里直接构造逻辑对象。</summary>
public sealed class DemoEntityProvider : IEntityInstanceProvider
{
    public IEntityLogic Create(string key)
    {
        Console.WriteLine($"[实体] 创建实例：{key}");
        return new DemoEntity(key);
    }

    private sealed class DemoEntity : EntityLogic
    {
        public DemoEntity(string key) => Key = key;

        public override void OnShow(object? userData) =>
            Console.WriteLine($"[实体] {Key}#{EntityId} 展示（数据：{userData ?? "无"}）");

        public override void OnHide(object? userData) => Console.WriteLine($"[实体] {Key}#{EntityId} 隐藏");
    }
}

// ===================== 入口 =====================

public static class Program
{
    public static Task Main()
    {
        // 商业项目推荐入口：流式装配 + 特性发现。
        var client = GameClientBuilder.Create()
            .WithSink(new DelegateLogSink((_, message) => Console.WriteLine(message)))
            .WithModule<TimerService>()
            .WithModule(new ProcedureModule()
                .AddProcedure(new SplashProcedure())
                .AddProcedure(new LoadingProcedure())
                .AddProcedure(new MainCityProcedure())
                .SetInitial<SplashProcedure>())
            .WithModulesFrom(typeof(Program).Assembly)   // 扫描 [GameModule] 标注的业务模块
            .Build();

        using var commercialServices = new CommercialDemoServices(client.Dispatcher);
        commercialServices.Register(client.Services);

        PumpUntil(client, client.StartAsync());
        PumpUntil(client, client.Dispatcher.InvokeAsync(() => commercialServices.RunAsync(client.Services)));
        for (int frame = 1; frame <= 5; frame++)
        {
            Console.WriteLine($"---- 第 {frame} 帧 (dt=0.1s) ----");
            client.Update(0.1);
        }

        PumpUntil(client, client.ShutdownAsync());
        return Task.CompletedTask;
    }

    private static void PumpUntil(GameClient client, Task task)
    {
        while (!task.IsCompleted)
        {
            client.Dispatcher.Pump();
            Thread.Sleep(1);
        }

        client.Dispatcher.Pump();
        task.GetAwaiter().GetResult();
    }
}
