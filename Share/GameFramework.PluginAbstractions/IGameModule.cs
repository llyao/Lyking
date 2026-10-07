using System;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions.Hosting;
using Lyking.GameFramework.PluginAbstractions.Modules;
using Lyking.GameFramework.PluginAbstractions.Registrar;

namespace Lyking.GameFramework.PluginAbstractions
{
    /// <summary>
    /// 游戏模块合同。模块回调序列固定：Describe → Contribute → Start → Drain → Stop；
    /// Plan/Validate/Apply 由宿主在 Start 前执行，模块不参与；Ready 是宿主状态（HostState.Ready），
    /// 不是模块回调阶段；容器释放发生在停止或失败回滚之后。
    /// 模块实例由宿主持有；其运行体通过 IModuleContext 解析协作者，
    /// 协作者之间的依赖通过构造函数注入完成。
    /// </summary>
    public interface IGameModule
    {
        /// <summary>发布模块合同。必须稳定，不读取环境或全局状态。</summary>
        ModuleDescriptor Describe();

        /// <summary>把本模块提供的服务写入贡献中间表示；不触碰具体容器。</summary>
        void Contribute(IModuleRegistrar registrar);

        ValueTask StartAsync(IModuleContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 排空：拒绝新输入、等待在途输入收敛。Drain 按依赖正序执行（上游先停流）。
        /// </summary>
        ValueTask DrainAsync(IModuleContext context, CancellationToken cancellationToken);

        ValueTask StopAsync(IModuleContext context, CancellationToken cancellationToken);
    }
}
