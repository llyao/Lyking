namespace Lyking.Blackjack.Core
{
    /// <summary>扑克花色：黑桃、红心、方块、梅花各 13 张；面板展示映射为字母 S/H/D/C。</summary>
    public enum Suit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs,
    }

    /// <summary>扑克面值：A 记 1，J/Q/K 记 11/12/13；手牌点数折算见 <see cref="HandEvaluator"/>。</summary>
    public enum Rank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
    }

    /// <summary>一张扑克牌的只读值：花色 + 面值。</summary>
    public readonly struct Card
    {
        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public Suit Suit { get; }

        public Rank Rank { get; }

        public override string ToString()
        {
            return $"{Rank}Of{Suit}";
        }
    }
}
