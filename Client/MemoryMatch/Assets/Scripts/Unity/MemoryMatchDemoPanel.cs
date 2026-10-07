using Lyking.MemoryMatch.Core;
using UnityEngine;

namespace Lyking.MemoryMatch.Unity
{
    /// <summary>
    /// 记忆翻牌演示面板：IMGUI 绘制状态栏、4x4 卡格、胜利提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 鼠标点击卡格请求翻牌，判定与翻回节奏由核心完成。
    /// </summary>
    public sealed class MemoryMatchDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.16f, 0.15f, 0.21f);
        private static readonly Color BoardBorder = new Color(0.44f, 0.40f, 0.58f);
        private static readonly Color CardHidden = new Color(0.24f, 0.26f, 0.34f);
        private static readonly Color CardMatchedFrame = new Color(0.85f, 0.80f, 0.55f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        // 8 种图案的配色，与核心的图案索引一一对应。
        private static readonly Color[] PatternColors =
        {
            new Color(0.88f, 0.42f, 0.36f),
            new Color(0.93f, 0.68f, 0.30f),
            new Color(0.90f, 0.86f, 0.38f),
            new Color(0.44f, 0.78f, 0.38f),
            new Color(0.36f, 0.70f, 0.86f),
            new Color(0.56f, 0.48f, 0.88f),
            new Color(0.88f, 0.55f, 0.78f),
            new Color(0.46f, 0.78f, 0.70f),
        };

        private MemoryMatchDemoBootstrap? _bootstrap;

        public void Bind(MemoryMatchDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string lifecycle = board.IsWon ? "胜利" : board.Phase == MemoryMatchPhase.TwoOpen ? "等待翻回" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"步数 {board.Moves}    已配对 {board.MatchedPairs}/{MemoryMatchRules.PatternCount}    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(board, bootstrap);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(MemoryBoard board, MemoryMatchDemoBootstrap bootstrap)
        {
            float cell = Mathf.Min((Screen.height - 140f) / MemoryMatchRules.Rows, (Screen.width - 80f) / MemoryMatchRules.Columns);
            float boardWidth = cell * MemoryMatchRules.Columns;
            float boardHeight = cell * MemoryMatchRules.Rows;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            HandleClick(board, bootstrap, origin, cell);

            var numberStyle = NumberStyle(cell);
            for (int index = 0; index < board.CardCount; index++)
            {
                var card = board.GetCard(index);
                var rect = CellRect(origin, cell, index);
                if (card.State == MemoryCardState.Matched)
                {
                    // 已配对：变暗并描边，保留编号示意归位。
                    Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), CardMatchedFrame);
                    Fill(rect, Color.Lerp(PatternColors[card.Pattern], Color.black, 0.55f));
                }
                else if (card.State == MemoryCardState.FaceUp)
                {
                    Fill(rect, PatternColors[card.Pattern]);
                }
                else
                {
                    Fill(rect, CardHidden);
                }

                if (card.State != MemoryCardState.FaceDown) GUI.Label(rect, (card.Pattern + 1).ToString(), numberStyle);
            }
        }

        /// <summary>把鼠标按下沿换算为格位并请求翻牌；点击落在棋盘外时忽略。</summary>
        private void HandleClick(MemoryBoard board, MemoryMatchDemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= MemoryMatchRules.Columns || row < 0 || row >= MemoryMatchRules.Rows) return;

            bootstrap.Flip(row * MemoryMatchRules.Columns + column);
            current.Use();
        }

        /// <summary>格位（行优先、0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int index)
        {
            int column = index % MemoryMatchRules.Columns;
            int row = index / MemoryMatchRules.Columns;
            return new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(MemoryBoard board)
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

        private void DrawOverlay(MemoryBoard board)
        {
            if (!board.IsWon) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "全部配对成功，胜利！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f), $"共用步数 {board.Moves}    按 R 重新开始", StatusStyle());
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
