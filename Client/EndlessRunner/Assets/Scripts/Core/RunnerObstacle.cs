namespace Lyking.EndlessRunner.Core
{
    /// <summary>
    /// 一个障碍的只读快照：Center 为横向中心与贴地高度的一半，
    /// IsTall 区分高矮两档（宽高由 <see cref="RunnerRules"/> 提供），Passed 表示是否已被玩家越过。
    /// </summary>
    public readonly struct RunnerObstacle
    {
        public RunnerObstacle(Vec2 center, bool isTall, bool passed)
        {
            Center = center;
            IsTall = isTall;
            Passed = passed;
        }

        public Vec2 Center { get; }

        public bool IsTall { get; }

        public bool Passed { get; }

        public override string ToString() => $"障碍 X={Center.X:0.00} {(IsTall ? "高" : "矮")}{(Passed ? " 已越过" : string.Empty)}";
    }
}
