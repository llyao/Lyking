using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Lyking.GameFramework.Client.Services.DataTable;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

/// <summary>测试用数据行：id、名称、价格三列。</summary>
public sealed class ItemRow : IDataRow
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

public sealed class DataTableServiceTests
{
    private const string ItemTableText =
        "id\tname\tprice\n" +
        "1\t长剑\t100\n" +
        "2\t盾牌\t80\n" +
        "3\t药水\t30\n";

    private static DataTableService CreateService()
    {
        var service = new DataTableService();
        service.InitializeAsync(
            new ModuleEnvironment(new ServiceRegistry(), new Logger("t", new DelegateLogSink((_, _) => { }))));
        return service;
    }

    [Fact]
    public void 加载数据表并按id取行()
    {
        var service = CreateService();
        var table = service.LoadFromText<ItemRow>("item", ItemTableText);

        Assert.Equal(3, table.Count);
        Assert.Equal(0, table.SkippedRowCount);
        Assert.Equal("长剑", table.GetRow(1).Name);
        Assert.Equal(80, table.GetRow(2).Price);
        Assert.True(table.HasRow(3));
        Assert.False(table.HasRow(99));
        Assert.Throws<KeyNotFoundException>(() => table.GetRow(99));
    }

    [Fact]
    public void 条件查询与TryGetTable()
    {
        var service = CreateService();
        service.LoadFromText<ItemRow>("item", ItemTableText);

        var cheap = service.GetTable<ItemRow>("item").Where(r => r.Price < 50).ToList();
        Assert.Equal(new[] { 3 }, cheap.Select(r => r.Id));

        Assert.True(service.TryGetTable<ItemRow>("item", out _));
        Assert.False(service.TryGetTable<ItemRow>("missing", out _));
        // 表存在但类型不符时同样返回 false。
        Assert.False(service.TryGetTable<EntityStubRow>("item", out _));
    }

    [Fact]
    public void 严格模式下坏行抛出()
    {
        var service = CreateService();
        const string badText = "id\tname\tprice\n1\t长剑\t100\nX\t坏行\t不存在的价格\n";

        var ex = Assert.Throws<DataTableParseException>(
            () => service.LoadFromText<ItemRow>("item", badText));
        Assert.Equal(2, ex.LineIndex);
    }

    [Fact]
    public void 宽松模式跳过坏行并计数()
    {
        var service = CreateService();
        const string text = "id\tname\tprice\n1\t长剑\t100\nX\t坏行\tNaN\n3\t药水\t30\n";

        var table = service.LoadFromText<ItemRow>("item", text, strict: false);

        Assert.Equal(2, table.Count);
        Assert.Equal(1, table.SkippedRowCount);
    }

    [Fact]
    public void 严格模式下重复id抛出()
    {
        var service = CreateService();
        const string text = "id\tname\tprice\n1\tA\t1\n1\tB\t2\n";

        Assert.Throws<DataTableParseException>(() => service.LoadFromText<ItemRow>("item", text));
    }

    [Fact]
    public void 重复表名抛出需先移除()
    {
        var service = CreateService();
        service.LoadFromText<ItemRow>("item", ItemTableText);

        Assert.Throws<InvalidOperationException>(() => service.LoadFromText<ItemRow>("item", ItemTableText));

        Assert.True(service.RemoveTable("item"));
        service.LoadFromText<ItemRow>("item", ItemTableText);
        Assert.Equal(1, service.TableCount);
    }

    [Fact]
    public void 无表头加载()
    {
        var service = CreateService();

        var table = service.LoadFromText<ItemRow>("item", "1\t长剑\t100\n2\t盾牌\t80\n", skipFirstLine: false);

        Assert.Equal(2, table.Count);
    }

    private sealed class EntityStubRow : IDataRow
    {
        public int Id => 0;

        public bool ParseRow(IReadOnlyList<string> cells) => true;
    }
}
