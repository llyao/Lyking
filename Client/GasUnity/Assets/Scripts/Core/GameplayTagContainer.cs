using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyking.GameplayAbilities.Core
{
    public sealed class GameplayTagContainer
    {
        private readonly HashSet<GameplayTag> _tags = new HashSet<GameplayTag>();

        public int Count => _tags.Count;

        public void Add(GameplayTag tag) => _tags.Add(tag);

        public void Add(string tag) => Add(new GameplayTag(tag));

        public bool Remove(GameplayTag tag) => _tags.Remove(tag);

        public bool Remove(string tag) => Remove(new GameplayTag(tag));

        public bool HasTag(GameplayTag requested)
        {
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
