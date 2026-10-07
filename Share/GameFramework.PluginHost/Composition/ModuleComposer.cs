using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.PluginAbstractions;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using Lyking.GameFramework.PluginAbstractions.Validation;

namespace Lyking.GameFramework.PluginHost.Composition
{
    /// <summary>组合产物：计划与按拓扑序对齐的模块实例（Plan.Modules[i] ↔ Modules[i]）。</summary>
    public sealed class ModuleComposition
    {
        public ModuleComposition(CompositionPlan plan, IReadOnlyList<IGameModule> modules)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
        }

        public CompositionPlan Plan { get; }

        public IReadOnlyList<IGameModule> Modules { get; }
    }

    /// <summary>
    /// 组合器：Describe → Contribute → Validate → 拓扑排序。
    /// 校验失败时一次性抛出全部诊断；排序稳定，同层模块按注册顺序执行。
    /// </summary>
    public static class ModuleComposer
    {
        /// <summary>
        /// 组合并返回计划。注意：返回值不携带与模块实例的对齐关系；
        /// 宿主构建与任何需要"计划项 ↔ 实例"对应关系的场景必须使用 <see cref="ComposeWithModules"/>。
        /// </summary>
        public static CompositionPlan Compose(
            IEnumerable<IGameModule> modules,
            ICompositionValidator? validator = null)
        {
            return ComposeWithModules(modules, validator).Plan;
        }

        public static ModuleComposition ComposeWithModules(
            IEnumerable<IGameModule> modules,
            ICompositionValidator? validator = null)
        {
            if (modules is null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            validator ??= new CompositionValidator();

            var orderedModules = modules.ToList();
            var descriptors = new List<ModuleDescriptor>(orderedModules.Count);
            var diagnostics = new List<PlanDiagnostic>();

            foreach (var module in orderedModules)
            {
                try
                {
                    descriptors.Add(module.Describe());
                }
                catch (Exception failure)
                {
                    descriptors.Add(new ModuleDescriptor(
                        $"invalid.{descriptors.Count}",
                        new ModuleVersion(0, 0),
                        ModuleKind.ToolPlugin,
                        string.Empty,
                        description: $"Describe 失败：{failure.Message}"));
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.InvalidDescriptor,
                        $"模块 Describe 失败：{failure.Message}"));
                }
            }

            var contributions = new ServiceContributionSet();
            for (var index = 0; index < orderedModules.Count; index++)
            {
                var module = orderedModules[index];
                var descriptor = descriptors[index];
                if (descriptor.Id.Value.StartsWith("invalid.", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    module.Contribute(new RegistrarAdapter(descriptor.Id, contributions));
                }
                catch (Exception failure)
                {
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.InvalidDescriptor,
                        $"模块 {descriptor.Id} Contribute 失败：{failure.Message}",
                        descriptor.Id.Value));
                }
            }

            var validationResult = validator.Validate(descriptors, contributions);
            diagnostics.AddRange(validationResult.Diagnostics);
            if (diagnostics.Count > 0)
            {
                throw new ModuleCompositionException(diagnostics);
            }

            var sorted = Sort(descriptors);
            var instanceById = new Dictionary<string, IGameModule>(StringComparer.Ordinal);
            for (var index = 0; index < orderedModules.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor.Id.Value.StartsWith("invalid.", StringComparison.Ordinal))
                {
                    continue;
                }

                instanceById[descriptor.Id.Value] = orderedModules[index];
            }

            var alignedModules = new List<IGameModule>(sorted.Count);
            foreach (var planned in sorted)
            {
                alignedModules.Add(instanceById[planned.Descriptor.Id.Value]);
            }

            return new ModuleComposition(new CompositionPlan(sorted, contributions), alignedModules);
        }

        private static IReadOnlyList<PlannedModule> Sort(IReadOnlyList<ModuleDescriptor> descriptors)
        {
            var byId = descriptors.ToDictionary(d => d.Id.Value, d => d, StringComparer.Ordinal);
            var pending = new HashSet<string>(byId.Keys, StringComparer.Ordinal);
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var descriptor in descriptors)
            {
                var count = 0;
                foreach (var dependency in descriptor.Dependencies)
                {
                    if (pending.Contains(dependency.Id.Value))
                    {
                        count++;

                        if (!dependents.TryGetValue(dependency.Id.Value, out var list))
                        {
                            list = new List<string>();
                            dependents[dependency.Id.Value] = list;
                        }

                        list.Add(descriptor.Id.Value);
                    }
                }

                inDegree[descriptor.Id.Value] = count;
            }

            var queue = new SortedSet<int>();
            var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < descriptors.Count; index++)
            {
                indexOf[descriptors[index].Id.Value] = index;
            }

            foreach (var descriptor in descriptors)
            {
                if (inDegree[descriptor.Id.Value] == 0)
                {
                    queue.Add(indexOf[descriptor.Id.Value]);
                }
            }

            var sorted = new List<PlannedModule>(descriptors.Count);
            while (queue.Count > 0)
            {
                var index = queue.Min;
                queue.Remove(index);
                var descriptor = descriptors[index];
                sorted.Add(new PlannedModule(descriptor, sorted.Count));

                if (!dependents.TryGetValue(descriptor.Id.Value, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                    {
                        queue.Add(indexOf[child]);
                    }
                }
            }

            if (sorted.Count != descriptors.Count)
            {
                var remaining = string.Join(", ", descriptors
                    .Where(d => sorted.All(p => p.Descriptor.Id != d.Id))
                    .Select(d => d.Id.Value));
                throw new ModuleCompositionException(new[]
                {
                    new PlanDiagnostic(
                        PlanDiagnosticCode.DependencyCycle,
                        $"拓扑排序后仍有未排入模块（应为依赖环）：{remaining}。"),
                });
            }

            return sorted;
        }

        /// <summary>把贡献写入共享集合，并记录贡献来源。</summary>
        private sealed class RegistrarAdapter : IModuleRegistrar
        {
            private readonly ModuleId _moduleId;
            private readonly ServiceContributionSet _contributions;

            public RegistrarAdapter(ModuleId moduleId, ServiceContributionSet contributions)
            {
                _moduleId = moduleId;
                _contributions = contributions;
            }

            public void Register(Type serviceType, Type implementationType, ModuleLifetime lifetime)
            {
                if (!serviceType.IsAssignableFrom(implementationType))
                {
                    throw new ArgumentException(
                        $"{implementationType.FullName} 不能赋给服务类型 {serviceType.FullName}。");
                }

                _contributions.Add(new ServiceContribution(_moduleId, serviceType, implementationType, lifetime));
            }

            public void RegisterInstance(Type serviceType, object instance)
            {
                if (!serviceType.IsInstanceOfType(instance))
                {
                    throw new ArgumentException(
                        $"实例类型 {instance.GetType().FullName} 不能赋给服务类型 {serviceType.FullName}。");
                }

                _contributions.Add(new ServiceContribution(_moduleId, serviceType, instance));
            }
        }
    }
}
