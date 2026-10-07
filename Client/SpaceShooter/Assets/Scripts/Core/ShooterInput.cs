namespace Lyking.SpaceShooter.Core
{
    /// <summary>一帧玩家输入：横移意图与射击键按下沿，纯数据结构、无引擎依赖。</summary>
    public readonly struct ShooterInput
    {
        public ShooterInput(float moveX, bool fire)
        {
            MoveX = moveX;
            Fire = fire;
        }

        /// <summary>水平移动意图：负值向左、正值向右，核心内会被夹到 -1..1。</summary>
        public float MoveX { get; }

        /// <summary>本帧射击键是否刚按下（按下沿），一次 <see cref="SpaceShooterGame.Step"/> 调用至多射击一次。</summary>
        public bool Fire { get; }
    }
}
