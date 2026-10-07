using Lyking.GameFramework.Client.UI;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.UI;

public sealed class ModalWindowTests
{
    private readonly UIWindowService _service = new();

    [Fact]
    public void 模态窗口暂停全部低层窗口()
    {
        var main = new StubView();
        var bag = new StubView();
        var dialog = new StubView();
        _service.Open(new UIWindowDescriptor("main", UILayer.Main), main);
        _service.Open(new UIWindowDescriptor("bag", UILayer.Main), bag);

        _service.Open(new UIWindowDescriptor("对话框", UILayer.Popup, modal: true), dialog);

        // 低层窗口被暂停；模态窗口自己与更高层不受影响。
        Assert.Equal(new[] { "opened", "focus", "blur", "pause" }, main.Calls);
        Assert.Equal(new[] { "opened", "focus", "blur", "pause" }, bag.Calls);
        Assert.Equal(new[] { "opened", "focus" }, dialog.Calls);
        Assert.True(_service.Find("main")!.Paused);
    }

    [Fact]
    public void 关闭模态后低层窗口恢复()
    {
        var main = new StubView();
        _service.Open(new UIWindowDescriptor("main", UILayer.Main), main);
        _service.Open(new UIWindowDescriptor("对话框", UILayer.Popup, modal: true), new StubView());

        _service.Close("对话框");

        Assert.Equal(new[] { "opened", "focus", "blur", "pause", "focus", "resume" }, main.Calls);
        Assert.False(_service.Find("main")!.Paused);
    }

    [Fact]
    public void 同层模态不暂停同层窗口()
    {
        var bottom = new StubView();
        var top = new StubView();
        _service.Open(new UIWindowDescriptor("a", UILayer.Main), bottom);
        _service.Open(new UIWindowDescriptor("b", UILayer.Main, modal: true), top);

        Assert.False(_service.Find("a")!.Paused);
        Assert.Equal(new[] { "opened", "focus", "blur" }, bottom.Calls);
    }

    [Fact]
    public void 非模态高层窗口不触发暂停()
    {
        var dialog = new StubView();
        var toast = new StubView();
        _service.Open(new UIWindowDescriptor("对话框", UILayer.Popup, modal: true), dialog);

        _service.Open(new UIWindowDescriptor("toast", UILayer.Toast), toast);

        // 暂停只由更高层的模态窗口引起；Toast 不是模态，对话框仅失去焦点。
        Assert.False(_service.Find("toast")!.Paused);
        Assert.False(_service.Find("对话框")!.Paused);
        Assert.Equal(new[] { "opened", "focus", "blur" }, dialog.Calls);
        Assert.Equal(new[] { "opened", "focus" }, toast.Calls);
    }

    [Fact]
    public void 暂停事件触发()
    {
        var events = new List<(string Name, bool Paused)>();
        _service.PauseChanged += (window, paused) => events.Add((window.Descriptor.Name, paused));
        _service.Open(new UIWindowDescriptor("main", UILayer.Main), new StubView());

        _service.Open(new UIWindowDescriptor("对话框", UILayer.Popup, modal: true), new StubView());

        Assert.Equal(new[] { ("main", true) }, events);
    }
}

public sealed class UIFormLogicTests
{
    private sealed class SampleForm : UIFormLogic
    {
        public List<string> Calls { get; } = new();

        public object? OpenData { get; private set; }

        public string? WindowName { get; private set; }

        /// <summary>测试探针：窗口绑定时返回窗口记录，未绑定/已关闭返回 null。</summary>
        public UIWindow? PeekWindow()
        {
            try
            {
                return Window;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        protected override void OnInit(UIWindow window)
        {
            WindowName = window.Descriptor.Name;
            Calls.Add("init");
        }

        protected override void OnOpen(object? userData)
        {
            OpenData = userData;
            Calls.Add("open");
        }

        protected override void OnPause(bool paused) => Calls.Add(paused ? "pause" : "resume");

        protected override void OnFocus(bool focused) => Calls.Add(focused ? "focus" : "blur");

        protected override void OnClose() => Calls.Add("close");
    }

    private readonly UIWindowService _service = new();

    [Fact]
    public void 打开时绑定窗口与userData()
    {
        var form = new SampleForm();

        _service.Open(new UIWindowDescriptor("商店", UILayer.Main), form, userData: "从主城打开");

        Assert.Equal(new[] { "init", "open", "focus" }, form.Calls);
        Assert.Equal("商店", form.WindowName);
        Assert.Equal("从主城打开", form.OpenData);
        Assert.Same(form, _service.Find("商店")!.View);
    }

    [Fact]
    public void 模态与关闭回调()
    {
        var form = new SampleForm();
        _service.Open(new UIWindowDescriptor("商店", UILayer.Main), form);

        _service.Open(new UIWindowDescriptor("确认框", UILayer.Popup, modal: true), new StubView());
        _service.Close("确认框");
        _service.Close("商店");

        Assert.Equal(new[] { "init", "open", "focus", "blur", "pause", "focus", "resume", "close" }, form.Calls);
    }

    [Fact]
    public void 关闭后窗口引用失效()
    {
        var form = new SampleForm();
        _service.Open(new UIWindowDescriptor("商店", UILayer.Main), form);
        Assert.NotNull(form.PeekWindow());

        _service.Close("商店");

        Assert.Null(form.PeekWindow());
    }
}
