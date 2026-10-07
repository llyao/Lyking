using Lyking.FloodIt.Core;
using UnityEngine;

namespace Lyking.FloodIt.Unity
{
    /// <summary>
    /// 色彩淹没演示面板：IMGUI 绘制状态栏、14x14 六色棋盘、选色按钮、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 已淹没格整体变亮与未淹没的同色区域区分，底部六个色块按钮由鼠标点击或数字键选色。
    /// </summary>
    public sealed class FloodItDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.16f, 0.15f, 0.21f);
        private static readonly Color BoardBorder = new Color(0.44f, 0.40f, 0.58f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);
        private static readonly Color ButtonText = new Color(0.13f, 0.12f, 0.16f);

        // 6 种棋盘配色，与核心的颜色编号一一对应。
        private static readonly Color[] CellColors =
        {
            new Color(0.88f, 0.42f, 0.36f),
            new Color(0.95f, 0.76f, 0.28f),
            new Color(0.44f, 0.78f, 0.38f),
            new Color(0.36f, 0.70f, 0.86f),
            new Color(0.56f, 0.48f, 0.88f),
            new Color(0.91f, 0.56f, 0.78f),
        };

        private FloodItDemoBootstrap? _bootstrap;

        public void Bind(FloodItDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string lifecycle = board.IsWon ? "胜利" : board.IsLost ? "失败" : "进行中";
            float percent = 100f * board.FloodedCount / board.CellCount;
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"步数 {board.Steps}    剩余步数 {board.RemainingMoves}    已淹没 {percent:F0}%    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(board);
            DrawButtons(board, bootstrap);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(FloodBoard board)
        {
            float cell = Mathf.Min((Screen.height - 180f) / board.Rows, (Screen.width - 80f) / board.Columns);
            float boardWidth = cell * board.Columns;
            float boardHeight = cell * board.Rows;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f - 16f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var color = CellColors[board.GetColor(row, column)];
                    // 已淹没格整体变亮，与未淹没的同色区域区分。
                    Fill(CellRect(origin, cell, row, column),
                        board.IsFlooded(row, column) ? Color.Lerp(color, Color.white, 0.35f) : color);
                }
            }
        }

        /// <summary>底部一排 6 个色块按钮：点击请求选色，当前淹没色的按钮描白边提示同色会被拒绝。</summary>
        private void DrawButtons(FloodBoard board, FloodItDemoBootstrap bootstrap)
        {
            const float size = 40f;
            const float gap = 12f;
            float x = (Screen.width - ButtonRowWidth(size, gap)) * 0.5f;
            float y = Screen.height - 96f;

            HandleButtonClick(bootstrap, x, y, size, gap);
            for (int color = 0; color < FloodItRules.ColorCount; color++)
            {
                var rect = ButtonRect(color, x, y, size, gap);
                if (board.GetColor(0, 0) == color)
                {
                    Fill(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Color.white);
                }

                Fill(rect, CellColors[color]);
                GUI.Label(rect, (color + 1).ToString(), ButtonStyle());
            }
        }

        /// <summary>把鼠标按下沿换算为按钮编号并请求选色；点击落在按钮外时忽略。</summary>
        private void HandleButtonClick(FloodItDemoBootstrap bootstrap, float x, float y, float size, float gap)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            for (int color = 0; color < FloodItRules.ColorCount; color++)
            {
                if (ButtonRect(color, x, y, size, gap).Contains(current.mousePosition))
                {
                    bootstrap.Choose(color);
                    current.Use();
                    return;
                }
            }
        }

        private static float ButtonRowWidth(float size, float gap) =>
            FloodItRules.ColorCount * size + (FloodItRules.ColorCount - 1) * gap;

        private static Rect ButtonRect(int color, float x, float y, float size, float gap) =>
            new Rect(x + color * (size + gap), y, size, size);

        /// <summary>格位（行优先、左上角为原点）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int column) =>
            new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);

        private void DrawEventLog(FloodBoard board)
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

        private void DrawOverlay(FloodBoard board)
        {
            if (!board.IsWon && !board.IsLost) return;

            string title = board.IsWon ? "全盘同色，胜利！" : "步数用尽，失败！";
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"共用步数 {board.Steps}    按 R 重新开始    按 N 换一题", StatusStyle());
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

        private static GUIStyle ButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = ButtonText;
            return style;
        }
    }
}
