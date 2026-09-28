using System;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using VContainer;

namespace Lyking.GameFramework.PluginHost.VContainer
{
    /// <summary>
    /// VContainer 物化器：把已校验计划的贡献翻译成容器注册并构建根容器。
    /// 生命周期映射：Singleton→Singleton、Scoped→Scoped、Transient→Transient。
    /// 不使用 VContainer 入口点（IInitializable/IStartable 等）；生命周期由 ModuleHost 拥有。
    /// 只使用与 UPM 包共同存在的最小核心 API 子集，见 design.md 3.9 节。
    /// </summary>
    public sealed class VContainerModuleMaterializer : IModuleContainerMaterializer
    {
        public IModuleContainer Apply(CompositionPlan plan)
        {
            if (plan is null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var builder = new ContainerBuilder();
            foreach (var contribution in plan.Contributions.All)
            {
                if (contribution.Instance is not null)
                {
                    builder.RegisterInstance(contribution.Instance).As(contribution.ServiceType);
                    continue;
                }

                var lifetime = contribution.Lifetime switch
                {
                    ModuleLifetime.Singleton => Lifetime.Singleton,
                    ModuleLifetime.Scoped => Lifetime.Scoped,
                    ModuleLifetime.Transient => Lifetime.Transient,
                    _ => throw new InvalidOperationException(
                        $"未知生命周期 {contribution.Lifetime}（服务 {contribution.ServiceType.FullName}）。"),
                };

                builder.Register(contribution.ImplementationType, lifetime).As(contribution.ServiceType);
            }

            return new VContainerModuleContainer(builder.Build());
        }
    }
}
