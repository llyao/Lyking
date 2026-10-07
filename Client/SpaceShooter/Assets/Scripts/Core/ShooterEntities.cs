namespace Lyking.SpaceShooter.Core
{
    /// <summary>玩家发射的子弹：位置即中心，从枪口出生后匀速直线上升，命中或飞出场顶即消失。</summary>
    public readonly struct ShooterBullet
    {
        public ShooterBullet(Vec2 position)
        {
            Position = position;
        }

        public Vec2 Position { get; }

        public override string ToString() => $"子弹 {Position}";
    }

    /// <summary>一名敌人：记录出生锚点与摆动相位，当前位置由核心每子步按确定公式更新。</summary>
    public readonly struct ShooterEnemy
    {
        public ShooterEnemy(Vec2 spawnOrigin)
            : this(spawnOrigin, spawnOrigin, 0d)
        {
        }

        public ShooterEnemy(Vec2 position, Vec2 spawnOrigin, double phase)
        {
            Position = position;
            SpawnOrigin = spawnOrigin;
            Phase = phase;
        }

        /// <summary>当前中心位置。</summary>
        public Vec2 Position { get; }

        /// <summary>出生锚点：横坐标固定，正弦摆动绕其展开。</summary>
        public Vec2 SpawnOrigin { get; }

        /// <summary>当前摆动相位（弧度），随时间线性累加。</summary>
        public double Phase { get; }

        public override string ToString() => $"敌人 {Position}";
    }
}
