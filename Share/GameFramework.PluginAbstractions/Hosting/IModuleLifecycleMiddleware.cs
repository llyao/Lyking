using System;
using System.Threading;
using System.Threading.Tasks;
using Lyking.GameFramework.PluginAbstractions.Planning;

namespace Lyking.GameFramework.PluginAbstractions.Hosting
{
    /// <summary>
    /// 生命周期中间件钩子。围绕每个模块的启动/停止被依次调用，
    /// 用于诊断、计时、追踪等横切关注点；不允许修改模块状态。
    /// </summary>
    public interface IModuleLifecycleMiddleware
    {
        ValueTask OnModuleStarting(PlannedModule module, CancellationToken cancellationToken) => default;

        ValueTask OnModuleStarted(PlannedModule module, TimeSpan elapsed, CancellationToken cancellationToken) => default;

        ValueTask OnModuleFault(PlannedModule module, Exception failure, CancellationToken cancellationToken) => default;

        ValueTask OnModuleStopped(PlannedModule module, TimeSpan elapsed, CancellationToken cancellationToken) => default;
    }
}
