using Lyking.GameFramework.Client.Core.DataNode;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class DataNodeTests
{
    [Fact]
    public void 路径自动建链与读写()
    {
        var service = new DataNodeService();
        service.Set("Battle.CurrentWave", 3);

        Assert.Equal(3, service.Get<int>("Battle.CurrentWave"));
        Assert.Equal("Battle.CurrentWave", service.GetNode("Battle.CurrentWave")!.FullName);
        Assert.Equal(2, service.GetNode("Battle.CurrentWave")!.Depth);
    }

    [Fact]
    public void 缺失路径不建链时返回null且Get抛出()
    {
        var service = new DataNodeService();

        Assert.Null(service.GetNode("Nope.Child"));
        Assert.Throws<InvalidOperationException>(() => service.Get<int>("Nope.Child"));
    }

    [Fact]
    public void 类型不匹配抛出且TryGet返回false()
    {
        var service = new DataNodeService();
        service.Set("a", "文本");

        Assert.Throws<InvalidOperationException>(() => service.Get<int>("a"));
        Assert.False(service.TryGet<int>("a", out _));
        Assert.True(service.TryGet<string>("a", out string? text));
        Assert.Equal("文本", text);
    }

    [Fact]
    public void 无数据结点读取抛出()
    {
        var service = new DataNodeService();
        service.GetNode("empty", create: true);

        Assert.Throws<InvalidOperationException>(() => service.Get<int>("empty"));
    }

    [Fact]
    public void 移除结点()
    {
        var service = new DataNodeService();
        service.Set("a.b", 1);
        service.Set("a.c", 2);

        Assert.True(service.Remove("a.b"));
        Assert.Null(service.GetNode("a.b"));
        Assert.False(service.Remove("a.b"));
        Assert.Equal(2, service.Get<int>("a.c"));
    }

    [Fact]
    public void 覆盖写入与子结点遍历()
    {
        var service = new DataNodeService();
        service.Set("player", 1);
        service.Set("player", "重写");
        service.Set("player.hp", 100);

        var node = service.GetNode("player")!;
        Assert.Equal(1, node.ChildCount);
        Assert.Equal("player.hp", node.Children[0].FullName);
    }

    [Fact]
    public void 清空数据树()
    {
        var service = new DataNodeService();
        service.Set("a.b", 1);
        service.ClearAll();

        Assert.Null(service.GetNode("a.b"));
    }

    [Fact]
    public void 空路径抛出()
    {
        var service = new DataNodeService();
        Assert.Throws<ArgumentException>(() => service.Set(" ", 1));
    }
}
