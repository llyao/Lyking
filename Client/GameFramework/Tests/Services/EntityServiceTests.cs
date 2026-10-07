using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.Entity;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

/// <summary>带轨迹的测试实体。</summary>
internal sealed class StubEntity : EntityLogic
{
    public StubEntity(string tag) => Tag = tag;

    public string Tag { get; }

    public List<string> Calls { get; } = new();

    public object? LastShowData { get; private set; }

    public object? LastHideData { get; private set; }

    public Exception? ThrowOnUpdate { get; set; }

    public override void OnInit() => Calls.Add("init");

    public override void OnShow(object? userData)
    {
        base.OnShow(userData);
        LastShowData = userData;
        Calls.Add("show");
    }

    public override void OnUpdate(double deltaTime)
    {
        Calls.Add($"update:{deltaTime}");
        if (ThrowOnUpdate != null)
        {
            throw ThrowOnUpdate;
        }
    }

    public override void OnHide(object? userData)
    {
        base.OnHide(userData);
        LastHideData = userData;
        Calls.Add("hide");
    }

    public override void OnReset() => Calls.Add("reset");
}

/// <summary>模拟实例提供器：按 key 注册实体工厂，记录创建轨迹。</summary>
internal sealed class StubEntityProvider : IEntityInstanceProvider
{
    private readonly Dictionary<string, Func<IEntityLogic>> _factories;

    public StubEntityProvider(IReadOnlyDictionary<string, Func<IEntityLogic>> factories)
    {
        _factories = new Dictionary<string, Func<IEntityLogic>>(factories);
    }

    public List<IEntityLogic> Created { get; } = new();

    public IEntityLogic Create(string key)
    {
        if (!_factories.TryGetValue(key, out Func<IEntityLogic>? factory))
        {
            throw new InvalidOperationException($"未注册实体 key：{key}");
        }

        IEntityLogic logic = factory();
        Created.Add(logic);
        return logic;
    }
}

public sealed class EntityServiceTests
{
    private static ModuleEnvironment CreateEnvironment() => new(
        new ServiceRegistry(),
        new Logger("t", new DelegateLogSink((_, _) => { })));

    private static (EntityService Service, StubEntityProvider Provider) CreateService(
        params (string Name, int Capacity)[] groups)
    {
        var provider = new StubEntityProvider(new Dictionary<string, Func<IEntityLogic>>
        {
            ["npc"] = () => new StubEntity("npc"),
            ["effect"] = () => new StubEntity("effect"),
            ["bad"] = () => new StubEntity("bad") { ThrowOnUpdate = new InvalidOperationException("坏实体") },
        });
        var service = new EntityService(provider);
        service.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        foreach ((string name, int capacity) in groups)
        {
            service.AddGroup(name, capacity);
        }

        // 默认测试组。
        if (!service.HasGroup("g"))
        {
            service.AddGroup("g", 0);
        }

        return (service, provider);
    }

    [Fact]
    public void 展示隐藏生命周期与userData透传()
    {
        var (service, provider) = CreateService(("g", 0));

        int id = service.Show("npc", "g", userData: "进场");

        var logic = Assert.IsType<StubEntity>(provider.Created.Single());
        Assert.True(service.IsVisible(id));
        Assert.Equal(1, service.VisibleCount("g"));
        Assert.Equal("npc", logic.Key);
        Assert.Equal(id, logic.EntityId);
        Assert.Equal("进场", logic.LastShowData);
        Assert.Equal(new[] { "init", "show" }, logic.Calls);

        Assert.True(service.Hide(id, userData: "离场"));
        Assert.False(service.IsVisible(id));
        Assert.Equal("离场", logic.LastHideData);
        Assert.Equal(new[] { "init", "show", "hide", "reset" }, logic.Calls);
        Assert.Equal(1, service.PooledCount("npc"));
    }

    [Fact]
    public void 实例回池复用且OnInit只执行一次()
    {
        var (service, provider) = CreateService();
        int firstId = service.Show("npc", "g");
        Assert.True(service.Hide(firstId));

        int secondId = service.Show("npc", "g");

        Assert.Single(provider.Created);
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(new[] { "init", "show", "hide", "reset", "show" },
            ((StubEntity)provider.Created[0]).Calls);
    }

    [Fact]
    public void 组容量满自动回收最早未固定实体()
    {
        var (service, _) = CreateService(("battle", 2));
        int a = service.Show("npc", "battle");
        int b = service.Show("npc", "battle");

        int c = service.Show("effect", "battle");

        Assert.False(service.IsVisible(a));
        Assert.True(service.IsVisible(b));
        Assert.True(service.IsVisible(c));
        Assert.Equal(2, service.VisibleCount("battle"));
    }

    [Fact]
    public void 固定实体不参与自动回收()
    {
        var (service, _) = CreateService(("battle", 2));
        int pinned = service.Show("npc", "battle", pinned: true);
        int b = service.Show("npc", "battle");

        int c = service.Show("effect", "battle");

        Assert.True(service.IsVisible(pinned));
        Assert.False(service.IsVisible(b));
        Assert.True(service.IsVisible(c));
        Assert.True(service.IsPinned(pinned));
    }

    [Fact]
    public void 全部固定且组满抛出()
    {
        var (service, _) = CreateService(("battle", 1));
        service.Show("npc", "battle", pinned: true);

        Assert.Throws<InvalidOperationException>(() => service.Show("effect", "battle"));
    }

    [Fact]
    public void 未注册组或key抛出()
    {
        var (service, _) = CreateService(("g", 0));
        Assert.Throws<InvalidOperationException>(() => service.Show("npc", "不存在"));
        Assert.Throws<InvalidOperationException>(() => service.Show("未注册key", "g"));
        Assert.Throws<InvalidOperationException>(() => service.AddGroup("g"));
    }

    [Fact]
    public void 帧更新驱动全部可见实体且异常隔离()
    {
        var (service, provider) = CreateService();
        service.Show("npc", "g");
        int badId = service.Show("bad", "g");
        int goodId = service.Show("effect", "g");
        var faults = new List<(IEntityLogic Logic, Exception Exception)>();
        service.EntityFaulted += (logic, ex) => faults.Add((logic, ex));

        service.Update(0.016);

        Assert.Single(faults);
        Assert.Equal("bad", faults[0].Logic.Key);
        // 两个健康实体本帧都被更新。
        Assert.Contains("update:0.016", ((StubEntity)provider.Created[0]).Calls);
        Assert.Contains("update:0.016", ((StubEntity)provider.Created[2]).Calls);
        Assert.True(service.IsVisible(badId));
        Assert.True(service.IsVisible(goodId));
    }

    [Fact]
    public void HideGroup批量隐藏()
    {
        var (service, _) = CreateService();
        service.Show("npc", "g");
        service.Show("effect", "g");

        Assert.Equal(2, service.HideGroup("g"));
        Assert.Equal(0, service.VisibleEntityCount);
    }

    [Fact]
    public void 关闭时隐藏全部实体()
    {
        var (service, _) = CreateService();
        int a = service.Show("npc", "g");
        int b = service.Show("effect", "g");

        service.ShutdownAsync(CancellationToken.None);

        Assert.False(service.IsVisible(a));
        Assert.False(service.IsVisible(b));
    }

    [Fact]
    public void 组未注册容量为0()
    {
        var (service, _) = CreateService();
        Assert.Equal(0, service.GetGroupCapacity("无"));
        Assert.False(service.HasGroup("无"));
    }
}
