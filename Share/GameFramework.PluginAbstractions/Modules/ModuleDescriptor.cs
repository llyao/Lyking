using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>
    /// 模块对外发布的合同。模块通过 Describe() 声明身份、能力、依赖与状态命名空间，
    /// 不直接触碰任何容器；组合层据此生成并校验计划。
    /// </summary>
    public sealed class ModuleDescriptor
    {
        public ModuleDescriptor(
            string id,
            ModuleVersion version,
            ModuleKind kind,
            string stateNamespace,
            ModuleCapabilities? capabilities = null,
            IReadOnlyList<ModuleDependency>? dependencies = null,
            bool isRequired = true,
            string? description = null)
        {
            Id = new ModuleId(id);
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Kind = kind;
            StateNamespace = stateNamespace;
            Capabilities = capabilities ?? ModuleCapabilities.None;
            Dependencies = dependencies ?? Array.Empty<ModuleDependency>();
            IsRequired = isRequired;
            Description = description;
        }

        public ModuleId Id { get; }

        public ModuleVersion Version { get; }

        public ModuleKind Kind { get; }

        /// <summary>模块拥有的状态命名空间前缀，全局唯一，禁止跨模块直写。</summary>
        public string StateNamespace { get; }

        public ModuleCapabilities Capabilities { get; }

        public IReadOnlyList<ModuleDependency> Dependencies { get; }

        /// <summary>必需模块启动失败会中止整个宿主；可选模块失败进入显式降级。</summary>
        public bool IsRequired { get; }

        public string? Description { get; }

        public override string ToString() => $"{Id}@{Version}({Kind})";
    }
}
