using System;

namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>对其他模块的依赖声明。Required 依赖缺失或版本不兼容时组合失败。</summary>
    public sealed class ModuleDependency
    {
        public ModuleDependency(string moduleId, ModuleVersion minimumVersion, bool required = true)
        {
            Id = new ModuleId(moduleId);
            MinimumVersion = minimumVersion ?? throw new ArgumentNullException(nameof(minimumVersion));
            Required = required;
        }

        public ModuleId Id { get; }

        public ModuleVersion MinimumVersion { get; }

        public bool Required { get; }
    }
}
