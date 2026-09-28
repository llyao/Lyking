using System;
using Lyking.GameFramework.PluginAbstractions.Modules;

namespace Lyking.GameFramework.PluginAbstractions.Planning
{
    /// <summary>计划中的一个模块条目：描述符 + 拓扑序号。</summary>
    public sealed class PlannedModule
    {
        public PlannedModule(ModuleDescriptor descriptor, int ordinal)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Ordinal = ordinal;
        }

        public ModuleDescriptor Descriptor { get; }

        /// <summary>拓扑序号：0 最先启动，最大值最后启动；停止按逆序执行。</summary>
        public int Ordinal { get; }

        public override string ToString() => $"#{Ordinal} {Descriptor}";
    }
}
