using Lyking.DoodleJumper.Core;
using UnityEngine;

namespace Lyking.DoodleJumper.Unity
{
    /// <summary>
    /// 涂鸦跳跃演示面板：IMGUI 绘制镜头视野内的天空、场界、三种平台、玩家、顶部状态、
    /// 结束提示与事件日志。全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，
    /// 不依赖物理引擎与精灵，重力与弹跳均由核心自积分。
    /// </summary>
    public sealed class DoodleJumperDemoPanel : MonoBehaviour
    {
        private static readonly Color SkyColor = new Color(0.87f, 0.94f, 0.98f);
        private static readonly Color FieldBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color NormalPlatformColor = new Color(0.36f, 0.68f, 0.33f);
        private static readonly Color FragilePlatformColor = new Color(0.78f, 0.55f, 0.24f);
        private static readonly Color MovingPlatformColor = new Color(0.55f, 0.42f, 0.85f);
        private static readonly Color PlayerColor = new Color(0.25f, 0.55f, 0.90f);
        private static readonly Color TextColor = new Color(0.15f, 0.20f, 0.24f);

        private DoodleJumperDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(DoodleJumperDemoBootstrap bootstrap) => _bootstrap = bootstrap;

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

        /// <summary>按镜头视野绘制场地边框、平台与玩家；原点取视野左上角，随镜头升高整体下移。</summary>
        private void DrawField(JumperGame game)
        {
            var rules = game.Rules;
            float cell = Mathf.Min((Screen.height - 150f) / rules.ViewHeight, (Screen.width - 80f) / rules.FieldWidth);
            float viewWidth = cell * rules.FieldWidth;
            float viewHeight = cell * rules.ViewHeight;
            var origin = new Vector2((Screen.width - viewWidth) * 0.5f, (Screen.height - viewHeight) * 0.5f + 14f);
            float bottom = game.CameraBottomY;

            Fill(new Rect(origin.x - 4f, origin.y - 4f, viewWidth + 8f, viewHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, viewWidth, viewHeight), SkyColor);

            foreach (var platform in game.Platforms)
            {
                float top = platform.Center.Y + rules.PlatformThickness * 0.5f;
                if (top < bottom || platform.Center.Y - rules.PlatformThickness * 0.5f > game.CameraTopY) continue;
                Color color = platform.Type switch
                {
                    JumperPlatformType.Fragile => FragilePlatformColor,
                    JumperPlatformType.Moving => MovingPlatformColor,
                    _ => NormalPlatformColor,
                };
                Fill(ViewRect(origin, cell, rules.ViewHeight, bottom,
                    platform.Center.X - rules.PlatformHalfWidth, platform.Center.Y - rules.PlatformThickness * 0.5f,
                    rules.PlatformWidth, rules.PlatformThickness), color);
            }

            var player = game.PlayerPosition;
            Fill(ViewRect(origin, cell, rules.ViewHeight, bottom,
                player.X - rules.PlayerHalfWidth, player.Y - rules.PlayerHalfHeight,
                rules.PlayerHalfWidth * 2f, rules.PlayerHalfHeight * 2f), PlayerColor);
        }

        /// <summary>世界矩形（逻辑坐标 Y 向上，给定左下角与尺寸）转 IMGUI 矩形（Y 向下），origin 为视野左上角。</summary>
        private static Rect ViewRect(Vector2 origin, float cell, float viewHeight, float bottom, float x, float y, float width, float height)
        {
            float left = origin.x + x * cell;
            float top = origin.y + (viewHeight - (y - bottom) - height) * cell;
            return new Rect(left, top, width * cell, height * cell);
        }

        private void DrawStatus(JumperGame game)
        {
            string lifecycle = game.IsGameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"高度 {game.MaxHeight:0.0} 米    分数 {game.Score}    平台 {game.Platforms.Count} 块    状态 {lifecycle}",
                StatusStyle());
        }

        private void DrawEventLog(JumperGame game)
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

        private void DrawOverlay(JumperGame game)
        {
            if (!game.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.38f + 48f, Screen.width, 28f),
                $"最终高度 {game.MaxHeight:0.0} 米    最终分数 {game.Score}    按 R 重新开始", StatusStyle());
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
