using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class ServiceRegistryTests
{
    private interface IGreet
    {
        string Hello();
    }

    private sealed class ZhGreet : IGreet
    {
        public string Hello() => "你好";
    }

    private sealed class EnGreet : IGreet
    {
        public string Hello() => "hello";
    }

    [Fact]
    public void 注册后可解析()
    {
        var registry = new ServiceRegistry();
        registry.Register<IGreet>(new ZhGreet());

        var resolved = registry.Resolve<IGreet>();

        Assert.IsType<ZhGreet>(resolved);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void 具名注册互不影响()
    {
        var registry = new ServiceRegistry();
        var zh = new ZhGreet();
        var en = new EnGreet();
        registry.Register<IGreet>("zh", zh);
        registry.Register<IGreet>("en", en);

        Assert.Same(zh, registry.Resolve<IGreet>("zh"));
        Assert.Same(en, registry.Resolve<IGreet>("en"));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void 重复注册抛出()
    {
        var registry = new ServiceRegistry();
        registry.Register<IGreet>(new ZhGreet());

        Assert.Throws<InvalidOperationException>(() => registry.Register<IGreet>(new ZhGreet()));
    }

    [Fact]
    public void 未注册解析抛出且TryResolve返回false()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve<IGreet>());
        Assert.False(registry.TryResolve<IGreet>(out _));
    }

    [Fact]
    public void 实例类型不匹配注册抛出()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(typeof(IGreet), new object()));
    }

    [Fact]
    public void 移除与清空()
    {
        var registry = new ServiceRegistry();
        registry.Register<IGreet>(new ZhGreet());
        registry.Register("en", new EnGreet());

        Assert.True(registry.Unregister(typeof(IGreet)));
        Assert.False(registry.Unregister(typeof(IGreet)));
        registry.Clear();

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryResolve<IGreet>("en", out _));
    }
}
