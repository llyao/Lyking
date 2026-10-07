using Lyking.EndlessRunner.Core;
using UnityEngine;

namespace Lyking.EndlessRunner.Unity
{
    /// <summary>
    /// 无尽跑酷演示面板：IMGUI 绘制天空、地面、障碍、玩家、顶部状态、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与精灵。
    /// </summary>
    public sealed class EndlessRunnerDemoPanel : MonoBehaviour
    {
        private static readonly Color SkyColor = new Color(0.53f, 0.81f, 0.92f);
        private static readonly Color FieldBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color GroundColor = new Color(0.76f, 0.60f, 0.35f);
        private static readonly Color ObstacleColor = new Color(0.80f, 0.36f, 0.28f);
        private static readonly Color PlayerColor = new Color(0.25f, 0.55f, 0.90f);
        private static readonly Color TextColor = new Color(0.15f, 0.20f, 0.24f);

        private EndlessRunnerDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(EndlessRunnerDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var game = _bootstrap.Game;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), SkyColor);
            DrawField(game);
            DrawStatus(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawField(EndlessRunnerGame game)
        {
            var rules = game.Rules;
            float cell = Mathf.Min((Screen.height - 140f) / game.FieldHeight, (Screen.width - 80f) / game.FieldWidth);
            float fieldWidth = cell * game.FieldWidth;
            float fieldHeight = cell * game.FieldHeight;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), SkyColor);

            // 地面条纹画在场地底部，障碍与玩家都立在其上。
            Fill(FieldRect(origin, cell, game.FieldHeight, 0f, rules.GroundHeight, rules.FieldWidth, 0.4f), GroundColor);

            foreach (var obstacle in game.Obstacles)
            {
                float width = obstacle.IsTall ? rules.TallObstacleWidth : rules.ShortObstacleWidth;
                float height = obstacle.IsTall ? rules.TallObstacleHeight : rules.ShortObstacleHeight;
                Fill(FieldRect(origin, cell, game.FieldHeight,
                    obstacle.Center.X - width * 0.5f, obstacle.Center.Y - height * 0.5f, width, height),
                    obstacle.Passed ? Color.Lerp(ObstacleColor, SkyColor, 0.45f) : ObstacleColor);
            }

            var player = game.PlayerPosition;
            Fill(FieldRect(origin, cell, game.FieldHeight,
                player.X - rules.PlayerHalfWidth, player.Y - rules.PlayerHalfHeight,
                rules.PlayerHalfWidth * 2f, rules.PlayerHalfHeight * 2f), PlayerColor);
        }

        /// <summary>游戏矩形（逻辑坐标 Y 向上，给定左下角与尺寸）转 IMGUI 矩形（Y 向下），origin 为场地左上角。</summary>
        private static Rect FieldRect(Vector2 origin, float cell, float fieldHeight, float x, float y, float width, float height)
        {
            float left = origin.x + x * cell;
            float top = origin.y + (fieldHeight - y - height) * cell;
            return new Rect(left, top, width * cell, height * cell);
        }

        private void DrawStatus(EndlessRunnerGame game)
        {
            string lifecycle = game.IsGameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"距离 {game.Distance:0.0} 米    分数 {game.Score}    速度 {game.Speed:0.0} 米/秒    状态 {lifecycle}",
                StatusStyle());
        }

        private void DrawEventLog(EndlessRunnerGame game)
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

        private void DrawOverlay(EndlessRunnerGame game)
        {
            if (!game.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.38f + 48f, Screen.width, 28f),
                $"最终距离 {game.Distance:0.0} 米    最终分数 {game.Score}    按 R 重新开始", StatusStyle());
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

        private static GUIStyle LogStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
            return style;
        }
    }
}
