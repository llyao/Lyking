using System;
using System.Collections.Generic;

namespace Lyking.Blackjack.Core
{
    /// <summary>
    /// 发牌堆：以注入种子对单副 52 张做 Fisher-Yates 洗牌后按序发牌；
    /// 重洗沿用同一随机流，同一种子构造的牌堆序列完全一致。
    /// </summary>
    public sealed class Shoe
    {
        private readonly List<Card> _cards = new List<Card>(BlackjackRules.DeckCardCount);
        private readonly Random _random;

        public Shoe(int seed)
        {
            _random = new Random(seed);
            Reshuffle();
        }

        /// <summary>牌堆剩余张数。</summary>
        public int RemainingCards => _cards.Count;

        /// <summary>整副重洗：重新生成 52 张并按注入随机流 Fisher-Yates 打乱。</summary>
        public void Reshuffle()
        {
            _cards.Clear();
            _cards.AddRange(BlackjackRules.CreateDeck());
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>从牌堆顶发出一张牌；牌堆为空时抛出异常（对局中应在发牌前重洗避免）。</summary>
        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException("牌堆已空，无法继续发牌。");

            Card card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }
    }
}
