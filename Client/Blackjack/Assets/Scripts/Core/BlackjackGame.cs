using System.Collections.Generic;

namespace Lyking.Blackjack.Core
{
    /// <summary>
    /// 21 点对局状态机：下注发牌（玩家-庄家-玩家-庄家）→ 玩家回合（要牌/加倍/停牌）
    /// → 庄家回合（补牌到 17 点及以上）→ 结算（天牌 3:2、爆牌、比点、平局退注）。
    /// 筹码在下注时扣减、结算时返还，跨局保留；事件日志封顶 128 条裁剪；
    /// 牌堆不足 15 张时下一局发牌前整副重洗，全部随机性来自注入种子的 <see cref="Shoe"/>。
    /// </summary>
    public sealed class BlackjackGame
    {
        private const int MaxEventLogs = 128;

        private readonly Shoe _shoe;
        private readonly List<Card> _playerCards = new List<Card>();
        private readonly List<Card> _dealerCards = new List<Card>();
        private readonly List<BlackjackEventLog> _events = new List<BlackjackEventLog>();

        public BlackjackGame(int seed, int chips = BlackjackRules.InitialChips)
        {
            _shoe = new Shoe(seed);
            Chips = chips;
        }

        /// <summary>当前局数（从 1 起），每次开下一局加一。</summary>
        public int Round { get; private set; } = 1;

        /// <summary>当前筹码：下注即扣减，结算时按结果返还，跨局与重开保留。</summary>
        public int Chips { get; private set; }

        /// <summary>本局赌注；加倍后翻倍。</summary>
        public int Bet { get; private set; }

        public BlackjackPhase State { get; private set; } = BlackjackPhase.Betting;

        public BlackjackOutcome Outcome { get; private set; } = BlackjackOutcome.None;

        /// <summary>本局结算净输赢：正为赢、负为输、零为平局退注；进行中为 0。</summary>
        public int SettleAmount { get; private set; }

        public IReadOnlyList<Card> PlayerCards => _playerCards;

        public IReadOnlyList<Card> DealerCards => _dealerCards;

        public IReadOnlyList<BlackjackEventLog> Events => _events;

        /// <summary>牌堆剩余张数；低于重洗阈值时下一局发牌前整副重洗。</summary>
        public int ShoeRemainingCards => _shoe.RemainingCards;

        /// <summary>玩家手牌最优点数。</summary>
        public int PlayerValue => HandEvaluator.BestValue(_playerCards);

        /// <summary>庄家手牌最优点数（含暗牌，表现层在玩家回合应自行隐藏）。</summary>
        public int DealerValue => HandEvaluator.BestValue(_dealerCards);

        /// <summary>庄家暗牌在玩家回合内保持背面朝上，进入庄家回合或结算后揭开。</summary>
        public bool IsDealerHoleCardHidden => State == BlackjackPhase.PlayerTurn;

        /// <summary>下注并发牌；仅在下注阶段、注额不小于最小注且不超过当前筹码时成功。</summary>
        public bool Deal(int bet)
        {
            if (State != BlackjackPhase.Betting) return false;
            if (bet < BlackjackRules.MinBet || bet > Chips) return false;

            if (_shoe.RemainingCards < BlackjackRules.ReshuffleThreshold)
            {
                _shoe.Reshuffle();
                Log(BlackjackEventType.ShoeReshuffled, 0);
            }

            Chips -= bet;
            Bet = bet;
            Log(BlackjackEventType.BetPlaced, bet);

            _playerCards.Clear();
            _dealerCards.Clear();
            _playerCards.Add(DrawCard());
            _dealerCards.Add(DrawCard());
            _playerCards.Add(DrawCard());
            _dealerCards.Add(DrawCard());
            Log(BlackjackEventType.CardsDealt, 0);

            // 任一方首两张即 21 点立即结算：双方天牌平局退注，玩家天牌 3:2，庄家天牌玩家输注。
            if (HandEvaluator.IsBlackjack(_playerCards) && HandEvaluator.IsBlackjack(_dealerCards))
            {
                Settle(BlackjackOutcome.Push, 0);
            }
            else if (HandEvaluator.IsBlackjack(_playerCards))
            {
                Settle(BlackjackOutcome.PlayerBlackjack,
                    Bet * BlackjackRules.BlackjackPayoutNumerator / BlackjackRules.BlackjackPayoutDenominator);
            }
            else if (HandEvaluator.IsBlackjack(_dealerCards))
            {
                Settle(BlackjackOutcome.DealerBlackjack, -Bet);
            }
            else
            {
                State = BlackjackPhase.PlayerTurn;
            }

            return true;
        }

