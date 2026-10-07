using Lyking.Snake.Core;
using UnityEngine;

namespace Lyking.Snake.Unity
{
    /// <summary>
    /// 贪吃蛇演示面板：IMGUI 绘制状态栏、棋盘、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产。
    /// </summary>
    public sealed class SnakeDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.16f, 0.18f, 0.15f);
        private static readonly Color BoardBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color SnakeHead = new Color(0.42f, 0.85f, 0.35f);
        private static readonly Color SnakeBody = new Color(0.25f, 0.60f, 0.25f);
        private static readonly Color FoodColor = new Color(0.90f, 0.32f, 0.28f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private SnakeDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(SnakeDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasBoard) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var board = _bootstrap.Board;
            var lifecycle = board.IsGameOver ? "结束" : board.IsWon ? "通关" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"分数 {board.Score}    长度 {board.Length}    进食 {board.FoodEaten}    速度 {1f / board.StepSeconds:0.0} 格/秒    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(board);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(SnakeBoard board)
        {
            float cell = Mathf.Min((Screen.height - 140f) / board.Height, (Screen.width - 80f) / board.Width);
            float boardWidth = cell * board.Width;
            float boardHeight = cell * board.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            for (int i = board.Body.Count - 1; i >= 0; i--)
            {
                var pos = board.Body[i];
                var rect = CellRect(origin, cell, board.Height, pos);
                Fill(rect, i == 0 ? SnakeHead : Color.Lerp(SnakeBody, SnakeHead, 0.35f * (1f - i / (float)Mathf.Max(1, board.Body.Count))));
            }

            if (board.Food.X >= 0)
            {
                var rect = CellRect(origin, cell, board.Height, board.Food);
                rect.x += cell * 0.15f;
                rect.y += cell * 0.15f;
                rect.width -= cell * 0.3f;
                rect.height -= cell * 0.3f;
                Fill(rect, FoodColor);
            }
        }

        /// <summary>游戏坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int height, GridPos pos)
        {
            float left = origin.x + pos.X * cell;
            float top = origin.y + (height - 1 - pos.Y) * cell;
            return new Rect(left, top, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(SnakeBoard board)
        {
            var events = board.Events;
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

        private void DrawOverlay(SnakeBoard board)
        {
            if (!board.IsFinished) return;
            string title = board.IsWon ? "蛇占满棋盘，通关！" : "游戏结束";
            string hint = $"最终分数 {board.Score}    按 R 重新开始";
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f), hint, StatusStyle());
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
