using Lyking.GameFramework.Client.Core.Fsms;
using Lyking.GameFramework.Client.Core.Logging;
using Lyking.GameFramework.Client.Core.Modules;
using Lyking.GameFramework.Client.Core.Procedures;
using Lyking.GameFramework.Client.Core.ServiceLocation;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class FsmTests
{
    private class TraceState : FsmState
    {
        public TraceState(string tag) => Tag = tag;

        public string Tag { get; }

        public List<string> Calls { get; } = new();

        public double? EnterTimeInState { get; private set; }

        protected override void OnInit(Fsm fsm) => Calls.Add("init");

        protected override void OnEnter(Fsm fsm)
        {
            Calls.Add("enter");
            EnterTimeInState = fsm.TimeInState;
        }

        protected override void OnUpdate(Fsm fsm, double deltaTime) => Calls.Add($"update:{deltaTime}");

        protected override void OnExit(Fsm fsm) => Calls.Add("exit");

        protected override void OnDestroy(Fsm fsm) => Calls.Add("destroy");
    }

    private sealed class StateA : TraceState
    {
        public StateA() : base("A")
        {
        }
    }

    private sealed class StateB : TraceState
    {
        public StateB() : base("B")
        {
        }
    }

    [Fact]
    public void 启动进入初始状态并推进更新()
    {
        var a = new StateA();
        var b = new StateB();
        var fsm = new Fsm("测试", a, b);

        fsm.Start<StateA>();
        Assert.True(fsm.IsRunning);
        Assert.Equal("init", a.Calls[0]);
        Assert.Equal(new[] { "init", "enter" }, a.Calls);

        fsm.Update(0.5);
        Assert.Contains("update:0.5", a.Calls);
        Assert.Equal(0.5, fsm.TimeInState);
    }

    [Fact]
    public void 迁移先退出旧状态再进入新状态()
    {
        var a = new StateA();
        var b = new StateB();
        var fsm = new Fsm("测试", a, b);
        fsm.Start<StateA>();

        fsm.ChangeState<StateB>();

        Assert.True(a.Calls.SequenceEqual(new[] { "init", "enter", "exit" }));
        Assert.True(b.Calls.SequenceEqual(new[] { "init", "enter" }));
        Assert.True(fsm.IsInState<StateB>());
        Assert.Equal(0, fsm.TimeInState);
    }

    [Fact]
    public void 同状态重入触发完整退出与进入()
    {
        var a = new StateA();
        var fsm = new Fsm("测试", a);
        fsm.Start<StateA>();

        fsm.ChangeState<StateA>();

        Assert.Equal(new[] { "init", "enter", "exit", "enter" }, a.Calls);
    }

    [Fact]
    public void 未注册状态抛出()
    {
        var fsm = new Fsm("测试", new StateA());
        Assert.Throws<InvalidOperationException>(() => fsm.Start<StateB>());
    }

    [Fact]
    public void 未运行时迁移抛出()
    {
        var fsm = new Fsm("测试", new StateA());
        Assert.Throws<InvalidOperationException>(() => fsm.ChangeState<StateA>());
    }

    [Fact]
    public void 重复启动抛出()
    {
        var fsm = new Fsm("测试", new StateA());
        fsm.Start<StateA>();
        Assert.Throws<InvalidOperationException>(() => fsm.Start<StateA>());
    }

    [Fact]
    public void 关闭触发退出与全部OnDestroy且幂等()
    {
        var a = new StateA();
        var b = new StateB();
        var fsm = new Fsm("测试", a, b);
        fsm.Start<StateA>();
        fsm.ChangeState<StateB>();

        fsm.Shutdown();
        fsm.Shutdown();

        Assert.False(fsm.IsRunning);
        Assert.Equal(new[] { "init", "enter", "exit", "destroy" }, b.Calls);
        Assert.Equal(new[] { "init", "enter", "exit", "destroy" }, a.Calls);
    }

    [Fact]
    public void 状态机服务创建驱动与销毁()
    {
        var service = new FsmService();
        service.InitializeAsync(new ModuleEnvironment(new ServiceRegistry(), CreateLogger()));
        var a = new StateA();
        var fsm = service.CreateFsm("ai", a);
        service.CreateFsm("ui-flow", new StateB());

        Assert.Equal(2, service.FsmCount);
        Assert.Throws<ArgumentException>(() => service.CreateFsm("ai", new StateA()));

        service.StartFsm("ai", a.GetType());
        service.Update(0.1);

        Assert.Contains("update:0.1", a.Calls);
        Assert.True(service.DestroyFsm("ai"));
        Assert.False(service.DestroyFsm("ai"));
        Assert.Contains("destroy", a.Calls);
    }

    private static ILogger CreateLogger() => new Logger("test", new DelegateLogSink((_, _) => { }));
}

public sealed class ProcedureModuleTests
{
    private sealed class SplashProcedure : ProcedureState
    {
        protected override void OnUpdate(Fsm fsm, double deltaTime)
        {
            if (fsm.TimeInState >= 0.1)
            {
                fsm.ChangeState<LoadingProcedure>();
            }
        }
    }

    private sealed class LoadingProcedure : ProcedureState
    {
        protected override void OnUpdate(Fsm fsm, double deltaTime)
        {
            if (fsm.TimeInState >= 0.3)
            {
                fsm.ChangeState<MainProcedure>();
            }
        }
    }

    private sealed class MainProcedure : ProcedureState
    {
    }

    private static ModuleEnvironment CreateEnvironment() => new(
        new ServiceRegistry(),
        new Logger("test", new DelegateLogSink((_, _) => { })));

    [Fact]
    public async Task 流程按帧推进并自动切换()
    {
        var module = new ProcedureModule();
        module.AddProcedure(new SplashProcedure())
            .AddProcedure(new LoadingProcedure())
            .AddProcedure(new MainProcedure())
            .SetInitial<SplashProcedure>();
        await module.InitializeAsync(CreateEnvironment(), CancellationToken.None);

        module.Start();
        Assert.Equal(nameof(SplashProcedure), module.CurrentProcedureName);
        Assert.True(module.IsRunning);

        module.Update(0.2);
        Assert.Equal(nameof(LoadingProcedure), module.CurrentProcedureName);

        module.Update(0.2);
        Assert.Equal(nameof(LoadingProcedure), module.CurrentProcedureName);

        module.Update(0.2);
        Assert.Equal(nameof(MainProcedure), module.CurrentProcedureName);

        await module.ShutdownAsync(CancellationToken.None);
        Assert.False(module.IsRunning);
    }

    [Fact]
    public async Task 流程切换事件()
    {
        var module = new ProcedureModule();
        var transitions = new List<string>();
        module.ProcedureChanged += (from, to) => transitions.Add($"{from}->{to}");
        module.AddProcedure(new SplashProcedure())
            .AddProcedure(new LoadingProcedure())
            .AddProcedure(new MainProcedure())
            .SetInitial<SplashProcedure>();
        await module.InitializeAsync(CreateEnvironment(), CancellationToken.None);
        module.Start();
        module.Update(0.2);

        Assert.Equal(new[] { $"{nameof(SplashProcedure)}->{nameof(LoadingProcedure)}" }, transitions);
    }

    [Fact]
    public async Task 无初始流程时启动告警不抛()
    {
        var module = new ProcedureModule();
        module.AddProcedure(new SplashProcedure());
        await module.InitializeAsync(CreateEnvironment(), CancellationToken.None);

        module.Start();

        Assert.False(module.IsRunning);
    }
}
