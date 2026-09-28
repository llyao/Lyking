namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>
    /// 模块四类（与 design.md 3.7/3.8 节一致）：
    /// SimulationModule 只改当前模拟状态；HostAdapter 承载全部外部副作用；
    /// PresentationAdapter 只消费投影并产生 Command；ToolPlugin 只参与编译与校验。
    /// </summary>
    public enum ModuleKind
    {
        SimulationModule = 0,
        HostAdapter = 1,
        PresentationAdapter = 2,
        ToolPlugin = 3,
    }
}
