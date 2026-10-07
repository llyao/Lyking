namespace Lyking.Blackjack.Core
{
    /// <summary>一局的结算结果；对局进行中为 <see cref="None"/>。</summary>
    public enum BlackjackOutcome
    {
        None,

        /// <summary>玩家首两张即 21 点，按 3:2 赔付。</summary>
        PlayerBlackjack,

        /// <summary>庄家首两张即 21 点，玩家输注。</summary>
        DealerBlackjack,

        /// <summary>玩家要牌超 21 点爆牌，输注。</summary>
        PlayerBusted,

        /// <summary>庄家补牌超 21 点爆牌，玩家获胜。</summary>
        DealerBusted,

        /// <summary>比点玩家更大，1:1 获胜。</summary>
        PlayerWins,

        /// <summary>比点庄家更大，玩家输注。</summary>
        DealerWins,

        /// <summary>点数相同，退回赌注。</summary>
        Push,
    }
}
