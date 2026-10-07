namespace Lyking.Blackjack.Core
{
    /// <summary>21 点对局的阶段：下注、玩家回合、庄家回合、已结算。</summary>
    public enum BlackjackPhase
    {
        /// <summary>等待玩家下注，下注后立即发牌。</summary>
        Betting,

        /// <summary>玩家回合：要牌、加倍或停牌。</summary>
        PlayerTurn,

        /// <summary>庄家按规则补牌到 17 点及以上。</summary>
        DealerTurn,

        /// <summary>本局已结算，可开下一局。</summary>
        Settled,
    }
}
