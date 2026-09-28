using System;
using System.Linq;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginHost.Hosting;
using Lyking.GameFramework.PluginHost.Tests.Samples;
using Lyking.GameFramework.PluginHost.VContainer;
using Xunit;

namespace Lyking.GameFramework.PluginHost.Tests
{
    /// <summary>宿主生命周期：启动顺序、健康、必需失败回滚、可选降级、Drain 与 Stop 语义。</summary>
    public class ModuleHostLifecycleTests
    {
        private static ModuleHost Build(IGameModule[] modules, TraceLog trace) =>
            ModuleHost.Build(
                modules,
                new VContainerModuleMaterializer(),
                new RecordingMiddleware(new TraceLog()));

        [Fact]
        public async Task Start_按计划顺序启动并进入Ready()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new EventLogModule(new EventLogSink(), trace),
                    new CounterModule(trace),
                    new ClockModule(trace: trace),
                },
                trace);

            await host.StartAsync();

            Assert.Equal(HostState.Ready, host.State);
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:game.counter",
                    "start:presentation.eventlog",
                },
                trace.Snapshot().ToArray());
            Assert.All(host.Health, h => Assert.Equal(ModuleHealthState.Ready, h.State));
            Assert.All(host.Health, h => Assert.True(h.StartElapsed is not null));
        }

        [Fact]
        public async Task Stop_按逆拓扑序停止并释放()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new ClockModule(trace: trace),
                    new CounterModule(trace),
                    new EventLogModule(new EventLogSink(), trace),
                },
                trace);

            await host.StartAsync();
            await host.StopAsync();

            Assert.Equal(HostState.Stopped, host.State);
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:game.counter",
                    "start:presentation.eventlog",
                    "stop:presentation.eventlog",
                    "stop:game.counter",
                    "stop:framework.clock",
                },
                trace.Snapshot().ToArray());
        }

        [Fact]
        public async Task 必需模块启动失败_逆序回滚并进入Failed()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new ClockModule(trace: trace),
                    new FailingModule("test.boom", isRequired: true, failPhase: "start", trace: trace),
                    new CounterModule(trace),
                },
                trace);

            var failure = await Assert.ThrowsAsync<ModuleHostException>(() => host.StartAsync().AsTask());

            Assert.Equal(HostState.Failed, host.State);
            Assert.Equal("test.boom", failure.ModuleId);
            Assert.True(failure.HadRollback);
            // 回滚应覆盖所有已启动模块（逆序）。
            Assert.Equal(new[] { "framework.clock" }, failure.Rollback);
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:test.boom",
                    "stop:framework.clock",
                },
                trace.Snapshot().ToArray());
            var clockHealth = Assert.Single(host.Health, h => h.ModuleId == "framework.clock");
            Assert.Equal(ModuleHealthState.Stopped, clockHealth.State);
        }

        [Fact]
        public async Task 可选模块启动失败_进入Degraded且核心继续()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new ClockModule(),
                    new FailingModule("test.optional", isRequired: false, failPhase: "start", trace: trace),
                    new CounterModule(),
                    new EventLogModule(new EventLogSink()),
                },
                trace);

            await host.StartAsync();

            Assert.Equal(HostState.Ready, host.State);
            var degraded = Assert.Single(host.Health, h => h.ModuleId == "test.optional");
            Assert.Equal(ModuleHealthState.Degraded, degraded.State);
            Assert.NotNull(degraded.Message);
            var counter = Assert.Single(host.Health, h => h.ModuleId == "game.counter");
            Assert.Equal(ModuleHealthState.Ready, counter.State);
        }

        [Fact]
        public async Task Drain_按依赖正序排空并拒绝新作用域()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new ClockModule(trace: trace),
                    new CounterModule(trace),
                    new EventLogModule(new EventLogSink(), trace),
                },
                trace);
            await host.StartAsync();

            await host.DrainAsync();

            Assert.Equal(HostState.Draining, host.State);
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:game.counter",
                    "start:presentation.eventlog",
                    "drain:framework.clock",
                    "drain:game.counter",
                    "drain:presentation.eventlog",
                },
                trace.Snapshot().ToArray());
            Assert.Throws<InvalidOperationException>(() => host.CreateScope("late"));
            // 重复 Drain 幂等。
            await host.DrainAsync();
        }

        [Fact]
        public async Task 单模块停止失败不阻断其余模块并汇总()
        {
            var trace = new TraceLog();
            var host = Build(
                new IGameModule[]
                {
                    new ClockModule(trace: trace),
                    new FailingModule("test.sticky", isRequired: true, failPhase: "stop", trace: trace),
                },
                trace);
            await host.StartAsync();

            var failure = await Assert.ThrowsAsync<ModuleHostException>(() => host.StopAsync().AsTask());

            Assert.Equal(HostState.Stopped, host.State);
            // sticky 失败后仍继续停止了 clock。
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:test.sticky",
                    "stop:test.sticky",
                    "stop:framework.clock",
                },
                trace.Snapshot().ToArray());
            Assert.Contains("1 个模块失败", failure.Message);
        }

        [Fact]
        public async Task 中间件钩子按生命周期顺序触发()
        {
            var middlewareTrace = new TraceLog();
            var host = ModuleHost.Build(
                new IGameModule[] { new ClockModule() },
                new VContainerModuleMaterializer(),
                new RecordingMiddleware(middlewareTrace));

            await host.StartAsync();
            await host.StopAsync();

            Assert.Equal(
                new[]
                {
                    "middleware:starting:framework.clock",
                    "middleware:started:framework.clock",
                    "middleware:stopped:framework.clock",
                },
                middlewareTrace.Snapshot().ToArray());
        }

        [Fact]
        public void 未启动的宿主拒绝解析与作用域()
        {
            var host = Build(new IGameModule[] { new ClockModule() }, new TraceLog());

            Assert.Throws<InvalidOperationException>(() => host.CreateScope("s"));
            Assert.Throws<InvalidOperationException>(() => host.Resolve<IGameClock>());
        }
    }
}
