namespace Lyking.FlappyBird.Core
{
    /// <summary>一对管道的只读快照：Center 为管道水平中心 X 与缺口中心 Y，Scored 表示小鸟是否已越过。</summary>
    public readonly struct PipePair
    {
        public PipePair(Vec2 center, bool scored)
        {
            Center = center;
            Scored = scored;
        }

        public Vec2 Center { get; }

        public bool Scored { get; }

        public override string ToString() => $"管道 X={Center.X:0.00} 缺口中心 Y={Center.Y:0.00}{(Scored ? " 已得分" : string.Empty)}";
    }
}
