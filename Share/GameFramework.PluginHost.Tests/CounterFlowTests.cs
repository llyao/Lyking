using System;
using Lyking.GameFramework.PluginAbstractions;
using System.Linq;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginHost.Hosting;
using Lyking.GameFramework.PluginHost.Tests.Samples;
using Lyking.GameFramework.PluginHost.VContainer;
using Xunit;

namespace Lyking.GameFramework.PluginHost.Tests
{
    /// <summary>
    /// 端到端全流程：Plan → Validate → Apply(VContainer) → Start → 命令/事件 → Scope → Drain → Stop。
    /// 对应 design.md 第 3.9 节的本切片验收。
    /// </summary>
    public class CounterFlowTests
    {
        private sealed record FlowFixture(ModuleHost Host, TraceLog Trace, EventLogSink Sink, ManualClock Clock);

        private static FlowFixture Create() 
        {
            var trace = new TraceLog();
            var sink = new EventLogSink();
            var clock = new ManualClock();
            var host = ModuleHost.Build(
                new IGameModule[]
                {
                    new EventLogModule(sink, trace),
                    new CounterModule(trace),
                    new ClockModule(clock, trace),
                },
                new VContainerModuleMaterializer());
            return new FlowFixture(host, trace, sink, clock);
        }

        [Fact]
        public async Task 全流程_命令FIFO推进_事件落盘_Drain拒绝_Stop逆序()
        {
            var fixture = Create();
            var host = fixture.Host;

            // Plan 阶段：构建即完成组合与校验，物化进 VContainer。
            Assert.Equal(
                new[] { "framework.clock", "game.counter", "presentation.eventlog" },
                host.Plan.Modules.Select(m => m.Descriptor.Id.Value).ToArray());

            await host.StartAsync();
            Assert.Equal(HostState.Ready, host.State);

            var store = host.Resolve<ICounterStore>();

            // 提交命令（意图）→ Step 推进（FIFO）→ 事实事件按序发布。
            store.Submit(2);
            store.Submit(-1);
            store.Submit(5);
            fixture.Clock.Tick = 7;
            store.Step();

            Assert.Equal(6, store.Value);
            var facts = fixture.Sink.Facts.Cast<CounterChanged>().ToArray();
            Assert.Equal(3, facts.Length);
            Assert.Equal(2, facts[0].Amount);
            Assert.Equal(2, facts[0].NewValue);
            Assert.Equal(-1, facts[1].Amount);
            Assert.Equal(1, facts[1].NewValue);
            Assert.Equal(5, facts[2].Amount);
            Assert.Equal(6, facts[2].NewValue);
            Assert.All(facts, f => Assert.Equal(7, f.Tick));

            // 命名作用域共享同一个模拟单例（会话读权威状态）。
            await using var session = host.CreateScope("session-1");
            Assert.Same(store, session.Resolve(typeof(ICounterStore)));

            // Drain：拒绝新输入，在途命令已收敛。
            await host.DrainAsync();
            Assert.Throws<InvalidOperationException>(() => store.Submit(1));
            store.Step();
            Assert.Equal(6, store.Value);

            // Stop：逆拓扑序，事实保留。
            await host.StopAsync();
            Assert.Equal(HostState.Stopped, host.State);
            Assert.Equal(
                new[]
                {
                    "start:framework.clock",
                    "start:game.counter",
                    "start:presentation.eventlog",
                    "drain:framework.clock",
                    "drain:game.counter",
                    "drain:presentation.eventlog",
                    "stop:presentation.eventlog",
                    "stop:game.counter",
                    "stop:framework.clock",
                },
                fixture.Trace.Snapshot().ToArray());

            var counterHealth = Assert.Single(host.Health, h => h.ModuleId == "game.counter");
            Assert.Equal(ModuleHealthState.Stopped, counterHealth.State);
        }

        [Fact]
        public async Task 提交在先_Step为空时无事件()
        {
            var fixture = Create();
            await fixture.Host.StartAsync();
            var store = fixture.Host.Resolve<ICounterStore>();

            store.Step();

            Assert.Empty(fixture.Sink.Facts);
            Assert.Equal(0, store.Value);
        }
    }
}
