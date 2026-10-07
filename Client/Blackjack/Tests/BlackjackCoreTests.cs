using System;
using System.Collections.Generic;
using Lyking.Blackjack.Core;
using Xunit;

namespace Lyking.Blackjack.Core.Tests
{
    /// <summary>21 点核心规则测试：牌堆与洗牌确定性、点数估值、状态机推进、结算赔付与筹码事件。</summary>
    public class BlackjackCoreTests
    {
        [Fact]
        public void 牌堆生成52张且同种子洗牌顺序完全一致()
        {
            Assert.Equal(BlackjackRules.DeckCardCount, BlackjackRules.CreateDeck().Count);

            var first = new Shoe(2026);
            var second = new Shoe(2026);
            var seen = new HashSet<Card>();
            for (int i = 0; i < BlackjackRules.DeckCardCount; i++)
            {
                Card card = first.Draw();
                Assert.Equal(card, second.Draw());
                Assert.True(seen.Add(card), $"第 {i + 1} 张牌重复：{card}。");
            }

            Assert.Equal(BlackjackRules.DeckCardCount, seen.Count);
            Assert.Throws<InvalidOperationException>(() => first.Draw());
        }

        [Fact]
        public void 发牌顺序为玩家庄家玩家庄家()
        {
            var game = new BlackjackGame(0);
            var shoe = new Shoe(0);

            Assert.True(game.Deal(BlackjackRules.MinBet));

            // 同种子牌堆逐张比对：第一张给玩家、第二张给庄家，交替各两张。
            Assert.Equal(shoe.Draw(), game.PlayerCards[0]);
            Assert.Equal(shoe.Draw(), game.DealerCards[0]);
            Assert.Equal(shoe.Draw(), game.PlayerCards[1]);
            Assert.Equal(shoe.Draw(), game.DealerCards[1]);
            Assert.Equal(2, game.PlayerCards.Count);
            Assert.Equal(2, game.DealerCards.Count);
            Assert.Equal(BlackjackEventType.BetPlaced, game.Events[0].Type);
            Assert.Equal(BlackjackEventType.CardsDealt, game.Events[1].Type);
        }

        [Fact]
        public void 点数计算JQK均记为10点()
        {
            Assert.Equal(15, Value(Jack(), Five()));
            Assert.Equal(15, Value(Queen(), Five()));
            Assert.Equal(15, Value(King(), Five()));
            Assert.Equal(20, Value(Ten(), Ten()));
        }

        [Fact]
        public void A先按11计超出21逐张降为1()
        {
            Assert.Equal(20, Value(Ace(), Nine()));
            // A+9+9：总和 29 超出，一张 A 降为 1 后 19。
            Assert.Equal(19, Value(Ace(), Nine(), Nine()));
            // A+A+9：一张 A 保持 11、一张降为 1，恰为 21。
            Assert.Equal(21, Value(Ace(), Ace(), Nine()));
            Assert.Equal(13, Value(Ace(), Ace(), Ace()));
            // A+K 软 21；再补 9 后 A 降为 1 变硬 20。
            Assert.Equal(21, Value(Ace(), King()));
            Assert.Equal(20, Value(Ace(), King(), Nine()));
            Assert.True(HandEvaluator.IsBust(new[] { King(), Queen(), Nine() }));
        }

        [Fact]
        public void 首两张21判定天牌其余21点不算()
        {
            Assert.True(HandEvaluator.IsBlackjack(new[] { Ace(), King(Suit.Hearts) }));
            Assert.True(HandEvaluator.IsBlackjack(new[] { Ten(), Ace(Suit.Clubs) }));
            // 三张凑成 21 不是天牌，AA 也不是。
            Assert.False(HandEvaluator.IsBlackjack(new[] { Ten(), Queen(), Ace() }));
            Assert.False(HandEvaluator.IsBlackjack(new[] { Ace(), Ace() }));
        }

