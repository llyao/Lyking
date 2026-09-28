using System;
using System.Collections.Generic;
using System.Linq;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginAbstractions.Registrar
{
    /// <summary>
    /// 全部模块贡献的有序集合。只收集不裁决；冲突由组合校验器统一诊断，
    /// 保证一次启动能看到全部问题而不是第一个失败。
    /// </summary>
    public sealed class ServiceContributionSet
    {
        private readonly List<ServiceContribution> _contributions = new List<ServiceContribution>();

        public IReadOnlyList<ServiceContribution> All => _contributions;

        public void Add(ServiceContribution contribution)
        {
            if (contribution is null)
            {
                throw new ArgumentNullException(nameof(contribution));
            }

            _contributions.Add(contribution);
        }

        public IEnumerable<ServiceContribution> ForModule(ModuleId moduleId) =>
            _contributions.Where(c => c.ContributedBy == moduleId);

        public IEnumerable<IGrouping<Type, ServiceContribution>> ByServiceType() =>
            _contributions.GroupBy(c => c.ServiceType);
    }
}
