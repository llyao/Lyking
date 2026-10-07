namespace Lyking.Asteroids.Core
{
    /// <summary>小行星档位：大、中、小，决定半径、分值与被击碎裂的结果。</summary>
    public enum AsteroidSize
    {
        Large,
        Medium,
        Small,
    }

    /// <summary>玩家飞船状态：位置、速度与朝向（弧度，0 朝右、逆时针为正，开局朝上）。</summary>
    public readonly struct AsteroidsShip
    {
        public AsteroidsShip(Vec2 position, Vec2 velocity, float heading)
        {
            Position = position;
            Velocity = velocity;
            Heading = heading;
        }

        /// <summary>当前中心位置，越界后从对侧环绕。</summary>
        public Vec2 Position { get; }

        /// <summary>当前速度，受推进、阻尼与上限共同约束。</summary>
        public Vec2 Velocity { get; }

        /// <summary>当前朝向角（弧度），射击与推进均沿该方向。</summary>
        public float Heading { get; }

        public override string ToString() => $"飞船 {Position} 朝向 {Heading:0.00}";
    }

    /// <summary>飞船发射的子弹：匀速直线飞行，寿命耗尽即消失，越界从对侧环绕。</summary>
    public readonly struct AsteroidsBullet
    {
        public AsteroidsBullet(Vec2 position, Vec2 velocity, float lifeRemaining)
        {
            Position = position;
            Velocity = velocity;
            LifeRemaining = lifeRemaining;
        }

        public Vec2 Position { get; }

        public Vec2 Velocity { get; }

        /// <summary>剩余寿命（秒），归零即从战局移除。</summary>
        public float LifeRemaining { get; }

        public override string ToString() => $"子弹 {Position}";
    }

    /// <summary>一颗小行星：匀速漂移并缓慢自旋，越界环绕；被击中按档位碎裂或消灭。</summary>
    public readonly struct AsteroidsAsteroid
    {
        public AsteroidsAsteroid(Vec2 position, Vec2 velocity, AsteroidSize size, float rotation, float spin)
        {
            Position = position;
            Velocity = velocity;
            Size = size;
            Rotation = rotation;
            Spin = spin;
        }

        /// <summary>当前中心位置，越界后从对侧环绕。</summary>
        public Vec2 Position { get; }

        /// <summary>漂移速度，大小由波次决定、方向由注入种子决定。</summary>
        public Vec2 Velocity { get; }

        public AsteroidSize Size { get; }

        /// <summary>当前自转角（弧度），随自旋角速度累加，渲染可忽略。</summary>
        public float Rotation { get; }

        /// <summary>自转角速度（弧度/秒），由注入种子决定，可正可负。</summary>
        public float Spin { get; }

        public override string ToString() => $"{Size} 小行星 {Position}";
    }
}
