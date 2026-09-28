using System;

namespace Lyking.GameFramework.PluginAbstractions.Modules
{
    /// <summary>
    /// 模块接口版本。兼容规则：主版本相同且实际版本不低于依赖要求的最低版本。
    /// 主版本不同视为不兼容，禁止静默降级。
    /// </summary>
    public sealed class ModuleVersion : IComparable<ModuleVersion>, IEquatable<ModuleVersion>
    {
        public const int CurrentApiVersion = 1;

        public ModuleVersion(int major, int minor)
        {
            if (major < 0 || minor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(major), "模块版本号不能为负。");
            }

            Major = major;
            Minor = minor;
        }

        public int Major { get; }

        public int Minor { get; }

        /// <summary>当前模块版本是否满足依赖方声明的最低要求。</summary>
        public bool Satisfies(ModuleVersion required) =>
            Major == required.Major
            && (Minor > required.Minor || (Minor == required.Minor));

        public int CompareTo(ModuleVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            var major = Major.CompareTo(other.Major);
            return major != 0 ? major : Minor.CompareTo(other.Minor);
        }

        public bool Equals(ModuleVersion? other) =>
            other is not null && Major == other.Major && Minor == other.Minor;

        public override bool Equals(object? obj) => obj is ModuleVersion other && Equals(other);

        public override int GetHashCode() => (Major << 16) ^ Minor;

        public override string ToString() => $"{Major}.{Minor}";
    }
}
