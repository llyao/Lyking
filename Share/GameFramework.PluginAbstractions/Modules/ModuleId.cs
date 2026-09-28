using System;

namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>
    /// 模块稳定身份。与程序集名称无关，跨宿主不变。
    /// 只允许字母、数字、点、下划线和连字符，防止依赖图校验被空白字符绕过。
    /// </summary>
    public sealed class ModuleId : IEquatable<ModuleId>
    {
        private readonly string _value;

        public ModuleId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("模块 Id 不能为空。", nameof(value));
            }

            foreach (var ch in value)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '_' && ch != '-')
                {
                    throw new ArgumentException($"模块 Id 含非法字符 '{ch}'：{value}", nameof(value));
                }
            }

            _value = value;
        }

        public string Value => _value;

        public bool Equals(ModuleId? other) => other is not null && _value == other._value;

        public override bool Equals(object? obj) => obj is ModuleId other && Equals(other);

        public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

        public override string ToString() => _value;

        public static bool operator ==(ModuleId? left, ModuleId? right) =>
            ReferenceEquals(left, right) || (left is not null && left.Equals(right));

        public static bool operator !=(ModuleId? left, ModuleId? right) => !(left == right);
    }
}
