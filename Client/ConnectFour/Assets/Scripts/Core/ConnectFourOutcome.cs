namespace Lyking.ConnectFour.Core
{
    /// <summary>一局的终局结果；对局进行中为 <see cref="None"/>。</summary>
    public enum ConnectFourOutcome
    {
        None,
        RedWins,
        YellowWins,
        Draw,
    }
}
