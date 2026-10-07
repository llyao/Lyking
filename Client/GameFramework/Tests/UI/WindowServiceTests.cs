using Lyking.GameFramework.Client.UI;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.UI;

/// <summary>记录回调的窗口视图桩。</summary>
internal sealed class StubView : IWindowView
{
    public List<string> Calls { get; } = new();

    public void OnOpened() => Calls.Add("opened");

    public void OnFocus(bool focused) => Calls.Add(focused ? "focus" : "blur");

    public void OnPause(bool paused) => Calls.Add(paused ? "pause" : "resume");

    public void OnClosed() => Calls.Add("closed");
}

public sealed class WindowServiceTests
{
    private readonly UIWindowService _service = new();

    private static UIWindowDescriptor Descriptor(
        string name,
        UILayer layer = UILayer.Main,
        bool exclusive = false,
        bool modal = false,
        bool unique = true) => new(name, layer, exclusive, modal, unique);

    [Fact]
    public void 打开窗口生命周期与焦点()
    {
        var view = new StubView();

        var window = _service.Open(Descriptor("main"), view);

        Assert.NotNull(window);
        Assert.True(_service.IsOpen("main"));
        Assert.Same(window, _service.TopWindow);
        Assert.True(window.Focused);
        Assert.Equal(new[] { "opened", "focus" }, view.Calls);
        Assert.Equal(1, _service.OpenCount);
    }

    [Fact]
    public void 高层窗口抢焦点低层窗口失焦()
    {
        var mainView = new StubView();
        var popupView = new StubView();
        _service.Open(Descriptor("main", UILayer.Main), mainView);

        _service.Open(Descriptor("popup", UILayer.Popup), popupView);

        Assert.Equal(new[] { "opened", "focus" }, popupView.Calls);
        Assert.Equal(new[] { "opened", "focus", "blur" }, mainView.Calls);
        Assert.Same(popupView, _service.TopWindow!.View);
        // 打开列表：自高向低。
        Assert.Equal(new[] { "popup", "main" }, _service.OpenWindows.Select(w => w.Descriptor.Name));
    }

    [Fact]
    public void 关闭顶层窗口后低层窗口重获焦点()
    {
        var mainView = new StubView();
        var popupView = new StubView();
        _service.Open(Descriptor("main", UILayer.Main), mainView);
        _service.Open(Descriptor("popup", UILayer.Popup), popupView);

        Assert.True(_service.Close("popup"));

        Assert.False(_service.IsOpen("popup"));
        Assert.Equal(new[] { "opened", "focus", "closed" }, popupView.Calls);
        Assert.Equal(new[] { "opened", "focus", "blur", "focus" }, mainView.Calls);
        Assert.Same(mainView, _service.TopWindow!.View);
    }

    [Fact]
    public void 同层后打开者在栈顶()
    {
        var bottom = new StubView();
        var top = new StubView();
        _service.Open(Descriptor("bag", UILayer.Main), bottom);
        _service.Open(Descriptor("shop", UILayer.Main), top);

        Assert.Same(top, _service.TopWindow!.View);
        Assert.Equal(new[] { "shop", "bag" }, _service.OpenWindows.Select(w => w.Descriptor.Name));
    }

    [Fact]
    public void 唯一窗口重复打开只置顶不新建()
    {
        var mainView = new StubView();
        var shopView = new StubView();
        var unusedView = new StubView();
        var window1 = _service.Open(Descriptor("main", UILayer.Main, unique: true), mainView);
        _service.Open(Descriptor("shop", UILayer.Main), shopView);

        var window2 = _service.Open(Descriptor("main", UILayer.Main, unique: true), unusedView);

        Assert.Same(window1, window2);
        Assert.Equal(2, _service.OpenCount);
        Assert.Empty(unusedView.Calls);
        Assert.Same(window1, _service.TopWindow);
        Assert.Equal(new[] { "opened", "focus", "blur", "focus" }, mainView.Calls);
    }

    [Fact]
    public void 非唯一窗口可同名多开且逐个关闭()
    {
        var viewA = new StubView();
        var viewB = new StubView();
        var first = _service.Open(Descriptor("toast", UILayer.Toast, unique: false), viewA);
        var second = _service.Open(Descriptor("toast", UILayer.Toast, unique: false), viewB);

        Assert.Equal(2, _service.OpenCount);

        // 先关最顶的后开者，再关先开者。
        Assert.True(_service.Close("toast"));
        Assert.Same(first, _service.Find("toast"));
        Assert.True(_service.Close("toast"));
        Assert.False(_service.IsOpen("toast"));
        Assert.False(_service.Close("toast"));
    }

    [Fact]
    public void 独占窗口关闭全部低层窗口()
    {
        var mainView = new StubView();
        var bagView = new StubView();
        var toastView = new StubView();
        var loadingView = new StubView();
        _service.Open(Descriptor("main", UILayer.Main), mainView);
        _service.Open(Descriptor("bag", UILayer.Main), bagView);
        _service.Open(Descriptor("toast", UILayer.Toast), toastView);

        _service.Open(Descriptor("loading", UILayer.Loading, exclusive: true), loadingView);

        // 独占只关闭更低层：Main 层两个窗口被关，Toast（50 > 40）保留且保持焦点。
        Assert.False(_service.IsOpen("main"));
        Assert.False(_service.IsOpen("bag"));
        Assert.True(_service.IsOpen("toast"));
        Assert.True(_service.IsOpen("loading"));
        Assert.Equal(new[] { "opened", "focus", "blur", "closed" }, bagView.Calls);
        Assert.Equal(new[] { "opened", "focus" }, toastView.Calls);
        Assert.Equal(new[] { "opened" }, loadingView.Calls);
        Assert.Same(toastView, _service.TopWindow!.View);
    }

    [Fact]
    public void 全部关闭返回数量并清空焦点()
    {
        var a = new StubView();
        var b = new StubView();
        _service.Open(Descriptor("a"), a);
        _service.Open(Descriptor("b"), b);

        Assert.Equal(2, _service.CloseAll());

        Assert.Equal(0, _service.OpenCount);
        Assert.Null(_service.TopWindow);
        Assert.Equal(new[] { "opened", "focus", "closed" }, b.Calls);
    }

    [Fact]
    public void 打开与关闭事件触发()
    {
        var opened = new List<string>();
        var closed = new List<string>();
        _service.Opened += w => opened.Add(w.Descriptor.Name);
        _service.Closed += w => closed.Add(w.Descriptor.Name);

        _service.Open(Descriptor("main"), new StubView());
        _service.Close("main");

        Assert.Equal(new[] { "main" }, opened);
        Assert.Equal(new[] { "main" }, closed);
    }

    [Fact]
    public void 空窗口名抛出()
    {
        Assert.Throws<ArgumentException>(() => Descriptor(" "));
    }

    [Fact]
    public void 空视图或空描述抛出()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Open(null!, new StubView()));
        Assert.Throws<ArgumentNullException>(() => _service.Open(Descriptor("x"), null!));
    }
}
