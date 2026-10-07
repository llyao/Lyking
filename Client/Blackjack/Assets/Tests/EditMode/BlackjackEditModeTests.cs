using System.Collections.Generic;
using Lyking.Blackjack.Core;
using NUnit.Framework;

namespace Lyking.Blackjack.EditModeTests
{
    /// <summary>编辑模式冒烟：核心对局在 Unity 程序集边界内可用，状态机推进与暗牌表现一致。</summary>
    public sealed class BlackjackEditModeTests
    {
        [Test]
        public void 对局在引擎内可创建并完成下注发牌()
        {
            var game = 首个玩家回合对局();

            Assert.AreEqual(BlackjackPhase.PlayerTurn, game.State);
            Assert.AreEqual(2, game.PlayerCards.Count);
            Assert.AreEqual(2, game.DealerCards.Count);
            Assert.AreEqual(BlackjackRules.InitialChips - BlackjackRules.MinBet, game.Chips);
            Assert.AreEqual(BlackjackRules.MinBet, game.Bet);
            Assert.IsTrue(game.IsDealerHoleCardHidden, "玩家回合庄家暗牌应保持背面。");

            Assert.IsTrue(game.Stand());
            Assert.AreEqual(BlackjackPhase.Settled, game.State);
            Assert.IsFalse(game.IsDealerHoleCardHidden, "停牌结算后庄家暗牌应揭开。");
        }

        [Test]
        public void 非法阶段的操作被状态机拒绝()
        {
            var game = new BlackjackGame(0);

            Assert.IsFalse(game.Hit(), "未下注不能要牌。");
            Assert.IsFalse(game.Double(), "未下注不能加倍。");
            Assert.IsFalse(game.Stand(), "未下注不能停牌。");
            Assert.IsFalse(game.NextRound(), "未结算不能开下一局。");

            Assert.IsTrue(game.Deal(BlackjackRules.MinBet));
            Assert.IsFalse(game.Deal(BlackjackRules.MinBet), "发牌后不能重复下注。");

            // 玩家连续要牌必然爆牌，推进到结算后再验证操作被拒。
            while (game.State == BlackjackPhase.PlayerTurn) Assert.IsTrue(game.Hit());
            Assert.AreEqual(BlackjackPhase.Settled, game.State);
            Assert.IsFalse(game.Hit());
            Assert.IsFalse(game.Stand());
            Assert.IsFalse(game.Double());
        }

        [Test]
        public void 开下一局复位手牌且筹码保留()
        {
            var game = 首个玩家回合对局();
            Assert.IsTrue(game.Stand());
            int chipsAfterSettle = game.Chips;

            Assert.IsTrue(game.NextRound());

            Assert.AreEqual(BlackjackPhase.Betting, game.State);
            Assert.AreEqual(2, game.Round);
            Assert.AreEqual(chipsAfterSettle, game.Chips, "新一局必须保留上一局的筹码。");
            Assert.IsEmpty(game.PlayerCards);
            Assert.IsEmpty(game.DealerCards);
            Assert.AreEqual(0, game.Bet);
            Assert.AreEqual(BlackjackEventType.RoundStarted, game.Events[game.Events.Count - 1].Type);
        }

        [Test]
        public void 点数估值与天牌判定在引擎内一致()
        {
            // A 先按 11 计、超出 21 降为 1；天牌仅限首两张 21。
            Assert.AreEqual(21, HandEvaluator.BestValue(new[]
            {
                new Card(Suit.Spades, Rank.Ace), new Card(Suit.Hearts, Rank.King),
            }));
            Assert.AreEqual(19, HandEvaluator.BestValue(new[]
            {
                new Card(Suit.Spades, Rank.Ace), new Card(Suit.Hearts, Rank.Nine), new Card(Suit.Clubs, Rank.Nine),
            }));
            Assert.IsTrue(HandEvaluator.IsBlackjack(new[]
            {
                new Card(Suit.Spades, Rank.Ace), new Card(Suit.Hearts, Rank.Ten),
            }));
            Assert.IsFalse(HandEvaluator.IsBlackjack(new[]
            {
                new Card(Suit.Spades, Rank.Ace), new Card(Suit.Hearts, Rank.Ace),
            }));
        }

        /// <summary>逐种子试玩最小注一局，返回首个发牌后仍处于玩家回合的对局。</summary>
        private static BlackjackGame 首个玩家回合对局()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var game = new BlackjackGame(seed);
                if (game.Deal(BlackjackRules.MinBet) && game.State == BlackjackPhase.PlayerTurn) return game;
            }

            Assert.Fail("500 个种子内未找到玩家回合开局。");
            return null!;
        }
    }
}
