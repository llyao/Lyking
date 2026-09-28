using System;
using System.Collections.Generic;

namespace Lyking.GameFramework.PluginHost.Hosting
{
    /// <summary>
    /// 生命周期阶段失败异常：携带模块身份与回滚摘要，
    /// 内部异常保留原始失败语义。
    /// </summary>
    public sealed class ModuleHostException : Exception
    {
        public ModuleHostException(
            string? moduleId,
            string message,
            Exception innerException,
            IReadOnlyList<string>? rollback)
            : base(message, innerException)
        {
            ModuleId = moduleId;
            Rollback = rollback ?? Array.Empty<string>();
        }

        public string? ModuleId { get; }

        /// <summary>启动失败回滚时，逆序停止的模块清单。</summary>
        public IReadOnlyList<string> Rollback { get; }

        public bool HadRollback => Rollback.Count > 0;
    }
}
