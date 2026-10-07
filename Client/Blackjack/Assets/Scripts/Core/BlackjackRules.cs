using System.Collections.Generic;

namespace Lyking.Blackjack.Core
{
    /// <summary>
    /// 21 点规则常量与纯函数助手：初始筹码 1000、最小注 50、庄家 17 点停牌、
    /// 天牌 Blackjack 赔 3:2、单副 52 张、牌堆不足 15 张时下局发牌前重洗。
    /// </summary>
    public static class BlackjackRules
    {
        /// <summary>玩家初始筹码，新一局保留当前筹码、只有重开全新对局才重置。</summary>
        public const int InitialChips = 1000;

        /// <summary>每局最小下注额，低于该值或超出当前筹码的下注被拒绝。</summary>
        public const int MinBet = 50;

        /// <summary>庄家停牌线：补牌到该点数及以上即停。</summary>
        public const int DealerStandValue = 17;

        /// <summary>玩家天牌赔付分子：净赢赌注的 3/2。</summary>
        public const int BlackjackPayoutNumerator = 3;

        /// <summary>玩家天牌赔付分母。</summary>
        public const int BlackjackPayoutDenominator = 2;

        /// <summary>单副牌总张数。</summary>
        public const int DeckCardCount = 52;

        /// <summary>重洗阈值：牌堆剩余低于该张数时，下一局发牌前整副重洗。</summary>
        public const int ReshuffleThreshold = 15;

        /// <summary>按黑桃、红心、方块、梅花 × A 到 K 的固定顺序生成一副 52 张牌。</summary>
        public static List<Card> CreateDeck()
        {
            var cards = new List<Card>(DeckCardCount);
            for (int suit = 0; suit < 4; suit++)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    cards.Add(new Card((Suit)suit, (Rank)rank));
                }
            }

            return cards;
        }
    }
}
