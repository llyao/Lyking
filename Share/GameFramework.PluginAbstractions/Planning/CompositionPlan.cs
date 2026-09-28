using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Registrar;

namespace Lyking.GameFramework.PluginAbstractions.Planning
{
    /// <summary>
    /// 通过校验的组合计划：拓扑有序的模块列表 + 已校验的服务贡献。
    /// 计划是冻结的，物化层只应用计划，不再做任何决策。
    /// </summary>
    public sealed class CompositionPlan
    {
        public CompositionPlan(IReadOnlyList<PlannedModule> modules, ServiceContributionSet contributions)
        {
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
            Contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));
        }

        public IReadOnlyList<PlannedModule> Modules { get; }

        public ServiceContributionSet Contributions { get; }

        public PlannedModule? Find(ModuleId id)
        {
            foreach (var module in Modules)
            {
                if (module.Descriptor.Id == id)
                {
                    return module;
                }
            }

            return null;
        }

        public override string ToString() =>
            string.Join(" -> ", Modules.Select(m => m.Descriptor.Id.Value));
    }
}
