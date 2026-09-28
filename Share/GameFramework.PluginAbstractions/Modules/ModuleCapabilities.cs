using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>
    /// 模块对外提供的能力集合（如 clock、transport、persistence）。
    /// 同一能力只允许一个模块提供，冲突在组合阶段失败，不允许后注册覆盖。
    /// </summary>
    public sealed class ModuleCapabilities
    {
        private readonly IReadOnlyList<string> _capabilities;

        public ModuleCapabilities(params string[] capabilities)
            : this((IEnumerable<string>)capabilities)
        {
        }

        public ModuleCapabilities(IEnumerable<string> capabilities)
        {
            var normalized = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var capability in capabilities)
            {
                if (string.IsNullOrWhiteSpace(capability))
                {
                    throw new ArgumentException("能力名称不能为空。", nameof(capabilities));
                }

                normalized.Add(capability.Trim());
            }

            _capabilities = normalized.ToList();
        }

        public static ModuleCapabilities None { get; } = new ModuleCapabilities();

        public IReadOnlyList<string> Items => _capabilities;

        public bool Contains(string capability) =>
            _capabilities.Contains(capability, StringComparer.Ordinal);

        public override string ToString() =>
            _capabilities.Count == 0 ? "(none)" : string.Join(",", _capabilities);
    }
}
