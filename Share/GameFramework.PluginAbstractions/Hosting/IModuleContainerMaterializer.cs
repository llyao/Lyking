using Lyking.GameFramework.PluginAbstractions.Planning;

namespace Lyking.GameFramework.PluginAbstractions.Hosting
{
    /// <summary>
    /// 物化层合同：把已校验的组合计划应用到具体容器（VContainer/MSDI/自有 Host）。
    /// 物化层只应用计划，不做任何组合决策。
    /// </summary>
    public interface IModuleContainerMaterializer
    {
        IModuleContainer Apply(CompositionPlan plan);
    }
}
