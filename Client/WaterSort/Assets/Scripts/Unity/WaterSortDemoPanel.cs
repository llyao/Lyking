using Lyking.WaterSort.Core;
using UnityEngine;

namespace Lyking.WaterSort.Unity
{
    /// <summary>
    /// 水体排序演示面板：IMGUI 绘制状态栏、一排（超过 8 瓶分两排）瓶子、通关提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与场景资产。
    /// </summary>
    public sealed class WaterSortDemoPanel : MonoBehaviour
    {
        private static readonly Color BackdropColor = new Color(0.10f, 0.11f, 0.14f);
        private static readonly Color GlassColor = new Color(0.55f, 0.58f, 0.65f);
        private static readonly Color LiquidBackColor = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color CursorColor = new Color(0.30f, 0.62f, 0.95f);
        private static readonly Color SelectedColor = new Color(0.95f, 0.78f, 0.30f);
        private static readonly Color DoneColor = new Color(0.85f, 0.68f, 0.20f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        /// <summary>颜色 1..8 的液体显示色。</summary>
        private static readonly Color[] LiquidColors =
        {
            new Color(0.85f, 0.33f, 0.30f),
            new Color(0.30f, 0.55f, 0.90f),
            new Color(0.36f, 0.75f, 0.40f),
            new Color(0.95f, 0.82f, 0.30f),
            new Color(0.45f, 0.80f, 0.90f),
            new Color(0.80f, 0.45f, 0.85f),
            new Color(0.95f, 0.55f, 0.25f),
            new Color(0.55f, 0.45f, 0.80f),
        };

        private WaterSortDemoBootstrap? _bootstrap;

        public void Bind(WaterSortDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasPuzzle) return;

            var puzzle = _bootstrap.Puzzle;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), BackdropColor);
            DrawStatus(puzzle);
            DrawTubes(puzzle);
            DrawEventLog(puzzle);
            DrawOverlay(puzzle);
        }

        private void DrawStatus(WaterSortPuzzle puzzle)
        {
            string lifecycle = puzzle.IsSolved ? "已通关" : "进行中";
            string selection = puzzle.SelectedTube >= 0 ? $"    已选 瓶{puzzle.SelectedTube + 1}" : string.Empty;
            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 26f),
                $"颜色 {puzzle.ColorCount}    瓶数 {puzzle.TubeCount}    步数 {puzzle.Moves}    撤销 {(puzzle.CanUndo ? "可用" : "不可用")}" +
                $"    状态 {lifecycle}    光标 瓶{_bootstrap!.Cursor + 1}{selection}",
                StatusStyle());
        }

        private void DrawTubes(WaterSortPuzzle puzzle)
        {
            int count = puzzle.TubeCount;
            int rows = count > 8 ? 2 : 1;
            int perRow = rows == 1 ? count : (count + 1) / 2;
            float areaTop = 48f;
            float areaHeight = Screen.height - 48f - 64f;
            float areaWidth = Screen.width - 32f;
            float gap = 16f;
            float width = Mathf.Min(64f, (areaWidth - gap * (perRow - 1)) / perRow);
            float height = width * 2.1f;
            float maxRowHeight = (areaHeight - gap * (rows - 1)) / rows;
            if (height > maxRowHeight)
            {
                height = maxRowHeight;
                width = height / 2.1f;
            }

            float boardHeight = rows * height + (rows - 1) * gap;
            float startY = areaTop + Mathf.Max(0f, (areaHeight - boardHeight) * 0.5f);
            var cursor = _bootstrap!.Cursor;
            for (int i = 0; i < count; i++)
            {
                int row = rows == 1 ? 0 : i / perRow;
                int column = rows == 1 ? i : i % perRow;
                int rowCount = rows == 1 ? count : Mathf.Min(perRow, count - row * perRow);
                float rowWidth = rowCount * width + (rowCount - 1) * gap;
                float left = (Screen.width - rowWidth) * 0.5f + column * (width + gap);
                float top = startY + row * (height + gap);
                DrawTube(puzzle, i, new Rect(left, top, width, height), cursor == i);
            }
        }

        /// <summary>绘制单只瓶子：玻璃描边、暗色内衬、四层色块自下而上、完成瓶画盖、光标高亮边框。</summary>
        private static void DrawTube(WaterSortPuzzle puzzle, int index, Rect rect, bool isCursor)
        {
            var snapshot = puzzle.Tubes[index];
            bool isDone = IsTubeDone(puzzle, snapshot);
            bool isSelected = puzzle.SelectedTube == index;

            Fill(Expand(rect, 3f), isSelected ? SelectedColor : isDone ? DoneColor : GlassColor);
            Fill(rect, LiquidBackColor);

            float layerHeight = (rect.height - 8f) / puzzle.Capacity;
            for (int layer = 0; layer < snapshot.Length; layer++)
            {
                var color = LiquidColors[Mathf.Clamp(snapshot[layer], 1, LiquidColors.Length) - 1];
                var layerRect = new Rect(
                    rect.x + 3f,
                    rect.yMax - 4f - (layer + 1) * layerHeight,
                    rect.width - 6f,
                    layerHeight - 2f);
                Fill(layerRect, color);
            }

            if (isDone) Fill(new Rect(rect.x - 2f, rect.y - 6f, rect.width + 4f, 5f), DoneColor);
            if (isCursor && !isSelected) DrawOutline(Expand(rect, 6f), CursorColor);
        }

        /// <summary>瓶子完成判定：满瓶且各层同色。</summary>
        private static bool IsTubeDone(WaterSortPuzzle puzzle, int[] snapshot)
        {
            if (snapshot.Length != puzzle.Capacity) return false;
            for (int i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i] != snapshot[0]) return false;
            }

            return true;
        }

        private static Rect Expand(Rect rect, float padding) =>
            new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);

        /// <summary>用四条细矩形描出外框。</summary>
        private static void DrawOutline(Rect rect, Color color)
        {
            float thickness = 2f;
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawEventLog(WaterSortPuzzle puzzle)
        {
            var events = puzzle.Events;
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

        private void DrawOverlay(WaterSortPuzzle puzzle)
        {
            if (!puzzle.IsSolved) return;
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 44f), "排序完成，太厉害了！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.38f + 48f, Screen.width, 28f), "按 R 重开本局", StatusStyle(TextAnchor.MiddleCenter));
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private static GUIStyle StatusStyle(TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = alignment };
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
