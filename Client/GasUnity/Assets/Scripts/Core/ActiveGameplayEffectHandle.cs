#nullable enable
using System;

namespace Lyking.GameplayAbilities.Core
{
    public readonly struct ActiveGameplayEffectHandle : IEquatable<ActiveGameplayEffectHandle>
    {
        public ActiveGameplayEffectHandle(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "活动效果句柄必须是正整数。");
            }

            Id = id;
            _ownerToken = Guid.Empty;
        }

        internal ActiveGameplayEffectHandle(int id, Guid ownerToken)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "活动效果句柄必须是正整数。");
            }

            if (ownerToken == Guid.Empty)
            {
                throw new ArgumentException("活动效果句柄必须带有有效归属令牌。", nameof(ownerToken));
            }

            Id = id;
            _ownerToken = ownerToken;
        }

        public int Id { get; }

        public bool IsValid => Id > 0;

        private readonly Guid _ownerToken;

        public bool Equals(ActiveGameplayEffectHandle other) => Id == other.Id;

        public override bool Equals(object? obj) => obj is ActiveGameplayEffectHandle other && Equals(other);

        public override int GetHashCode() => Id;

        internal bool BelongsTo(Guid ownerToken) => _ownerToken == ownerToken;

        public static bool operator ==(ActiveGameplayEffectHandle left, ActiveGameplayEffectHandle right) => left.Equals(right);

        public static bool operator !=(ActiveGameplayEffectHandle left, ActiveGameplayEffectHandle right) => !left.Equals(right);
    }
}
