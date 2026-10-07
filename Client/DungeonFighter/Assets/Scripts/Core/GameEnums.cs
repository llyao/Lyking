namespace Lyking.DungeonFighter.Core
{
    /// <summary>单位朝向：决定近战判定与弹道方向。</summary>
    public enum Facing
    {
        Left = -1,
        Right = 1,
    }

    /// <summary>阵营：玩家或怪物。</summary>
    public enum FighterTeam
    {
        Player = 0,
        Monster = 1,
    }

    /// <summary>单位动作状态：攻击与受击硬直期间不接受移动输入。</summary>
    public enum FighterState
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Hurt = 3,
        Dead = 4,
    }

    /// <summary>怪物 AI 状态机。</summary>
    public enum MonsterAiState
    {
        Idle = 0,
        Chase = 1,
        Windup = 2,
        Recover = 3,
    }

    /// <summary>对局阶段。</summary>
    public enum GamePhase
    {
        InProgress = 0,
        Victory = 1,
        Defeat = 2,
    }
}
