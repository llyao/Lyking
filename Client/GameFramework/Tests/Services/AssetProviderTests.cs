using Lyking.GameFramework.Client.Services.Assets;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class AssetProviderTests
{
    private sealed class Widget
    {
        public string? Tag { get; set; }
    }

    [Fact]
    public async Task 默认按无参构造创建实例()
    {
        var provider = new SimulationAssetProvider();

        using var handle = await provider.LoadAsync<Widget>("ui/widget");

        Assert.IsType<Widget>(handle.Value);
        Assert.True(handle.IsLoaded);
        Assert.Equal(1, provider.GetActiveCount("ui/widget"));
    }

    [Fact]
    public async Task 工厂表优先于默认构造()
    {
        var widget = new Widget { Tag = "fixed" };
        var provider = new SimulationAssetProvider(new Dictionary<string, Func<object>>
        {
            ["ui/widget"] = () => widget,
        });

        using var handle = await provider.LoadAsync<Widget>("ui/widget");

        Assert.Same(widget, handle.Value);
    }

    [Fact]
    public async Task 同key多句柄引用计数且全部释放后归零()
    {
        var provider = new SimulationAssetProvider();
        var first = await provider.LoadAsync<Widget>("ui/widget");
        var second = await provider.LoadAsync<Widget>("ui/widget");

        Assert.Equal(2, provider.GetActiveCount("ui/widget"));
        Assert.NotSame(first, second);

        second.Dispose();
        Assert.Equal(1, provider.GetActiveCount("ui/widget"));

        first.Dispose();
        Assert.Equal(0, provider.GetActiveCount("ui/widget"));
        Assert.Equal(0, provider.ActiveKeyCount);
    }

    [Fact]
    public async Task 重复Dispose句柄幂等()
    {
        var provider = new SimulationAssetProvider();
        var handle = await provider.LoadAsync<Widget>("ui/widget");

        handle.Dispose();
        handle.Dispose();

        Assert.Equal(0, provider.GetActiveCount("ui/widget"));
    }

    [Fact]
    public async Task 类型不匹配抛出()
    {
        var provider = new SimulationAssetProvider(new Dictionary<string, Func<object>>
        {
            ["bad"] = () => "不是 Widget",
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadAsync<Widget>("bad").AsTask());
    }

    [Fact]
    public async Task 工厂抛出包装为加载失败()
    {
        var provider = new SimulationAssetProvider(new Dictionary<string, Func<object>>
        {
            ["boom"] = () => throw new InvalidOperationException("工厂内部错误"),
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadAsync<Widget>("boom").AsTask());
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task 空key抛出()
    {
        var provider = new SimulationAssetProvider();
        await Assert.ThrowsAsync<ArgumentException>(() => provider.LoadAsync<Widget>(" ").AsTask());
    }
}