        [Fact]
        public void 玩家要牌超21即爆牌判负()
        {
            var game = 首个玩家回合对局();

            int guard = 0;
            while (game.State == BlackjackPhase.PlayerTurn)
            {
                Assert.True(game.Hit());
                guard++;
                Assert.True(guard <= 30, "有限牌堆下手牌必然爆牌，循环不应超过 30 次。");
            }

            Assert.Equal(BlackjackPhase.Settled, game.State);
            Assert.Equal(BlackjackOutcome.PlayerBusted, game.Outcome);
            Assert.Equal(-BlackjackRules.MinBet, game.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips - BlackjackRules.MinBet, game.Chips);
            Assert.Equal(BlackjackEventType.PlayerBusted, game.Events[game.Events.Count - 2].Type);
            Assert.Equal(BlackjackEventType.Settled, game.Events[game.Events.Count - 1].Type);
        }

        [Fact]
        public void 玩家停牌后庄家不足17必须要牌到位即停()
        {
            var game = 首个玩家回合对局();

            Assert.True(game.Stand());

            Assert.Equal(BlackjackPhase.Settled, game.State);
            if (!HandEvaluator.IsBust(game.DealerCards))
            {
                Assert.True(game.DealerValue >= BlackjackRules.DealerStandValue, "庄家未爆牌时必须停在 17 点及以上。");
            }

            // 庄家每次不足 17 补一张，补牌次数应恰为手牌数减去初始两张。
            Assert.Equal(game.DealerCards.Count - 2, CountEvents(game, BlackjackEventType.DealerHit));
        }

        [Fact]
        public void 庄家爆牌玩家获胜()
        {
            var game = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.DealerBusted, "500 个种子内未出现庄家爆牌。");

            Assert.True(HandEvaluator.IsBust(game.DealerCards));
            Assert.Equal(BlackjackEventType.DealerBusted, game.Events[game.Events.Count - 2].Type);
            Assert.Equal(BlackjackRules.MinBet, game.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips + BlackjackRules.MinBet, game.Chips);
        }

        [Fact]
        public void 玩家天牌按3比2赔付()
        {
            var game = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.PlayerBlackjack, "500 个种子内未出现玩家天牌。");
            int bet = BlackjackRules.MinBet;

