namespace Lyking.GameFramework.PluginAbstractions.Registrar
{
    /// <summary>
    /// 贡献生命周期。Singleton 属根容器；Scoped 属命名作用域；Transient 每次解析新建。
    /// </summary>
    public enum ModuleLifetime
    {
        Singleton = 0,
        Scoped = 1,
        Transient = 2,
    }
}
