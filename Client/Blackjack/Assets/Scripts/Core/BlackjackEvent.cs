namespace Lyking.Blackjack.Core
{
    /// <summary>对局过程事件的类型，供表现层日志与测试断言。</summary>
    public enum BlackjackEventType
    {
        /// <summary>玩家下注（带下注金额）。</summary>
        BetPlaced,

        /// <summary>本局初始发牌完成（玩家-庄家-玩家-庄家四张）。</summary>
        CardsDealt,

        /// <summary>玩家要牌。</summary>
        PlayerHit,

        /// <summary>玩家停牌。</summary>
        PlayerStood,

        /// <summary>玩家加倍：赌注翻倍且只再补一张（带加倍后的总赌注）。</summary>
        Doubled,

        /// <summary>玩家爆牌。</summary>
        PlayerBusted,

        /// <summary>庄家补牌。</summary>
        DealerHit,

        /// <summary>庄家爆牌。</summary>
        DealerBusted,

        /// <summary>本局结算（带净输赢金额，正值赢、负值输、零为平局退注）。</summary>
        Settled,

        /// <summary>新一局开始。</summary>
        RoundStarted,

        /// <summary>牌堆不足阈值，整副重洗。</summary>
        ShoeReshuffled,
    }

    /// <summary>一条带局数与金额的事件记录。</summary>
    public readonly struct BlackjackEventLog
    {
        public BlackjackEventLog(int round, BlackjackEventType type, int amount)
        {
            Round = round;
            Type = type;
            Amount = amount;
        }

        /// <summary>事件所属局数（从 1 起）。</summary>
        public int Round { get; }

        public BlackjackEventType Type { get; }

        /// <summary>事件金额：下注额、结算净输赢、加倍后总赌注；无金额事件为 0。</summary>
        public int Amount { get; }

        public override string ToString()
        {
            return Amount != 0
                ? $"第 {Round} 局: {Type}({Amount:+0;-0;0})"
                : $"第 {Round} 局: {Type}";
        }
    }
}
