using Lyking.GameFramework.Client.UI.Binding;
using Lyking.GameFramework.Client.UI.Mvp;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.UI;

public sealed class BindablePropertyTests
{
    [Fact]
    public void 初始值与变更通知()
    {
        var property = new BindableProperty<int>(10);
        var changes = new List<(int Old, int New)>();
        property.Changed += (oldValue, newValue) => changes.Add((oldValue, newValue));

        property.Value = 20;

        Assert.Equal(20, property.Value);
        Assert.Equal(new[] { (10, 20) }, changes);
    }

    [Fact]
    public void 相等赋值不触发通知()
    {
        var property = new BindableProperty<string>("a");
        int notified = 0;
        property.Changed += (_, _) => notified++;

        property.Value = "a";

        Assert.Equal(0, notified);
    }

    [Fact]
    public void 静默写入不触发通知()
    {
        var property = new BindableProperty<int>(1);
        int notified = 0;
        property.Changed += (_, _) => notified++;

        property.SetSilently(5);

        Assert.Equal(5, property.Value);
        Assert.Equal(0, notified);
    }

    [Fact]
    public void Bind立即回调一次并随Dispose退订()
    {
        var property = new BindableProperty<int>(7);
        var seen = new List<int>();

        using (property.Bind((_, newValue) => seen.Add(newValue)))
        {
            property.Value = 8;
        }

        property.Value = 9;

        Assert.Equal(new[] { 7, 8 }, seen);
    }
}

public sealed class PresenterTests
{
    private sealed class FakeView
    {
    }

    private sealed class FakePresenter : Presenter<FakeView>
    {
        public List<string> Calls { get; } = new();

        protected override void OnViewAttached(FakeView view) => Calls.Add("attached");

        protected override void OnViewDetached(FakeView view) => Calls.Add("detached");
    }

    [Fact]
    public void 绑定与解绑周期()
    {
        var presenter = new FakePresenter();
        var view = new FakeView();

        presenter.Attach(view);
        Assert.True(presenter.IsAttached);
        Assert.Same(view, presenter.View);

        presenter.Detach();

        Assert.False(presenter.IsAttached);
        Assert.Null(presenter.View);
        Assert.Equal(new[] { "attached", "detached" }, presenter.Calls);
    }

    [Fact]
    public void 重复绑定抛出且解绑幂等()
    {
        var presenter = new FakePresenter();
        presenter.Attach(new FakeView());

        Assert.Throws<InvalidOperationException>(() => presenter.Attach(new FakeView()));

        presenter.Detach();
        presenter.Detach();
        Assert.Equal(new[] { "attached", "detached" }, presenter.Calls);
    }

    [Fact]
    public void 绑定null视图抛出()
    {
        var presenter = new FakePresenter();
        Assert.Throws<ArgumentNullException>(() => presenter.Attach(null!));
    }
}
