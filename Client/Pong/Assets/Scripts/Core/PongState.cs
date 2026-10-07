namespace Lyking.Pong.Core
{
    /// <summary>对局状态：进行中，或某一方先到胜利分获胜。</summary>
    public enum PongState
    {
        Running,
        LeftWon,
        RightWon,
    }
}
