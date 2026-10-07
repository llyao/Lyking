#nullable enable
using System;

namespace Lyking.GameplayAbilities.Core
{
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        private readonly string? _name;

        public GameplayTag(string name)
        {
            _name = Normalize(name);
        }

        public string Name => _name ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(Name);

        public bool IsParentOf(GameplayTag child)
        {
            return !IsEmpty && !child.IsEmpty && child.Name.Length > Name.Length
                && child.Name.StartsWith(Name + ".", StringComparison.Ordinal);
        }

        public bool Equals(GameplayTag other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => IsEmpty ? 0 : StringComparer.Ordinal.GetHashCode(Name);

        public override string ToString() => Name;

        public static implicit operator GameplayTag(string name) => new GameplayTag(name);

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("GameplayTag 名称不能为空。", nameof(name));
            }

            string normalized = name.Trim();
            foreach (string segment in normalized.Split('.'))
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new ArgumentException("GameplayTag 层级段不能为空。", nameof(name));
                }
            }

            return normalized;
        }
    }
}
