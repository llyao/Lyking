using System.Collections.Generic;
using System.Text;
using Lyking.Blackjack.Core;
using UnityEngine;

namespace Lyking.Blackjack.Unity
{
    /// <summary>
    /// 21 点演示面板：IMGUI 绘制顶部状态、庄家与玩家手牌、下注按钮、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 纯色矩形 + 文字，
    /// 花色用字母表示避开字体缺字（S=黑桃、H=红心、D=方块、C=梅花），
    /// 庄家暗牌在玩家回合绘制为背面色块，下注按钮点击直接驱动核心，不依赖物理引擎。
    /// </summary>
    public sealed class BlackjackDemoPanel : MonoBehaviour
    {
        private const float CardWidth = 64f;
        private const float CardHeight = 92f;
        private const float CardGap = 10f;

        private static readonly Color TableBackground = new Color(0.10f, 0.24f, 0.14f);
        private static readonly Color CardFace = new Color(0.96f, 0.95f, 0.88f);
        private static readonly Color CardBorder = new Color(0.25f, 0.25f, 0.22f);
        private static readonly Color CardBack = new Color(0.55f, 0.22f, 0.22f);
        private static readonly Color RedSuit = new Color(0.75f, 0.16f, 0.16f);
        private static readonly Color BlackSuit = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private BlackjackDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(BlackjackDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var game = _bootstrap.Game;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), TableBackground);
            DrawStatus(game);
            DrawHands(game);
            DrawBetButtons(game);
            DrawHint(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawStatus(BlackjackGame game)
        {
            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {game.Round} 局    筹码 {game.Chips}    赌注 {game.Bet}    {DescribeState(game)}",
                StatusStyle());
        }

        private static string DescribeState(BlackjackGame game)
        {
            switch (game.State)
            {
                case BlackjackPhase.Betting:
                    return game.Chips >= BlackjackRules.MinBet ? "请下注" : "筹码不足，无法下注";
                case BlackjackPhase.PlayerTurn:
                    return $"你的回合，{game.PlayerValue} 点";
                case BlackjackPhase.DealerTurn:
                    return "庄家回合";
                case BlackjackPhase.Settled:
                    return DescribeOutcome(game);
                default:
                    return game.State.ToString();
            }
        }

        private static string DescribeOutcome(BlackjackGame game)
        {
            switch (game.Outcome)
            {
                case BlackjackOutcome.PlayerBlackjack:
                    return $"天牌！净赢 {game.SettleAmount}";
                case BlackjackOutcome.DealerBusted:
                    return $"庄家爆牌，净赢 {game.SettleAmount}";
                case BlackjackOutcome.PlayerWins:
                    return $"比点获胜，净赢 {game.SettleAmount}";
                case BlackjackOutcome.DealerBlackjack:
                case BlackjackOutcome.PlayerBusted:
                case BlackjackOutcome.DealerWins:
                    return $"庄家胜，输 {-game.SettleAmount}";
                case BlackjackOutcome.Push:
                    return "平局，退回赌注";
                default:
                    return "本局已结算";
            }
        }

        private void DrawHands(BlackjackGame game)
        {
            if (game.State == BlackjackPhase.Betting) return;

            bool holeHidden = game.IsDealerHoleCardHidden;
            string dealerTitle = holeHidden
                ? $"庄家 {VisibleDealerValue(game)} 点 + 暗牌"
                : $"庄家 {game.DealerValue} 点";
            DrawHand(game.DealerCards, holeHidden, 56f, dealerTitle);
            DrawHand(game.PlayerCards, false, Mathf.RoundToInt(Screen.height * 0.56f), $"你 {game.PlayerValue} 点");
        }

        /// <summary>暗牌阶段庄家点数只按明牌（第一张）计。</summary>
        private static int VisibleDealerValue(BlackjackGame game)
        {
            return HandEvaluator.BestValue(new[] { game.DealerCards[0] });
        }

        private void DrawHand(IReadOnlyList<Card> cards, bool hideHole, float top, string title)
        {
            GUI.Label(new Rect(0f, top, Screen.width, 24f), title, HandTitleStyle());
            float totalWidth = cards.Count * CardWidth + (cards.Count - 1) * CardGap;
            float x = (Screen.width - totalWidth) * 0.5f;
            for (int i = 0; i < cards.Count; i++)
            {
                var rect = new Rect(x + i * (CardWidth + CardGap), top + 26f, CardWidth, CardHeight);
                if (hideHole && i == 1) DrawCardBack(rect);
                else DrawCard(rect, cards[i]);
            }
        }

