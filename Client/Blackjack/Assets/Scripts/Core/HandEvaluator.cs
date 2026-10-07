using System.Collections.Generic;

namespace Lyking.Blackjack.Core
{
    /// <summary>
    /// 手牌点数估值纯函数：A 先按 11 计、总和超 21 时逐张降为 1（软硬点转换），
    /// J/Q/K 记 10，其余按面值；天牌仅指首两张即 21 点。
    /// </summary>
    public static class HandEvaluator
    {
        /// <summary>单张牌的基础分值：A 记 11，J/Q/K 记 10，其余按面值。</summary>
        public static int CardValue(Card card)
        {
            if (card.Rank == Rank.Ace) return 11;
            if (card.Rank >= Rank.Ten) return 10;
            return (int)card.Rank;
        }

        /// <summary>手牌最优点数：A 先按 11 计入，超出 21 时把 A 逐张降为 1。</summary>
        public static int BestValue(IReadOnlyList<Card> cards)
        {
            int total = 0;
            int aces = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                total += CardValue(cards[i]);
                if (cards[i].Rank == Rank.Ace) aces++;
            }

            while (total > 21 && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return total;
        }

        /// <summary>手牌最优点数超过 21 即爆牌。</summary>
        public static bool IsBust(IReadOnlyList<Card> cards)
        {
            return BestValue(cards) > 21;
        }

        /// <summary>天牌判定：恰好两张牌且点数为 21（A + 任意 10 值牌）。</summary>
        public static bool IsBlackjack(IReadOnlyList<Card> cards)
        {
            return cards.Count == 2 && BestValue(cards) == 21;
        }
    }
}
