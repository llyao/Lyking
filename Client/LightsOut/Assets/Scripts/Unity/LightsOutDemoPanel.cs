using Lyking.LightsOut.Core;
using UnityEngine;

namespace Lyking.LightsOut.Unity
{
    /// <summary>
    /// 点灯演示面板：IMGUI 绘制状态栏、灯格棋盘、胜利提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 亮灯为暖黄、灭灯为深黑，鼠标点击格位请求按下，键盘光标格以描边高亮。
    /// </summary>
    public sealed class LightsOutDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.14f, 0.15f, 0.20f);
        private static readonly Color BoardBorder = new Color(0.58f, 0.52f, 0.32f);
        private static readonly Color LightOff = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color LightOn = new Color(0.95f, 0.82f, 0.35f);
        private static readonly Color CursorFrame = new Color(0.40f, 0.74f, 0.94f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private LightsOutDemoBootstrap? _bootstrap;

        public void Bind(LightsOutDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string lifecycle = board.IsSolved ? "胜利" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"步数 {board.Moves}    亮灯 {board.LightCount}    状态 {lifecycle}    谜题号 {board.PuzzleNumber}",
                StatusStyle());

            DrawBoard(board, bootstrap);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(LightsBoard board, LightsOutDemoBootstrap bootstrap)
        {
            float cell = Mathf.Min((Screen.height - 140f) / board.Size, (Screen.width - 80f) / board.Size);
            float boardWidth = cell * board.Size;
            float boardHeight = cell * board.Size;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            HandleClick(board, bootstrap, origin, cell);

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Fill(CellRect(origin, cell, row, column), board.IsLit(row, column) ? LightOn : LightOff);
                }
            }

            // 键盘光标：先画描边再回填格底色，形成高亮边框，与鼠标点击并存。
            var cursor = CellRect(origin, cell, bootstrap.CursorRow, bootstrap.CursorColumn);
            Fill(new Rect(cursor.x - 3f, cursor.y - 3f, cursor.width + 6f, cursor.height + 6f), CursorFrame);
            Fill(cursor, board.IsLit(bootstrap.CursorRow, bootstrap.CursorColumn) ? LightOn : LightOff);
        }

        /// <summary>把鼠标按下沿换算为格位并请求按下；点击落在棋盘外时忽略。</summary>
        private void HandleClick(LightsBoard board, LightsOutDemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= board.Size || row < 0 || row >= board.Size) return;

            bootstrap.Press(row, column);
            current.Use();
        }

        /// <summary>格位（0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int column) =>
            new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);

        private void DrawEventLog(LightsBoard board)
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

        private void DrawOverlay(LightsBoard board)
        {
            if (!board.IsSolved) return;

            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "全部熄灭，胜利！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"共用步数 {board.Moves}    按 R 重新开始    按 N 换一题", StatusStyle());
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