        /// <summary>玩家要牌；爆牌立即判负结算。</summary>
        public bool Hit()
        {
            if (State != BlackjackPhase.PlayerTurn) return false;

            _playerCards.Add(DrawCard());
            Log(BlackjackEventType.PlayerHit, 0);
            if (HandEvaluator.IsBust(_playerCards))
            {
                Log(BlackjackEventType.PlayerBusted, 0);
                Settle(BlackjackOutcome.PlayerBusted, -Bet);
            }

            return true;
        }

        /// <summary>
        /// 玩家加倍：仅在首两张时可用，赌注翻倍后再扣一次筹码，
        /// 只补一张牌后强制停牌进入庄家回合；补牌爆牌立即判负。
        /// </summary>
        public bool Double()
        {
            if (State != BlackjackPhase.PlayerTurn) return false;
            if (_playerCards.Count != 2) return false;
            if (Chips < Bet) return false;

            Chips -= Bet;
            Bet *= 2;
            Log(BlackjackEventType.Doubled, Bet);

            _playerCards.Add(DrawCard());
            if (HandEvaluator.IsBust(_playerCards))
            {
                Log(BlackjackEventType.PlayerBusted, 0);
                Settle(BlackjackOutcome.PlayerBusted, -Bet);
                return true;
            }

            // 加倍即强制停牌：不再记录停牌事件，直接进入庄家回合。
            DealerPlay();
            return true;
        }

        /// <summary>玩家停牌，轮到庄家按规则补牌。</summary>
        public bool Stand()
        {
            if (State != BlackjackPhase.PlayerTurn) return false;

            Log(BlackjackEventType.PlayerStood, 0);
            DealerPlay();
            return true;
        }

        /// <summary>开下一局：仅在本局结算后可用，清空手牌与赌注，筹码保留。</summary>
        public bool NextRound()
        {
            if (State != BlackjackPhase.Settled) return false;

            Round++;
            Bet = 0;
            Outcome = BlackjackOutcome.None;
            SettleAmount = 0;
            _playerCards.Clear();
            _dealerCards.Clear();
            State = BlackjackPhase.Betting;
            Log(BlackjackEventType.RoundStarted, 0);
            return true;
        }

        /// <summary>庄家回合：不足 17 点必须要牌，达到后停牌并比点结算。</summary>
        private void DealerPlay()
        {
            State = BlackjackPhase.DealerTurn;
            while (HandEvaluator.BestValue(_dealerCards) < BlackjackRules.DealerStandValue)
            {
                _dealerCards.Add(DrawCard());
                Log(BlackjackEventType.DealerHit, 0);
            }

            if (HandEvaluator.IsBust(_dealerCards))
            {
                Log(BlackjackEventType.DealerBusted, 0);
                Settle(BlackjackOutcome.DealerBusted, Bet);
                return;
            }

            int player = HandEvaluator.BestValue(_playerCards);
            int dealer = HandEvaluator.BestValue(_dealerCards);
            if (player > dealer) Settle(BlackjackOutcome.PlayerWins, Bet);
            else if (player < dealer) Settle(BlackjackOutcome.DealerWins, -Bet);
            else Settle(BlackjackOutcome.Push, 0);
        }

        /// <summary>按结果结算：返还赌注与净输赢（输钱时赌注不返还），写入结算事件。</summary>
        private void Settle(BlackjackOutcome outcome, int netAmount)
        {
            Outcome = outcome;
            SettleAmount = netAmount;
            Chips += Bet + netAmount;
            State = BlackjackPhase.Settled;
            Log(BlackjackEventType.Settled, netAmount);
        }

        /// <summary>发一张牌；牌堆意外发空时（阈值内极限长手牌）整副重洗后继续。</summary>
        private Card DrawCard()
        {
            if (_shoe.RemainingCards == 0)
            {
                _shoe.Reshuffle();
                Log(BlackjackEventType.ShoeReshuffled, 0);
            }

            return _shoe.Draw();
        }

        private void Log(BlackjackEventType type, int amount)
        {
            _events.Add(new BlackjackEventLog(Round, type, amount));
            if (_events.Count > MaxEventLogs) _events.RemoveRange(0, _events.Count - MaxEventLogs);
        }
    }
}
