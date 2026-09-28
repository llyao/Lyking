using System;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using Lyking.GameFramework.PluginHost.Hosting;
using Lyking.GameFramework.PluginHost.Tests.Samples;
using Lyking.GameFramework.PluginHost.VContainer;
using Xunit;

namespace Lyking.GameFramework.PluginHost.Tests
{
    /// <summary>作用域语义：Singleton 根共享、Scoped 按作用域隔离、Transient 每次新建、作用域释放回收。</summary>
    public class ScopeTests
    {
        public interface ISessionBadge
        {
            bool IsDisposed { get; }
        }

        public sealed class SessionBadge : ISessionBadge, IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        public sealed class TransientProbe
        {
        }

        private sealed class BadgeModule : IGameModule
        {
            public ModuleDescriptor Describe() => new ModuleDescriptor(
                "test.badge",
                new ModuleVersion(1, 0),
                ModuleKind.HostAdapter,
                "test.badge",
                capabilities: new ModuleCapabilities("session"));

            public void Contribute(IModuleRegistrar registrar)
            {
                registrar.Register<ISessionBadge, SessionBadge>(ModuleLifetime.Scoped);
                registrar.RegisterInstance<IGameClock>(new ManualClock());
                registrar.Register<TransientProbe, TransientProbe>(ModuleLifetime.Transient);
            }

            public ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        }

        [Fact]
        public async Task 作用域内Scoped独立_Singleton共享_Transient新建()
        {
            var host = ModuleHost.Build(new IGameModule[] { new BadgeModule() }, new VContainerModuleMaterializer());
            await host.StartAsync();

            await using var scopeA = host.CreateScope("session-a");
            await using var scopeB = host.CreateScope("session-b");

            var badgeA1 = (ISessionBadge)scopeA.Resolve(typeof(ISessionBadge));
            var badgeA2 = (ISessionBadge)scopeA.Resolve(typeof(ISessionBadge));
            var badgeB = (ISessionBadge)scopeB.Resolve(typeof(ISessionBadge));

            Assert.NotSame(badgeA1, badgeB);
            Assert.Same(badgeA1, badgeA2);

            var clockA = scopeA.Resolve(typeof(IGameClock));
            var clockB = scopeB.Resolve(typeof(IGameClock));
            Assert.Same(clockA, clockB);

            var transient1 = scopeA.Resolve(typeof(TransientProbe));
            var transient2 = scopeA.Resolve(typeof(TransientProbe));
            Assert.NotSame(transient1, transient2);

            await scopeA.DisposeAsync();
            Assert.True(((SessionBadge)badgeA1).IsDisposed);
            Assert.False(((SessionBadge)badgeB).IsDisposed);
        }

        [Fact]
        public async Task 宿主根解析与作用域解析一致地共享单例()
        {
            var host = ModuleHost.Build(new IGameModule[] { new BadgeModule() }, new VContainerModuleMaterializer());
            await host.StartAsync();

            var rootClock = host.Resolve<IGameClock>();
            await using var scope = host.CreateScope("session");
            Assert.Same(rootClock, scope.Resolve(typeof(IGameClock)));
        }

        [Fact]
        public async Task 必需服务缺失时解析抛出结构化异常()
        {
            var host = ModuleHost.Build(new IGameModule[] { new BadgeModule() }, new VContainerModuleMaterializer());
            await host.StartAsync();

            var failure = Assert.Throws<InvalidOperationException>(() => host.Resolve<IEventSink>());
            Assert.Contains("IEventSink", failure.Message);
        }
    }
}
