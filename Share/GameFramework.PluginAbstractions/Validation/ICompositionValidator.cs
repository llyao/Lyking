using System.Collections.Generic;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Planning;
using Lyking.GameFramework.PluginAbstractions.Registrar;

namespace Lyking.GameFramework.PluginAbstractions.Validation
{
    /// <summary>
    /// 组合校验合同。实现必须覆盖：重复 Id、缺失依赖、版本不兼容、依赖环、
    /// 能力冲突、状态命名空间冲突、重复服务贡献。
    /// </summary>
    public interface ICompositionValidator
    {
        CompositionValidationResult Validate(
            IReadOnlyList<ModuleDescriptor> descriptors,
            ServiceContributionSet contributions);
    }
}
