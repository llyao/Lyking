using Lyking.SlidingPuzzle.Core;
using UnityEngine;

namespace Lyking.SlidingPuzzle.Unity
{
    /// <summary>
    /// 数字华容道演示面板：IMGUI 绘制状态栏、N x N 滑块网格、胜利提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 鼠标点击与空位同行/同列的滑块请求一次滑入（多格滑动由核心逐块完成），判定与步数由核心完成。
    /// </summary>
    public sealed class SlidingPuzzleDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.16f, 0.15f, 0.21f);
        private static readonly Color BoardBorder = new Color(0.44f, 0.40f, 0.58f);
        private static readonly Color TileNormal = new Color(0.31f, 0.37f, 0.50f);
        private static readonly Color TileHomeFrame = new Color(0.85f, 0.80f, 0.55f);
        private static readonly Color TileBlank = new Color(0.10f, 0.10f, 0.14f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private SlidingPuzzleDemoBootstrap? _bootstrap;

        public void Bind(SlidingPuzzleDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string lifecycle = board.IsSolved ? "胜利" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"步数 {board.Steps}    尺寸 {board.Size}x{board.Size}    状态 {lifecycle}    耗时 {bootstrap.ElapsedSeconds:F0} 秒",
                StatusStyle());
            GUI.Label(new Rect(12f, 32f, 720f, 22f),
                "方向键/WASD 滑动相邻滑块    鼠标点击同行/同列滑块    数字键 3/4 切换尺寸    Z 撤销    R 重开",
                HintStyle());

            DrawBoard(board, bootstrap);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(SlidingBoard board, SlidingPuzzleDemoBootstrap bootstrap)
        {
            int size = board.Size;
            float cell = Mathf.Min((Screen.height - 150f) / size, (Screen.width - 80f) / size);
            float boardWidth = cell * size;
            float boardHeight = cell * size;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            HandleClick(bootstrap, origin, cell, size);

            var numberStyle = NumberStyle(cell);
            int[] tiles = board.Tiles;
            for (int index = 0; index < tiles.Length; index++)
            {
                int tile = tiles[index];
                var rect = CellRect(origin, cell, size, index);
                if (tile == 0)
                {
                    // 空位：深色凹槽示意。
                    Fill(rect, TileBlank);
                    continue;
                }

                if (board.IsTileHome(index))
                {
                    // 已归位：加亮边框。
                    Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), TileHomeFrame);
                }

                Fill(rect, TileNormal);
                GUI.Label(rect, tile.ToString(), numberStyle);
            }
        }

        /// <summary>把鼠标按下沿换算为格位并请求滑动；点击落在棋盘外时忽略，非法点击由核心拒绝并记日志。</summary>
        private void HandleClick(SlidingPuzzleDemoBootstrap bootstrap, Vector2 origin, float cell, int size)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= size || row < 0 || row >= size) return;

            bootstrap.SlideTile(row * size + column);
            current.Use();
        }

        /// <summary>格位（行优先、0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int size, int index)
        {
            int column = index % size;
            int row = index / size;
            return new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(SlidingBoard board)
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

        private void DrawOverlay(SlidingBoard board)
        {
            if (!board.IsSolved) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "还原成功，胜利！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f), $"共用步数 {board.Steps}    按 R 重新开始", StatusStyle());
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

        private static GUIStyle HintStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f);
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
