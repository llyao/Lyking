using Lyking.GameFramework.Client.Core.Pooling;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class ReferencePoolTests
{
    private sealed class Payload : IReference
    {
        public int Value { get; set; }

        public string? Tag { get; set; }

        public bool Cleared { get; private set; }

        public void Clear()
        {
            Value = 0;
            Tag = null;
            Cleared = true;
        }
    }

    [Fact]
    public void 首次租用创建之后复用()
    {
        var pool = new ReferencePool();
        var first = pool.Rent<Payload>();
        first.Value = 42;
        pool.Return(first);

        var second = pool.Rent<Payload>();

        Assert.Same(first, second);
        // 归还时已执行 Clear，脏状态不跨租用泄漏。
        Assert.Equal(0, second.Value);
        Assert.True(second.Cleared);
        Assert.Equal(0, pool.PooledCount<Payload>());
    }

    [Fact]
    public void 重复归还抛出()
    {
        var pool = new ReferencePool();
        var reference = pool.Rent<Payload>();
        pool.Return(reference);

        Assert.Throws<InvalidOperationException>(() => pool.Return(reference));
    }

    [Fact]
    public void 归还null抛出()
    {
        var pool = new ReferencePool();
        Assert.Throws<ArgumentNullException>(() => pool.Return<Payload>(null!));
    }

    [Fact]
    public void 统计计数()
    {
        var pool = new ReferencePool();
        var a = pool.Rent<Payload>();
        var b = pool.Rent<Payload>();
        pool.Return(a);
        pool.Return(b);

        Assert.Equal(2, pool.TotalRentCount);
        Assert.Equal(2, pool.TotalReturnCount);
        Assert.Equal(2, pool.PooledCount<Payload>());
    }

    [Fact]
    public void 清空缓存()
    {
        var pool = new ReferencePool();
        var reference = pool.Rent<Payload>();
        pool.Return(reference);
        pool.Clear();

        Assert.Equal(0, pool.PooledCount<Payload>());
    }
}

public sealed class ListPoolTests
{
    [Fact]
    public void 租借归还后复用且内容被清空()
    {
        var first = ListPool<int>.Rent();
        first.Add(1);
        first.Add(2);
        ListPool<int>.Return(first);

        var second = ListPool<int>.Rent();

        Assert.Same(first, second);
        Assert.Empty(second);
    }

    [Fact]
    public void 重复归还抛出()
    {
        var list = ListPool<int>.Rent();
        ListPool<int>.Return(list);

        Assert.Throws<InvalidOperationException>(() => ListPool<int>.Return(list));
        ListPool<int>.Return(ListPool<int>.Rent()); // 清理，避免影响其他用例（同一线程内单例池）
        ListPool<int>.Clear();
    }
}
