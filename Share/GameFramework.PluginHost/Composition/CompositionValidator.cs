using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;
using Lyking.GameFramework.PluginAbstractions.Validation;

namespace Lyking.GameFramework.PluginHost.Composition
{
    /// <summary>
    /// 默认组合校验器。覆盖 design.md 3.9 节要求的全部检查：
    /// 重复 Id、缺失依赖、版本不兼容、依赖环、能力冲突、命名空间冲突、重复服务贡献。
    /// 全部诊断一次收集，不做首个失败短路。
    /// </summary>
    public sealed class CompositionValidator : ICompositionValidator
    {
        public CompositionValidationResult Validate(
            IReadOnlyList<ModuleDescriptor> descriptors,
            ServiceContributionSet contributions)
        {
            var diagnostics = new List<PlanDiagnostic>();

            CollectDescriptorDiagnostics(descriptors, diagnostics);
            CollectDependencyDiagnostics(descriptors, diagnostics);
            CollectCycleDiagnostics(descriptors, diagnostics);
            CollectServiceDiagnostics(contributions, diagnostics);

            return new CompositionValidationResult(diagnostics);
        }

        private static void CollectDescriptorDiagnostics(
            IReadOnlyList<ModuleDescriptor> descriptors,
            List<PlanDiagnostic> diagnostics)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var namespaces = new Dictionary<string, List<ModuleDescriptor>>(StringComparer.Ordinal);
            var capabilities = new Dictionary<string, List<ModuleDescriptor>>(StringComparer.Ordinal);

            foreach (var descriptor in descriptors)
            {
                if (string.IsNullOrWhiteSpace(descriptor.StateNamespace))
                {
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.InvalidDescriptor,
                        "StateNamespace 不能为空。",
                        descriptor.Id.Value));
                }

                if (!seenIds.Add(descriptor.Id.Value))
                {
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.DuplicateModuleId,
                        $"模块 Id 重复：{descriptor.Id}。",
                        descriptor.Id.Value));
                }

                if (!namespaces.TryGetValue(descriptor.StateNamespace, out var nsModules))
                {
                    nsModules = new List<ModuleDescriptor>();
                    namespaces[descriptor.StateNamespace] = nsModules;
                }

                nsModules.Add(descriptor);

                foreach (var capability in descriptor.Capabilities.Items)
                {
                    if (!capabilities.TryGetValue(capability, out var capModules))
                    {
                        capModules = new List<ModuleDescriptor>();
                        capabilities[capability] = capModules;
                    }

                    capModules.Add(descriptor);
                }
            }

            foreach (var pair in namespaces)
            {
                if (pair.Value.Count > 1)
                {
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.StateNamespaceConflict,
                        $"StateNamespace '{pair.Key}' 被 {string.Join(", ", pair.Value.Select(d => d.Id.Value))} 同时声明。",
                        pair.Value[1].Id.Value));
                }
            }

            foreach (var pair in capabilities)
            {
                if (pair.Value.Count > 1)
                {
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.DuplicateCapability,
                        $"能力 '{pair.Key}' 被 {string.Join(", ", pair.Value.Select(d => d.Id.Value))} 同时提供。",
                        pair.Value[1].Id.Value));
                }
            }
        }

        private static void CollectDependencyDiagnostics(
            IReadOnlyList<ModuleDescriptor> descriptors,
            List<PlanDiagnostic> diagnostics)
        {
            var byId = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in descriptors)
            {
                byId[descriptor.Id.Value] = descriptor;
            }

            foreach (var descriptor in descriptors)
            {
                foreach (var dependency in descriptor.Dependencies)
                {
                    if (!byId.TryGetValue(dependency.Id.Value, out var target))
                    {
                        diagnostics.Add(new PlanDiagnostic(
                            PlanDiagnosticCode.MissingDependency,
                            $"{descriptor.Id} 依赖的模块 {dependency.Id} 不存在。",
                            descriptor.Id.Value));
                        continue;
                    }

                    if (!target.Version.Satisfies(dependency.MinimumVersion))
                    {
                        diagnostics.Add(new PlanDiagnostic(
                            PlanDiagnosticCode.IncompatibleVersion,
                            $"{descriptor.Id} 要求 {dependency.Id} >= {dependency.MinimumVersion}，"
                            + $"实际为 {target.Version}（主版本必须一致）。",
                            descriptor.Id.Value));
                    }
                }
            }
        }

        private static void CollectCycleDiagnostics(
            IReadOnlyList<ModuleDescriptor> descriptors,
            List<PlanDiagnostic> diagnostics)
        {
            const int Visiting = 1;
            const int Done = 2;

            var byId = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in descriptors)
            {
                byId[descriptor.Id.Value] = descriptor;
            }

            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<ModuleDescriptor>();

            foreach (var descriptor in descriptors)
            {
                Visit(descriptor);
            }

            void Visit(ModuleDescriptor descriptor)
            {
                state.TryGetValue(descriptor.Id.Value, out var current);
                if (current == Visiting)
                {
                    var from = stack.IndexOf(descriptor);
                    var cycle = from >= 0
                        ? stack.GetRange(from, stack.Count - from)
                        : new List<ModuleDescriptor> { descriptor };
                    var chain = string.Join(" -> ", cycle.Select(d => d.Id.Value)) + " -> " + descriptor.Id.Value;
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.DependencyCycle,
                        $"存在依赖环：{chain}。",
                        descriptor.Id.Value));
                    return;
                }

                if (current == Done)
                {
                    return;
                }

                state[descriptor.Id.Value] = Visiting;
                stack.Add(descriptor);

                foreach (var dependency in descriptor.Dependencies)
                {
                    if (byId.TryGetValue(dependency.Id.Value, out var target))
                    {
                        Visit(target);
                    }
                }

                stack.RemoveAt(stack.Count - 1);
                state[descriptor.Id.Value] = Done;
            }
        }

        private static void CollectServiceDiagnostics(
            ServiceContributionSet contributions,
            List<PlanDiagnostic> diagnostics)
        {
            foreach (var group in contributions.ByServiceType())
            {
                if (group.Count() > 1)
                {
                    var contributors = string.Join(", ", group.Select(c => c.ContributedBy.Value));
                    diagnostics.Add(new PlanDiagnostic(
                        PlanDiagnosticCode.DuplicateServiceRegistration,
                        $"服务 {group.Key.FullName} 被 {contributors} 重复贡献。",
                        group.First().ContributedBy.Value));
                }
            }
        }
    }
}
