namespace Lyking.DoodleJumper.Core
{
    /// <summary>
    /// 一帧玩家输入的纯数据快照，全部为持续量：<see cref="MoveX"/> 为水平移动意图
    /// （-1 左、+1 右，允许中间值），<see cref="FastFallHeld"/> 为快落键按住状态，每个子步都生效。
    /// </summary>
    public readonly struct JumperInput
    {
        public JumperInput(float moveX, bool fastFallHeld)
        {
            MoveX = moveX;
            FastFallHeld = fastFallHeld;
        }

        /// <summary>水平移动意图，核心内部会夹到 -1 到 +1 之间。</summary>
        public float MoveX { get; }

        /// <summary>本帧是否持续按住快落键（空中生效，重力按倍率放大）。</summary>
        public bool FastFallHeld { get; }

        /// <summary>无输入的默认快照。</summary>
        public static JumperInput None => default;
    }
}