        private void DrawCard(Rect rect, Card card)
        {
            Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), CardBorder);
            Fill(rect, CardFace);
            bool red = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;
            GUI.Label(rect, $"{SuitLetter(card.Suit)} {RankLetter(card.Rank)}", CardStyle(red ? RedSuit : BlackSuit));
        }

        private void DrawCardBack(Rect rect)
        {
            Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), CardBorder);
            Fill(rect, CardBack);
        }

        /// <summary>花色字母映射：S=黑桃、H=红心、D=方块、C=梅花。</summary>
        private static string SuitLetter(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return "S";
                case Suit.Hearts: return "H";
                case Suit.Diamonds: return "D";
                default: return "C";
            }
        }

        private static string RankLetter(Rank rank)
        {
            switch (rank)
            {
                case Rank.Ace: return "A";
                case Rank.Jack: return "J";
                case Rank.Queen: return "Q";
                case Rank.King: return "K";
                default: return ((int)rank).ToString();
            }
        }

        private void DrawBetButtons(BlackjackGame game)
        {
            if (game.State != BlackjackPhase.Betting || _bootstrap == null) return;

            int[] amounts = { BlackjackRules.MinBet, BlackjackRules.MinBet * 2, BlackjackRules.MinBet * 4 };
            float buttonWidth = 110f;
            float buttonHeight = 40f;
            float spacing = 12f;
            float y = Screen.height * 0.5f - buttonHeight * 0.5f;
            float x = (Screen.width - ((buttonWidth + spacing) * (amounts.Length + 1) - spacing)) * 0.5f;

            for (int i = 0; i < amounts.Length; i++)
            {
                if (DrawBetButton(new Rect(x + i * (buttonWidth + spacing), y, buttonWidth, buttonHeight),
                        $"下注 {amounts[i]}", amounts[i] <= game.Chips))
                {
                    _bootstrap.Deal(amounts[i]);
                }
            }

            if (DrawBetButton(new Rect(x + amounts.Length * (buttonWidth + spacing), y, buttonWidth, buttonHeight),
                    "全押", game.Chips >= BlackjackRules.MinBet))
            {
                _bootstrap.Deal(game.Chips);
            }
        }

        private static bool DrawBetButton(Rect rect, string label, bool affordable)
        {
            GUI.enabled = affordable;
            bool clicked = GUI.Button(rect, label, ButtonStyle());
            GUI.enabled = true;
            return clicked;
        }

        private void DrawHint(BlackjackGame game)
        {
            string hint;
            switch (game.State)
            {
                case BlackjackPhase.Betting:
                    hint = "点击按钮下注    R 重开一局";
                    break;
                case BlackjackPhase.PlayerTurn:
                    hint = "H 要牌    S 停牌    D 加倍    R 重开一局";
                    break;
                default:
                    hint = "按 R 再来一局";
                    break;
            }

            GUI.Label(new Rect(12f, Screen.height - 58f, Screen.width - 24f, 22f), hint, HintStyle());
        }

        private void DrawEventLog(BlackjackGame game)
        {
            var events = game.Events;
            int count = Mathf.Min(6, events.Count);
            var builder = new StringBuilder();
            builder.Append("事件日志: ");
            for (int i = events.Count - count; i < events.Count; i++)
            {
                if (i > events.Count - count) builder.Append("    ");
                builder.Append(events[i].ToString());
            }

            GUI.Label(new Rect(12f, Screen.height - 34f, Screen.width - 24f, 24f), builder.ToString(), LogStyle());
        }

        private void DrawOverlay(BlackjackGame game)
        {
            if (game.State != BlackjackPhase.Settled) return;

            string title;
            switch (game.Outcome)
            {
                case BlackjackOutcome.PlayerBlackjack:
                    title = "天牌！你赢了";
                    break;
                case BlackjackOutcome.DealerBusted:
                case BlackjackOutcome.PlayerWins:
                    title = "你赢了！";
                    break;
                case BlackjackOutcome.Push:
                    title = "平局";
                    break;
                default:
                    title = "庄家获胜";
                    break;
            }

            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"筹码 {game.Chips}    本局输赢 {game.SettleAmount:+0;-0;0}    按 R 再来一局",
                StatusStyle());
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private static GUIStyle StatusStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle TitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle HandTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle CardStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle ButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            return style;
        }

        private static GUIStyle HintStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
            return style;
        }

        private static GUIStyle LogStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
            return style;
        }
    }
}
