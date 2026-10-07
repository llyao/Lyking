using System.Collections.Generic;

namespace Lyking.DungeonFighter.Core
{
    /// <summary>穿透弹道实体：烈焰波等飞行攻击。</summary>
    public sealed class Projectile
    {
        private readonly HashSet<int> _hitFighterIds = new HashSet<int>();

        public Projectile(int id, FighterTeam team, string displayName, Vec2 position, float directionX, ProjectileSpec spec)
        {
            Id = id;
            Team = team;
            DisplayName = displayName;
            Position = position;
            DirectionX = directionX >= 0 ? 1f : -1f;
            Spec = spec;
        }

        public int Id { get; }

        public FighterTeam Team { get; }

        public string DisplayName { get; }

        public Vec2 Position { get; set; }

        /// <summary>飞行方向的水平分量，取 +1 或 -1。</summary>
        public float DirectionX { get; }

        public ProjectileSpec Spec { get; }

        /// <summary>已飞行距离。</summary>
        public float Traveled { get; set; }

        public bool Expired { get; set; }

        public bool HasHit(int fighterId) => _hitFighterIds.Contains(fighterId);

        public void MarkHit(int fighterId) => _hitFighterIds.Add(fighterId);
    }
}
