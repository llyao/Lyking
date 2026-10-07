namespace Lyking.DungeonFighter.Core
{
    /// <summary>单帧玩家输入快照：表现层采集后传入 <see cref="DungeonGame.Tick"/>。</summary>
    public struct PlayerInputFrame
    {
        /// <summary>横向移动意图，取值 -1..1。</summary>
        public float MoveX;

        /// <summary>纵深移动意图，取值 -1..1。</summary>
        public float MoveY;

        /// <summary>跳跃（按下沿触发）。</summary>
        public bool Jump;

        /// <summary>普攻（按下沿触发，攻击动作中按下会缓冲接下一段）。</summary>
        public bool Attack;

        /// <summary>上挑（按下沿触发）。</summary>
        public bool SkillUpper;

        /// <summary>烈焰波（按下沿触发）。</summary>
        public bool SkillWave;

        public static PlayerInputFrame None => default;
    }
}
