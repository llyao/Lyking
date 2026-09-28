using System;
using System.Linq;
using System.Threading;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using Lyking.GameFramework.PluginHost.Composition;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginHost.Tests.Samples;
using Xunit;

namespace Lyking.GameFramework.PluginHost.Tests
{
    /// <summary>组合与校验：依赖排序、环、缺失、版本、重复 Id、能力冲突、命名空间冲突、重复服务。</summary>
    public class CompositionPlanTests
    {
        private static IGameModule Stub(string id, ModuleDependency[]? deps = null, string? ns = null, string[]? caps = null) =>
            new DescriptorOnlyModule(new ModuleDescriptor(
                id,
                new ModuleVersion(1, 0),
                ModuleKind.HostAdapter,
                ns ?? $"test.{id}",
                capabilities: caps is null ? null : new ModuleCapabilities(caps),
                dependencies: deps));

        [Fact]
        public void Plan_按依赖拓扑排序且同层保持输入顺序()
        {
            var clock = new ClockModule();
            var counter = new CounterModule();
            var log = new EventLogModule(new EventLogSink());

            // 故意按依赖逆序注册，计划必须还原正确顺序。
            var plan = ModuleComposer.Compose(new IGameModule[] { log, counter, clock });

            Assert.Equal(
                new[] { "framework.clock", "game.counter", "presentation.eventlog" },
                plan.Modules.Select(m => m.Descriptor.Id.Value).ToArray());
        }

        [Fact]
        public void Plan_拒绝依赖环并给出链条()
        {
            var a = Stub("test.a", deps: new[] { new ModuleDependency("test.b", new ModuleVersion(1, 0)) });
            var b = Stub("test.b", deps: new[] { new ModuleDependency("test.a", new ModuleVersion(1, 0)) });

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { a, b }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.DependencyCycle);
        }

        [Fact]
        public void Plan_拒绝缺失依赖()
        {
            var lonely = Stub("test.lonely", deps: new[] { new ModuleDependency("test.ghost", new ModuleVersion(1, 0)) });

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { lonely }));

            var diagnostic = Assert.Single(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.MissingDependency);
            Assert.Equal("test.lonely", diagnostic.ModuleId);
        }

        [Fact]
        public void Plan_拒绝主版本不兼容的依赖()
        {
            var provider = new DescriptorOnlyModule(new ModuleDescriptor(
                "test.provider", new ModuleVersion(1, 0), ModuleKind.HostAdapter, "test.provider"));
            var consumer = Stub("test.consumer", deps: new[] { new ModuleDependency("test.provider", new ModuleVersion(2, 0)) });

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { provider, consumer }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.IncompatibleVersion);
        }

        [Fact]
        public void Plan_拒绝次版本低于要求的依赖()
        {
            var provider = new DescriptorOnlyModule(new ModuleDescriptor(
                "test.provider", new ModuleVersion(1, 0), ModuleKind.HostAdapter, "test.provider"));
            var consumer = Stub("test.consumer", deps: new[] { new ModuleDependency("test.provider", new ModuleVersion(1, 5)) });

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { provider, consumer }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.IncompatibleVersion);
        }

        [Fact]
        public void Plan_拒绝重复模块Id()
        {
            var first = Stub("test.dup");
            var second = Stub("test.dup");

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { first, second }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.DuplicateModuleId);
        }

        [Fact]
        public void Plan_拒绝重复能力()
        {
            var first = Stub("test.first", caps: new[] { "transport" });
            var second = Stub("test.second", caps: new[] { "transport" });

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { first, second }));

            var diagnostic = Assert.Single(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.DuplicateCapability);
            Assert.Contains("transport", diagnostic.Message);
        }

        [Fact]
        public void Plan_拒绝状态命名空间冲突()
        {
            var first = Stub("test.first", ns: "shared.state");
            var second = Stub("test.second", ns: "shared.state");

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { first, second }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.StateNamespaceConflict);
        }

        [Fact]
        public void Plan_拒绝跨模块重复服务贡献()
        {
            var sink = new EventLogSink();
            var eventLog = new EventLogModule(sink);                 // 贡献 IEventSink
            var another = new SinkContributorModule(sink);           // 再次贡献 IEventSink

            var failure = Assert.Throws<ModuleCompositionException>(
                () => ModuleComposer.Compose(new IGameModule[] { eventLog, another }));

            Assert.Contains(failure.Diagnostics, d => d.Code == PlanDiagnosticCode.DuplicateServiceRegistration);
        }

        private sealed class SinkContributorModule : IGameModule
        {
            private readonly EventLogSink _sink;

            public SinkContributorModule(EventLogSink sink) => _sink = sink;

            public ModuleDescriptor Describe() => new ModuleDescriptor(
                "test.sink",
                new ModuleVersion(1, 0),
                ModuleKind.PresentationAdapter,
                "test.sink");

            public void Contribute(IModuleRegistrar registrar) =>
                registrar.RegisterInstance<IEventSink>(_sink);

            public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        }
    }
}
