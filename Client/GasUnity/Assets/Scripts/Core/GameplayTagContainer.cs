#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class GameplayTagContainer
    {
        private readonly HashSet<GameplayTag> _tags = new HashSet<GameplayTag>();

        public int Count => _tags.Count;

        internal IEnumerable<GameplayTag> Enumerate() => _tags;

        public void Add(GameplayTag tag)
        {
            if (tag.IsEmpty)
            {
                throw new ArgumentException("GameplayTag 容器不能包含空标签。", nameof(tag));
            }

            _tags.Add(tag);
        }

        public void Add(string tag) => Add(new GameplayTag(tag));

        public bool Remove(GameplayTag tag) => !tag.IsEmpty && _tags.Remove(tag);

        public bool Remove(string tag) => Remove(new GameplayTag(tag));

        public bool HasTag(GameplayTag requested)
        {
            if (requested.IsEmpty)
            {
                return false;
            }

            return _tags.Any(owned => owned.Equals(requested) || requested.IsParentOf(owned));
        }

        public bool HasTag(string requested) => HasTag(new GameplayTag(requested));

        public bool HasAny(GameplayTagContainer other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other._tags.Any(HasTag);
        }

        public bool HasAll(GameplayTagContainer other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other._tags.All(HasTag);
        }

        public GameplayTagContainer Clone()
        {
            var clone = new GameplayTagContainer();
            foreach (GameplayTag tag in _tags)
            {
                clone.Add(tag);
            }

            return clone;
        }
    }
}
