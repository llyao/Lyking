using Lyking.Breakout.Core;
using UnityEngine;

namespace Lyking.Breakout.Unity
{
    /// <summary>
    /// 打砖块演示面板：IMGUI 绘制状态栏、场地、砖块、挡板、球、发球与结束提示、事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class BreakoutDemoPanel : MonoBehaviour
    {
        private static readonly Color FieldBackground = new Color(0.13f, 0.15f, 0.18f);
        private static readonly Color FieldBorder = new Color(0.32f, 0.38f, 0.45f);
        private static readonly Color PaddleColor = new Color(0.92f, 0.92f, 0.88f);
        private static readonly Color BallColor = new Color(0.98f, 0.85f, 0.35f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f);
        private static readonly Color[] BrickColors =
        {
            new Color(0.90f, 0.36f, 0.30f),
            new Color(0.95f, 0.62f, 0.26f),
            new Color(0.95f, 0.86f, 0.38f),
            new Color(0.47f, 0.80f, 0.42f),
            new Color(0.42f, 0.66f, 0.90f),
        };

        private BreakoutDemoBootstrap? _bootstrap;

        public void Bind(BreakoutDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;

            var game = _bootstrap.Game;
            var lifecycle = game.IsGameOver ? "结束" : game.BallLaunched ? "进行中" : "待发球";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"分数 {game.Score}    生命 {game.Lives}    关卡 {game.Level}    球速 {game.BallSpeed:0.0}    状态 {lifecycle}",
                StatusStyle());

            DrawField(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawField(BreakoutGame game)
        {
            var rules = game.Rules;
            float scale = Mathf.Min((Screen.height - 150f) / rules.FieldHeight, (Screen.width - 80f) / rules.FieldWidth);
            float fieldWidth = scale * rules.FieldWidth;
            float fieldHeight = scale * rules.FieldHeight;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), FieldBackground);

            for (int i = 0; i < game.Bricks.Count; i++)
            {
                var brick = game.Bricks[i];
                if (!brick.IsAlive) continue;
                var rect = WorldRect(origin, scale, rules.FieldHeight, brick.Bounds.Min, brick.Bounds.Max);
                var baseColor = BrickColors[brick.Row % BrickColors.Length];
                // 耐久砖受击后调浅，与满耐久状态区分
                var full = rules.DurabilityForBrick(brick.Row);
                Fill(rect, brick.Durability < full ? Color.Lerp(baseColor, Color.white, 0.45f) : baseColor);
            }

            var paddleRect = CenterRect(origin, scale, rules.FieldHeight, game.PaddlePosition,
                new Vec2(rules.PaddleWidth, rules.PaddleHeight));
            Fill(paddleRect, PaddleColor);

            var ballRect = CenterRect(origin, scale, rules.FieldHeight, game.BallPosition,
                new Vec2(rules.BallRadius * 2f, rules.BallRadius * 2f));
            Fill(ballRect, BallColor);
        }

        /// <summary>世界坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="center"/> 为矩形中心。</summary>
        private static Rect CenterRect(Vector2 origin, float scale, float fieldHeight, Vec2 center, Vec2 size)
        {
            float left = origin.x + (center.X - size.X * 0.5f) * scale;
            float top = origin.y + (fieldHeight - center.Y - size.Y * 0.5f) * scale;
            return new Rect(left, top, size.X * scale, size.Y * scale);
        }

        /// <summary>世界坐标（Y 向上）转 IMGUI 矩形（Y 向下），输入为包围盒的上下角点。</summary>
        private static Rect WorldRect(Vector2 origin, float scale, float fieldHeight, Vec2 min, Vec2 max)
        {
            float left = origin.x + min.X * scale;
            float top = origin.y + (fieldHeight - max.Y) * scale;
            return new Rect(left, top, (max.X - min.X) * scale, (max.Y - min.Y) * scale);
        }

        private void DrawEventLog(BreakoutGame game)
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

        private void DrawOverlay(BreakoutGame game)
        {
            if (game.IsGameOver)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "游戏结束", TitleStyle());
                GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                    $"最终分数 {game.Score}    抵达关卡 {game.Level}    按 R 重新开始", StatusStyle());
                return;
            }

            if (game.BallLaunched) return;

            // 过关与失命后球回到挡板待发球；刚过关时附加关卡提示。
            var lastType = game.Events[game.Events.Count - 1].Type;
            var hint = lastType == BreakoutEventType.LevelCleared
                ? $"第 {game.Level} 关开始！    按 空格 发球"
                : "按 空格 发球";
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 28f), hint, HintStyle());
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

        private static GUIStyle TitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle HintStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.9f);
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