            Assert.True(HandEvaluator.IsBlackjack(game.PlayerCards));
            Assert.Equal(BlackjackPhase.Settled, game.State);
            Assert.False(game.IsDealerHoleCardHidden);
            // 下注 50：发牌扣 50，结算返还 50 本金 + 75 净赢（3:2）。
            Assert.Equal(bet * BlackjackRules.BlackjackPayoutNumerator / BlackjackRules.BlackjackPayoutDenominator, game.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips + bet * BlackjackRules.BlackjackPayoutNumerator / BlackjackRules.BlackjackPayoutDenominator, game.Chips);
        }

        [Fact]
        public void 庄家天牌玩家输注()
        {
            var game = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.DealerBlackjack, "500 个种子内未出现庄家天牌。");

            Assert.True(HandEvaluator.IsBlackjack(game.DealerCards));
            Assert.False(HandEvaluator.IsBlackjack(game.PlayerCards));
            Assert.Equal(-BlackjackRules.MinBet, game.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips - BlackjackRules.MinBet, game.Chips);
        }

        [Fact]
        public void 比点平局退注筹码复原()
        {
            var game = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.Push, "500 个种子内未出现平局。");

            Assert.Equal(game.PlayerValue, game.DealerValue);
            Assert.False(HandEvaluator.IsBust(game.PlayerCards));
            Assert.Equal(0, game.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips, game.Chips);
        }

        [Fact]
        public void 比点大小决定胜负与筹码结算()
        {
            var playerWin = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.PlayerWins, "500 个种子内未出现玩家比点获胜。");
            Assert.True(playerWin.PlayerValue > playerWin.DealerValue);
            Assert.True(playerWin.DealerValue >= BlackjackRules.DealerStandValue, "庄家比点落败前应已补牌到 17 点及以上。");
            Assert.Equal(BlackjackRules.MinBet, playerWin.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips + BlackjackRules.MinBet, playerWin.Chips);

            var dealerWin = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.DealerWins, "500 个种子内未出现庄家比点获胜。");
            Assert.True(dealerWin.PlayerValue < dealerWin.DealerValue);
            Assert.Equal(-BlackjackRules.MinBet, dealerWin.SettleAmount);
            Assert.Equal(BlackjackRules.InitialChips - BlackjackRules.MinBet, dealerWin.Chips);
        }

        [Fact]
        public void 低于最小注或超出筹码拒绝下注()
        {
            var game = new BlackjackGame(0);

            Assert.False(game.Deal(BlackjackRules.MinBet - 1));
            Assert.False(game.Deal(0));
            Assert.False(game.Deal(BlackjackRules.InitialChips + 1));
            Assert.Equal(BlackjackPhase.Betting, game.State);
            Assert.Equal(BlackjackRules.InitialChips, game.Chips);
            Assert.Empty(game.Events);

            // 全押恰好等于筹码时允许。
            Assert.True(new BlackjackGame(0).Deal(BlackjackRules.InitialChips));
            // 筹码不足最小注时无法开局。
            Assert.False(new BlackjackGame(0, BlackjackRules.MinBet - 1).Deal(BlackjackRules.MinBet));

            Assert.True(game.Deal(BlackjackRules.MinBet));
            Assert.Equal(BlackjackRules.InitialChips - BlackjackRules.MinBet, game.Chips);
            Assert.Equal(BlackjackRules.MinBet, game.Bet);
            Assert.Equal(BlackjackRules.MinBet, game.Events[0].Amount);
        }

        [Fact]
        public void 加倍翻倍赌注只补一张后强制停牌()
        {
            var game = 首个玩家回合对局();
            int bet = BlackjackRules.MinBet;

            Assert.True(game.Double());

            Assert.Equal(bet * 2, game.Bet);
            Assert.Equal(3, game.PlayerCards.Count);
            Assert.Equal(BlackjackPhase.Settled, game.State);
            Assert.Contains(game.Events, e => e.Type == BlackjackEventType.Doubled && e.Amount == bet * 2);
            // 结算恒等式：筹码 = 初始 - 两次扣注 + 赌注返还 + 净输赢。
            Assert.Equal(BlackjackRules.InitialChips - bet * 2 + game.Bet + game.SettleAmount, game.Chips);
            Assert.True(game.SettleAmount == game.Bet || game.SettleAmount == 0 || game.SettleAmount == -game.Bet, "加倍局结算额只能是翻倍后赌注的赢、平、输。");
            Assert.False(game.Hit(), "加倍后强制停牌，不允许继续要牌。");
        }

        [Fact]
        public void 超过两张后不可加倍()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var game = new BlackjackGame(seed);
                Assert.True(game.Deal(BlackjackRules.MinBet));
                if (game.State != BlackjackPhase.PlayerTurn) continue;

                Assert.True(game.Hit());
                if (game.State != BlackjackPhase.PlayerTurn) continue; // 该次要牌爆牌则换种子

                Assert.False(game.Double(), "三张牌不应允许加倍。");
                Assert.Equal(BlackjackPhase.PlayerTurn, game.State);
                return;
            }

            Assert.Fail("500 个种子内未找到可验证的加倍拒绝场景。");
        }

        [Fact]
        public void 结算后开下一局复位手牌且筹码保留()
        {
            var game = 首个满足条件的对局(g => g.Outcome == BlackjackOutcome.DealerWins, "500 个种子内未出现庄家比点获胜。");
            int chipsAfterSettle = game.Chips;
            Assert.Equal(BlackjackRules.InitialChips - BlackjackRules.MinBet, chipsAfterSettle);

            Assert.True(game.NextRound());

            Assert.Equal(2, game.Round);
            Assert.Equal(BlackjackPhase.Betting, game.State);
            Assert.Equal(BlackjackOutcome.None, game.Outcome);
            Assert.Equal(0, game.Bet);
            Assert.Equal(0, game.SettleAmount);
            Assert.Empty(game.PlayerCards);
            Assert.Empty(game.DealerCards);
            Assert.Equal(chipsAfterSettle, game.Chips);
            Assert.Equal(BlackjackEventType.RoundStarted, game.Events[game.Events.Count - 1].Type);
            Assert.False(game.NextRound(), "未结算时不能重复开下一局。");
        }

        [Fact]
        public void 牌堆不足15张时下局发牌前自动重洗()
        {
            var game = new BlackjackGame(0);
            while (game.ShoeRemainingCards >= BlackjackRules.ReshuffleThreshold && game.Chips >= BlackjackRules.MinBet)
            {
                if (game.State == BlackjackPhase.Settled) Assert.True(game.NextRound());
                Assert.True(game.Deal(BlackjackRules.MinBet));
                if (game.State == BlackjackPhase.PlayerTurn) Assert.True(game.Stand());
            }

            Assert.True(game.ShoeRemainingCards < BlackjackRules.ReshuffleThreshold, "循环应把牌堆消耗到重洗阈值以下。");
            Assert.True(game.Chips >= BlackjackRules.MinBet, "验证过程中筹码不应耗尽。");
            int reshuffles = CountEvents(game, BlackjackEventType.ShoeReshuffled);

            Assert.True(game.NextRound());
            Assert.True(game.Deal(BlackjackRules.MinBet));

            Assert.Equal(BlackjackRules.DeckCardCount - 4, game.ShoeRemainingCards);
            Assert.Equal(reshuffles + 1, CountEvents(game, BlackjackEventType.ShoeReshuffled));
        }

        [Fact]
        public void 事件日志封顶128并裁剪最早事件()
        {
            var game = new BlackjackGame(0);
            for (int round = 0; round < 500 && game.Chips >= BlackjackRules.MinBet; round++)
            {
                if (game.State == BlackjackPhase.Settled) Assert.True(game.NextRound());
                Assert.True(game.Deal(BlackjackRules.MinBet));
                if (game.State == BlackjackPhase.PlayerTurn) Assert.True(game.Stand());
                if (game.Events.Count >= 128 && game.Events[0].Round > 1) break;
            }

            Assert.Equal(128, game.Events.Count);
            Assert.True(game.Events[0].Round > 1, "封顶裁剪后最早事件不应属于第 1 局。");
        }

        [Fact]
        public void 同种子对局操作序列完全可复现()
        {
            var first = new BlackjackGame(42);
            var second = new BlackjackGame(42);

            按脚本试玩一手(first);
            按脚本试玩一手(second);

            Assert.Equal(first.Chips, second.Chips);
            Assert.Equal(first.Round, second.Round);
            Assert.Equal(first.State, second.State);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.SettleAmount, second.SettleAmount);
            Assert.Equal(first.PlayerCards, second.PlayerCards);
            Assert.Equal(first.DealerCards, second.DealerCards);
            Assert.Equal(first.Events, second.Events);
            Assert.Equal(first.ShoeRemainingCards, second.ShoeRemainingCards);
        }

        /// <summary>点数估值助手。</summary>
        private static int Value(params Card[] cards)
        {
            return HandEvaluator.BestValue(cards);
        }

        private static Card Ace(Suit suit = Suit.Spades) => new Card(suit, Rank.Ace);

        private static Card Five(Suit suit = Suit.Hearts) => new Card(suit, Rank.Five);

        private static Card Nine(Suit suit = Suit.Diamonds) => new Card(suit, Rank.Nine);

        private static Card Ten(Suit suit = Suit.Hearts) => new Card(suit, Rank.Ten);

        private static Card Jack(Suit suit = Suit.Spades) => new Card(suit, Rank.Jack);

        private static Card Queen(Suit suit = Suit.Diamonds) => new Card(suit, Rank.Queen);

        private static Card King(Suit suit = Suit.Clubs) => new Card(suit, Rank.King);

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

        /// <summary>逐种子以最小注开局并立即停牌，返回首个满足条件的已结算对局。</summary>
        private static BlackjackGame 首个满足条件的对局(Func<BlackjackGame, bool> predicate, string message)
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var game = new BlackjackGame(seed);
                Assert.True(game.Deal(BlackjackRules.MinBet));
                if (game.State == BlackjackPhase.PlayerTurn) Assert.True(game.Stand());
                if (predicate(game)) return game;
            }

            Assert.Fail(message);
            return null!;
        }

        /// <summary>固定脚本：下注 100 后视状态要牌、停牌，用于双对局比对。</summary>
        private static void 按脚本试玩一手(BlackjackGame game)
        {
            Assert.True(game.Deal(100));
            if (game.State == BlackjackPhase.PlayerTurn)
            {
                Assert.True(game.Hit());
                if (game.State == BlackjackPhase.PlayerTurn) Assert.True(game.Stand());
            }
        }

        private static int CountEvents(BlackjackGame game, BlackjackEventType type)
        {
            int count = 0;
            foreach (var entry in game.Events)
            {
                if (entry.Type == type) count++;
            }

            return count;
        }
    }
}
