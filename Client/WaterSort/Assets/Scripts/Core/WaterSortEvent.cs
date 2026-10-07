namespace Lyking.WaterSort.Core
{
    /// <summary>水体排序过程事件的类型，供表现层日志与测试断言。</summary>
    public enum WaterSortEventType
    {
        /// <summary>生成新谜题（含开局构造）。</summary>
        NewPuzzle,

        /// <summary>两段式操作中选中一只瓶子。</summary>
        Selected,

        /// <summary>再次点击已选中瓶子，取消选中。</summary>
        Deselected,

        /// <summary>倒水成功，附带倒入层数。</summary>
        Poured,

        /// <summary>倒水被拒：越界、空源、满目标、异色顶层或源即目标。</summary>
        PourRejected,

        /// <summary>撤销一次倒水。</summary>
        Undone,

        /// <summary>重置回当前谜题初始布局。</summary>
        Reset,

        /// <summary>全部瓶子要么为空要么同色满，通关。</summary>
        Won,
    }

    /// <summary>一条带步序号与瓶序号的事件记录。</summary>
    public readonly struct WaterSortEventLog
    {
        public WaterSortEventLog(int moveNumber, WaterSortEventType type, int fromTube, int toTube, int layerCount)
        {
            MoveNumber = moveNumber;
            Type = type;
            FromTube = fromTube;
            ToTube = toTube;
            LayerCount = layerCount;
        }

        /// <summary>事件发生前已完成的倒水次数。</summary>
        public int MoveNumber { get; }

        public WaterSortEventType Type { get; }

        /// <summary>事件涉及的源瓶序号；与瓶无关的事件为 -1。</summary>
        public int FromTube { get; }

        /// <summary>事件涉及的目标瓶序号；与瓶无关的事件为 -1。</summary>
        public int ToTube { get; }

        /// <summary>本次倒水的层数；无关事件为 0。</summary>
        public int LayerCount { get; }

        public override string ToString()
        {
            string text = $"步数 {MoveNumber}: {Type}";
            if (FromTube >= 0 && ToTube >= 0) return $"{text}(瓶{FromTube}->瓶{ToTube} x{LayerCount})";
            if (FromTube >= 0) return $"{text}(瓶{FromTube})";
            return text;
        }
    }
}
