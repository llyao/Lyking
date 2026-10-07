namespace Lyking.BattleCity.Core
{
    /// <summary>一帧玩家输入：四向移动意图（None 为原地）与射击键按下沿，纯数据结构、无引擎依赖。</summary>
    public readonly struct TankInput
    {
        public TankInput(TankDir moveDir, bool fire)
        {
            MoveDir = moveDir;
            Fire = fire;
        }

        /// <summary>移动意图：四向之一，核心内即转向即移动；None 表示本帧原地不动。</summary>
        public TankDir MoveDir { get; }

        /// <summary>本帧射击键是否刚按下（按下沿），一次 <see cref="BattleCityGame.Step"/> 调用至多射击一次。</summary>
        public bool Fire { get; }
    }
}
