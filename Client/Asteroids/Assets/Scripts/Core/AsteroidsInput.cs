namespace Lyking.Asteroids.Core
{
    /// <summary>一帧玩家输入：转向、推进与射击按下沿，纯数据结构、无引擎依赖。</summary>
    public readonly struct AsteroidsInput
    {
        public AsteroidsInput(float turn, bool thrust, bool fire)
        {
            Turn = turn;
            Thrust = thrust;
            Fire = fire;
        }

        /// <summary>转向意图：负值左转、正值右转，核心内会被夹到 -1..1。</summary>
        public float Turn { get; }

        /// <summary>本帧推进键是否按住，按住时飞船沿当前朝向持续加速。</summary>
        public bool Thrust { get; }

        /// <summary>本帧射击键是否刚按下（按下沿），一次 <see cref="AsteroidsGame.Step"/> 调用至多射击一次。</summary>
        public bool Fire { get; }
    }
}
