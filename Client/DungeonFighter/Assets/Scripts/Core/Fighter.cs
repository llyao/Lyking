namespace Lyking.DungeonFighter.Core
{
    /// <summary>战斗单位基类：位置、朝向、生命与动作状态。属性可写仅限核心模拟内部与测试驱动。</summary>
    public abstract class Fighter
    {
        protected Fighter(int id, FighterTeam team, string displayName)
        {
            Id = id;
            Team = team;
            DisplayName = displayName;
        }

        public int Id { get; }

        public FighterTeam Team { get; }

        public string DisplayName { get; }

        /// <summary>地面平面坐标。</summary>
        public Vec2 Position { get; set; }

        public Facing Facing { get; set; } = Facing.Right;

        public int MaxHealth { get; set; }

        public int Health { get; set; }

        public float MoveSpeed { get; set; }

        public FighterState State { get; set; } = FighterState.Idle;

        /// <summary>当前状态剩余秒数（攻击动作或受击硬直）。</summary>
        public float StateTimer { get; set; }

        /// <summary>受击无敌剩余秒数，期间免疫再次伤害。</summary>
        public float InvulnTimer { get; set; }

        public bool IsAlive => Health > 0 && State != FighterState.Dead;

        /// <summary>是否处于空中（跳跃），空中目标免疫近战与弹道。</summary>
        public virtual bool IsAirborne => false;
    }
}
