namespace Lyking.MazeChase.Core
{
    /// <summary>棋局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum MazeChaseEventType
    {
        /// <summary>开局装配完成。</summary>
        Spawned,

        /// <summary>吃掉一颗豆子。</summary>
        AtePellet,

        /// <summary>吃掉一颗能量豆。</summary>
        AtePowerPellet,

        /// <summary>惊惧开始：在场幽灵进入可被吃掉状态。</summary>
        FrightenedStarted,

        /// <summary>惊惧结束：惊惧计时自然耗尽。</summary>
        FrightenedEnded,

        /// <summary>惊惧期吃掉幽灵，幽灵被送回巢格延迟复活。</summary>
        GhostEaten,

        /// <summary>被普通幽灵抓住。</summary>
        PlayerCaught,

        /// <summary>失去一条生命，玩家回出生点、幽灵回巢。</summary>
        LifeLost,

        /// <summary>清空全部豆子过关，进入下一关。</summary>
        LevelCleared,

        /// <summary>生命耗尽，游戏结束。</summary>
        GameOver,
    }

    /// <summary>一条带棋局时刻的事件记录。</summary>
    public readonly struct MazeChaseEventLog
    {
        public MazeChaseEventLog(float time, MazeChaseEventType type)
        {
            Time = time;
            Type = type;
        }

        /// <summary>事件发生时的棋局累计时间（秒）。</summary>
        public float Time { get; }

        public MazeChaseEventType Type { get; }

        public override string ToString() => $"时刻 {Time:0.0}s: {Type}";
    }
}
