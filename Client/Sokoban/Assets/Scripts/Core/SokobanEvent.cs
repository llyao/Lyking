namespace Lyking.Sokoban.Core
{
    /// <summary>推箱过程事件的类型，供表现层日志与测试断言。</summary>
    public enum SokobanEventType
    {
        /// <summary>装载或切换关卡（含开局装载第一关）。</summary>
        LevelChanged,

        /// <summary>玩家移动一步且未推动箱子。</summary>
        Moved,

        /// <summary>玩家推动箱子移动一步。</summary>
        Pushed,

        /// <summary>移动被拒绝：撞墙、箱子前方是墙或另一箱。</summary>
        MoveRejected,

        /// <summary>撤销一步。</summary>
        Undone,

        /// <summary>当前关重置回初始状态。</summary>
        Reset,

        /// <summary>本关全部箱子就位，通关。</summary>
        LevelSolved,
    }

    /// <summary>一条带步序号与方向的事件记录。</summary>
    public readonly struct SokobanEventLog
    {
        public SokobanEventLog(int moveNumber, SokobanEventType type, SokobanDir direction)
        {
            MoveNumber = moveNumber;
            Type = type;
            Direction = direction;
        }

        /// <summary>事件发生前已完成的步数。</summary>
        public int MoveNumber { get; }

        public SokobanEventType Type { get; }

        /// <summary>触发事件的方向；与方向无关的事件为 <see cref="SokobanDir.None"/>。</summary>
        public SokobanDir Direction { get; }

        public override string ToString() => Direction == SokobanDir.None
            ? $"步数 {MoveNumber}: {Type}"
            : $"步数 {MoveNumber}: {Type}({Direction})";
    }
}
