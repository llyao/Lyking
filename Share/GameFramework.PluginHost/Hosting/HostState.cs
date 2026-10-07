namespace Lyking.GameFramework.PluginHost.Hosting
{
    /// <summary>宿主整体状态。固定流转：Created → Starting → Ready → Draining → Stopped；启动失败进入 Failed。</summary>
    public enum HostState
    {
        Created = 0,
        Starting = 1,
        Ready = 2,
        Draining = 3,
        Stopped = 4,
        Failed = 5,
    }
}
