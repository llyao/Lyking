using Lyking.Pong.Core;
using UnityEngine;

namespace Lyking.Pong.Unity
{
    /// <summary>
    /// 乒乓演示面板：IMGUI 绘制状态栏、顶部比分、场地（中线虚线）、挡板、球、胜利提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class PongDemoPanel : MonoBehaviour
    {
        private static readonly Color FieldBackground = new Color(0.13f, 0.15f, 0.18f);
        private static readonly Color FieldBorder = new Color(0.32f, 0.38f, 0.45f);
        private static readonly Color CenterLineColor = new Color(0.45f, 0.50f, 0.55f);
        private static readonly Color PaddleColor = new Color(0.92f, 0.92f, 0.88f);
        private static readonly Color BallColor = new Color(0.98f, 0.85f, 0.35f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f);

        private PongDemoBootstrap? _bootstrap;

        public void Bind(PongDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;

            var game = _bootstrap.Game;
            var lifecycle = game.State == PongState.LeftWon ? "左方胜"
                : game.State == PongState.RightWon ? "右方胜" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"左方 {game.LeftScore} : {game.RightScore} 右方    球速 {game.BallSpeed:0.0}    状态 {lifecycle}",
                StatusStyle());
            GUI.Label(new Rect(0f, 36f, Screen.width, 40f),
                $"{game.LeftScore}  :  {game.RightScore}", ScoreStyle());

            DrawField(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawField(PongGame game)
        {
            var rules = game.Rules;
            float scale = Mathf.Min((Screen.height - 170f) / rules.FieldHeight, (Screen.width - 80f) / rules.FieldWidth);
            float fieldWidth = scale * rules.FieldWidth;
            float fieldHeight = scale * rules.FieldHeight;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 24f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), FieldBackground);

            // 中线虚线：沿高度画一段空一段
            float dashHeight = fieldHeight / rules.FieldHeight;
            for (int i = 0; i < (int)rules.FieldHeight; i += 2)
            {
                Fill(new Rect(origin.x + fieldWidth * 0.5f - 2f, origin.y + i * dashHeight, 4f, dashHeight), CenterLineColor);
            }

            var paddleSize = new Vec2(rules.PaddleWidth, rules.PaddleHeight);
            Fill(CenterRect(origin, scale, rules.FieldHeight, game.LeftPaddlePosition, paddleSize), PaddleColor);
            Fill(CenterRect(origin, scale, rules.FieldHeight, game.RightPaddlePosition, paddleSize), PaddleColor);
            Fill(CenterRect(origin, scale, rules.FieldHeight, game.BallPosition,
                new Vec2(rules.BallRadius * 2f, rules.BallRadius * 2f)), BallColor);
        }

        /// <summary>世界坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="center"/> 为矩形中心。</summary>
        private static Rect CenterRect(Vector2 origin, float scale, float fieldHeight, Vec2 center, Vec2 size)
        {
            float left = origin.x + (center.X - size.X * 0.5f) * scale;
            float top = origin.y + (fieldHeight - center.Y - size.Y * 0.5f) * scale;
            return new Rect(left, top, size.X * scale, size.Y * scale);
        }

        private void DrawEventLog(PongGame game)
        {
            var events = game.Events;
            int count = Mathf.Min(6, events.Count);
            var builder = new System.Text.StringBuilder();
            builder.Append("事件日志: ");
            for (int i = events.Count - count; i < events.Count; i++)
            {
                if (i > events.Count - count) builder.Append("    ");
                builder.Append(events[i].ToString());
            }

            GUI.Label(new Rect(12f, Screen.height - 34f, Screen.width - 24f, 24f), builder.ToString(), LogStyle());
        }

        private void DrawOverlay(PongGame game)
        {
            if (!game.IsFinished) return;

            string title = game.State == PongState.LeftWon ? "左方获胜！" : "右方获胜！";
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终比分 {game.LeftScore} : {game.RightScore}    按 R 重新开始", StatusStyle());
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
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle ScoreStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle TitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
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
