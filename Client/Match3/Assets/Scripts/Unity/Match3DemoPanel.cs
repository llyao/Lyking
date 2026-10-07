using Lyking.Match3.Core;
using UnityEngine;

namespace Lyking.Match3.Unity
{
    /// <summary>
    /// 三消演示面板：IMGUI 绘制状态栏、8x8 宝石网格、光标与选中高亮、死局与结束覆盖层、事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 纯色块与编号文字，不依赖任何场景资产；
    /// 鼠标点击请求选中/交换，消除结算由核心离散完成，面板直接刷新呈现结果。
    /// </summary>
    public sealed class Match3DemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.13f, 0.15f, 0.20f);
        private static readonly Color BoardBorder = new Color(0.42f, 0.46f, 0.60f);
        private static readonly Color CellBackground = new Color(0.20f, 0.23f, 0.30f);
        private static readonly Color CursorFrame = new Color(0.95f, 0.90f, 0.45f);
        private static readonly Color SelectedFrame = new Color(0.45f, 0.90f, 0.95f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        // 6 种宝石的配色与编号，与核心的种类索引一一对应。
        private static readonly Color[] GemColors =
        {
            new Color(0.88f, 0.42f, 0.36f),
            new Color(0.93f, 0.68f, 0.30f),
            new Color(0.90f, 0.86f, 0.38f),
            new Color(0.44f, 0.78f, 0.38f),
            new Color(0.36f, 0.70f, 0.86f),
            new Color(0.56f, 0.48f, 0.88f),
        };

        private Match3DemoBootstrap? _bootstrap;

        public void Bind(Match3DemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string state = board.IsFinished ? "已结束" : !board.HasValidMove ? "死局" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"分数 {board.Score}    剩余步数 {board.MovesLeft}/{Match3Rules.TotalMoves}    连锁 x{board.ChainLevel}    状态 {state}",
                StatusStyle());

            DrawBoard(board, bootstrap);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(Match3Board board, Match3DemoBootstrap bootstrap)
        {
            float cell = Mathf.Min((Screen.height - 150f) / Match3Rules.Rows, (Screen.width - 80f) / Match3Rules.Columns);
            float boardWidth = cell * Match3Rules.Columns;
            float boardHeight = cell * Match3Rules.Rows;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            HandleClick(bootstrap, origin, cell);

            var numberStyle = NumberStyle(cell);
            for (int row = 0; row < Match3Rules.Rows; row++)
            {
                for (int column = 0; column < Match3Rules.Columns; column++)
                {
                    var rect = CellRect(origin, cell, row, column);
                    Fill(rect, CellBackground);
                    int kind = board.GetGem(row, column);
                    Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), GemColors[kind]);
                    GUI.Label(rect, (kind + 1).ToString(), numberStyle);
                }
            }

            DrawFrame(origin, cell, bootstrap.CursorIndex, CursorFrame);
            if (bootstrap.SelectedIndex >= 0) DrawFrame(origin, cell, bootstrap.SelectedIndex, SelectedFrame);
        }

        /// <summary>把鼠标按下沿换算为格位并请求选中/交换；点击落在棋盘外时忽略。</summary>
        private void HandleClick(Match3DemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= Match3Rules.Columns || row < 0 || row >= Match3Rules.Rows) return;

            bootstrap.Select(row * Match3Rules.Columns + column);
            current.Use();
        }

        /// <summary>在指定格位绘制高亮描边（四条纯色细矩形）。</summary>
        private static void DrawFrame(Vector2 origin, float cell, int index, Color color)
        {
            int column = index % Match3Rules.Columns;
            int row = index / Match3Rules.Columns;
            var rect = CellRect(origin, cell, row, column);
            float thickness = 3f;
            Fill(new Rect(rect.x - thickness, rect.y - thickness, rect.width + thickness * 2f, thickness), color);
            Fill(new Rect(rect.x - thickness, rect.y + rect.height, rect.width + thickness * 2f, thickness), color);
            Fill(new Rect(rect.x - thickness, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.x + rect.width, rect.y, thickness, rect.height), color);
        }

        /// <summary>行列格位（0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int column) =>
            new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);

        private void DrawEventLog(Match3Board board)
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

        private void DrawOverlay(Match3Board board)
        {
            if (board.IsFinished)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f),
                    $"步数用尽，最终分数 {board.Score}！", TitleStyle());
                GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f), "按 R 重新开始", StatusStyle());
            }
            else if (!board.HasValidMove)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 28f),
                    "当前无可行交换：按 S 重排棋盘，或按 R 重新开始", StatusStyle());
            }
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

        private static GUIStyle NumberStyle(float cell)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp((int)(cell * 0.4f), 12, 48),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = TextColor;
            return style;
        }
    }
}
