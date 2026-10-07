namespace Lyking.BattleCity.Core
{
    /// <summary>玩家坦克：连续坐标中心与朝向，由核心每子步按确定规则更新。</summary>
    public readonly struct BattleCityTank
    {
        public BattleCityTank(Vec2 position, TankDir facing)
        {
            Position = position;
            Facing = facing;
        }

        /// <summary>当前中心位置。</summary>
        public Vec2 Position { get; }

        /// <summary>当前朝向，射击沿该方向出膛。</summary>
        public TankDir Facing { get; }

        public BattleCityTank WithPosition(Vec2 position) => new BattleCityTank(position, Facing);

        public override string ToString() => $"坦克 {Position} 朝 {Facing}";
    }

    /// <summary>敌方坦克：位置、朝向、追击目标标记与开火计时，位置由核心每子步按确定规则更新。</summary>
    public readonly struct BattleCityEnemy
    {
        public BattleCityEnemy(Vec2 position, TankDir facing, bool chasePlayer, float fireTimer)
        {
            Position = position;
            Facing = facing;
            ChasePlayer = chasePlayer;
            FireTimer = fireTimer;
        }

        /// <summary>当前中心位置。</summary>
        public Vec2 Position { get; }

        /// <summary>当前朝向，射击沿该方向出膛。</summary>
        public TankDir Facing { get; }

        /// <summary>追击目标标记：true 追玩家，false 追基地；出生时由种子决定，终身不变。</summary>
        public bool ChasePlayer { get; }

        /// <summary>距下次开火的秒数。</summary>
        public float FireTimer { get; }

        public BattleCityEnemy WithPosition(Vec2 position) => new BattleCityEnemy(position, Facing, ChasePlayer, FireTimer);

        public BattleCityEnemy WithFacing(TankDir facing) => new BattleCityEnemy(Position, facing, ChasePlayer, FireTimer);

        public BattleCityEnemy WithFireTimer(float fireTimer) => new BattleCityEnemy(Position, Facing, ChasePlayer, fireTimer);

        public override string ToString() => $"敌人 {Position} 朝 {Facing}";
    }

    /// <summary>一发子弹：连续坐标、飞行方向与阵营，命中砖墙、钢墙、基地或坦克即结算消失。</summary>
    public readonly struct BattleCityBullet
    {
        public BattleCityBullet(Vec2 position, TankDir direction, bool fromPlayer)
        {
            Position = position;
            Direction = direction;
            FromPlayer = fromPlayer;
        }

        /// <summary>当前中心位置。</summary>
        public Vec2 Position { get; }

        /// <summary>飞行方向，直线匀速。</summary>
        public TankDir Direction { get; }

        /// <summary>阵营：true 玩家子弹，false 敌方子弹；敌我子弹迎面对消。</summary>
        public bool FromPlayer { get; }

        public BattleCityBullet Advanced(float distance) => new BattleCityBullet(Position + Direction.ToVector() * distance, Direction, FromPlayer);

        public override string ToString() => $"{(FromPlayer ? "玩家" : "敌方")}子弹 {Position}";
    }
}
