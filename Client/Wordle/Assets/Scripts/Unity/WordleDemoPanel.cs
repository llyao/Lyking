using System.Text;
using Lyking.Wordle.Core;
using UnityEngine;

namespace Lyking.Wordle.Unity
{
    /// <summary>
    /// 猜单词演示面板：IMGUI 绘制统计栏、6x5 字母格、QWERTY 键盘染色、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，绿黄灰复用核心逐格判定结果，
    /// 键盘提示行按历史判定聚合染色且可点击输入，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class WordleDemoPanel : MonoBehaviour
    {
        private static readonly Color PageBackground = new Color(0.12f, 0.13f, 0.12f);
        private static readonly Color CellBorder = new Color(0.33f, 0.35f, 0.34f);
        private static readonly Color CellActiveBorder = new Color(0.78f, 0.80f, 0.78f);
        private static readonly Color CorrectColor = new Color(0.26f, 0.54f, 0.31f);
        private static readonly Color PresentColor = new Color(0.72f, 0.62f, 0.25f);
        private static readonly Color MissColor = new Color(0.34f, 0.36f, 0.37f);
        private static readonly Color KeyIdleColor = new Color(0.24f, 0.26f, 0.25f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.92f);

        private static readonly string[] KeyboardRows = { "qwertyuiop", "asdfghjkl", "zxcvbnm" };

        private WordleDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(WordleDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var game = _bootstrap.Game;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), PageBackground);
            DrawStatistics(game);
            DrawGrid(game);
            DrawKeyboard(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawStatistics(WordleGame game)
        {
            string state;
            if (game.State == WordleGameState.Won) state = "猜中获胜";
            else if (game.State == WordleGameState.Lost) state = "六次未中";
            else state = $"输入中 第 {game.AttemptCount + 1}/{WordleRules.MaxAttempts} 行";

            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {game.Round} 局    连胜 {game.WinStreak}    状态 {state}", StatusStyle());
        }

        /// <summary>绘制 6 行 x 5 列字母格：已提交行按判定染色，当前行高亮并画出光标下划线。</summary>
        private void DrawGrid(WordleGame game)
        {
            float cell = CellSize();
            float gridWidth = cell * WordleRules.WordLength;
            var origin = new Vector2((Screen.width - gridWidth) * 0.5f, 46f);

            for (int row = 0; row < WordleRules.MaxAttempts; row++)
            {
                bool submitted = row < game.Guesses.Count;
                bool current = !game.IsFinished && row == game.Guesses.Count;
                for (int col = 0; col < WordleRules.WordLength; col++)
                {
                    char letter = current && col < game.CurrentGuess.Length ? game.CurrentGuess[col] : '\0';
                    LetterHint? hint = submitted ? game.Hints[row][col] : (LetterHint?)null;
                    DrawCell(game, CellRect(origin, cell, row, col), letter, hint, current, col);
                }
            }
        }

        private void DrawCell(WordleGame game, Rect rect, char letter, LetterHint? hint, bool current, int col)
        {
            if (hint != null)
            {
                Fill(rect, HintColor(hint.Value));
            }
            else
            {
                // 空格画双层矩形形成边框：当前行用高亮边色。
                Fill(rect, current ? CellActiveBorder : CellBorder);
                Fill(Shrink(rect, 2f), PageBackground);
            }

            if (letter != '\0') GUI.Label(rect, letter.ToString(), LetterStyle(Mathf.RoundToInt(rect.height * 0.55f)));

            // 光标下划线：标示当前行的下一个待输入格位。
            if (current && col == game.CurrentGuess.Length)
            {
                var cursor = new Rect(rect.x + rect.width * 0.18f, rect.yMax - rect.height * 0.14f,
                    rect.width * 0.64f, 3f);
                Fill(cursor, TextColor);
            }
        }

        /// <summary>绘制 QWERTY 三行提示键盘：按历史判定聚合染色，点击键位即输入。</summary>
        private void DrawKeyboard(WordleGame game)
        {
            float keyWidth = Mathf.Min(46f, (Screen.width - 48f) / KeyboardRows[0].Length);
            float keyHeight = keyWidth * 0.72f;
            float top = 46f + CellSize() * WordleRules.MaxAttempts + 16f;
            var click = Event.current;

            for (int row = 0; row < KeyboardRows.Length; row++)
            {
                string letters = KeyboardRows[row];
                float x = (Screen.width - letters.Length * keyWidth) * 0.5f;
                for (int i = 0; i < letters.Length; i++)
                {
                    var rect = new Rect(x + i * keyWidth, top + row * keyHeight, keyWidth - 3f, keyHeight - 3f);
                    LetterHint? hint = AggregateHint(game, letters[i]);
                    Fill(rect, hint != null ? HintColor(hint.Value) : KeyIdleColor);
                    GUI.Label(rect, letters[i].ToString(), LetterStyle(Mathf.RoundToInt(keyHeight * 0.5f)));

                    if (_bootstrap != null && click != null && click.type == EventType.MouseDown && click.button == 0
                        && rect.Contains(click.mousePosition) && !game.IsFinished)
                    {
                        _bootstrap.TypeLetter(letters[i]);
                        click.Use();
                    }
                }
            }
        }

        /// <summary>聚合历史判定作为键盘染色：Correct 优先于 Present、Present 优先于 Miss，无信息为 null。</summary>
        private static LetterHint? AggregateHint(WordleGame game, char letter)
        {
            LetterHint? best = null;
            for (int row = 0; row < game.Guesses.Count; row++)
            {
                string guess = game.Guesses[row];
                var hints = game.Hints[row];
                for (int i = 0; i < guess.Length; i++)
                {
                    if (guess[i] != letter) continue;
                    if (best == null || hints[i] > best.Value) best = hints[i];
                }
            }

            return best;
        }

        private void DrawEventLog(WordleGame game)
        {
            var events = game.Events;
            int count = Mathf.Min(3, events.Count);
            var builder = new StringBuilder();
            builder.Append("事件日志: ");
            for (int i = events.Count - count; i < events.Count; i++)
            {
                if (i > events.Count - count) builder.Append("    ");
                builder.Append(events[i].ToString());
            }

            GUI.Label(new Rect(12f, Screen.height - 32f, Screen.width - 24f, 24f), builder.ToString(), LogStyle());
        }

        private void DrawOverlay(WordleGame game)
        {
            if (!game.IsFinished) return;

            string title = game.State == WordleGameState.Won ? "你赢了！" : "六次未中";
            GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.36f + 48f, Screen.width, 28f),
                $"答案是 {game.Answer}    当前连胜 {game.WinStreak}    按 R 再来一局", StatusStyle());
        }

        /// <summary>把行列换算为字母格矩形：行优先布局，(0,0) 位于左上格。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int col)
        {
            const float inset = 3f;
            return new Rect(origin.x + col * cell + inset, origin.y + row * cell + inset,
                cell - inset * 2f, cell - inset * 2f);
        }

        private static float CellSize()
        {
            float byWidth = (Screen.width - 60f) / WordleRules.WordLength;
            float byHeight = (Screen.height - 300f) / WordleRules.MaxAttempts;
            return Mathf.Clamp(Mathf.Min(byWidth, byHeight), 36f, 68f);
        }

        private static Color HintColor(LetterHint hint)
        {
            if (hint == LetterHint.Correct) return CorrectColor;
            return hint == LetterHint.Present ? PresentColor : MissColor;
        }

        private static Rect Shrink(Rect rect, float inset)
        {
            return new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
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

        private static GUIStyle LetterStyle(int fontSize)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, fontSize), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
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
