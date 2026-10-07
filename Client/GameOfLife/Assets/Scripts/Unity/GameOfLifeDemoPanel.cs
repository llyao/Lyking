using Lyking.GameOfLife.Core;
using UnityEngine;

namespace Lyking.GameOfLife.Unity
{
    /// <summary>
    /// 生命游戏演示面板：IMGUI 绘制状态栏、环形网格、事件日志与操作提示。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 活细胞按存活代数由亮绿渐变到蓝，死细胞为深色，鼠标按下翻转格位、拖拽连续绘制。
    /// </summary>
    public sealed class GameOfLifeDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color BoardBorder = new Color(0.32f, 0.48f, 0.40f);
        private static readonly Color DeadCell = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color NewLife = new Color(0.55f, 0.95f, 0.55f);
        private static readonly Color OldLife = new Color(0.30f, 0.55f, 0.92f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        /// <summary>年龄渐变的封顶代数：存活超过该代数的细胞统一用最老颜色。</summary>
        private const int AgeColorSpan = 24;

        private GameOfLifeDemoBootstrap? _bootstrap;
        private bool _painting;
        private bool _paintValue;

        public void Bind(GameOfLifeDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasBoard) return;

            var board = bootstrap.Board;
            string lifecycle = board.IsPaused ? "已暂停" : "运行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"代数 {board.Generation}    活细胞 {board.AliveCount}    状态 {lifecycle}    节拍 {LifeRules.StepSeconds:0.###} 秒/代",
                StatusStyle());

            DrawBoard(board);
            DrawEventLog(board);
            DrawHelp();
        }

        private void DrawBoard(LifeBoard board)
        {
            float cell = Mathf.Min((Screen.height - 150f) / board.Height, (Screen.width - 60f) / board.Width);
            float boardWidth = cell * board.Width;
            float boardHeight = cell * board.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 10f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), BoardBackground);

            HandleMouse(board, origin, cell);

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Fill(CellRect(origin, cell, x, y), CellColor(board, x, y));
                }
            }
        }

        /// <summary>
        /// 把鼠标按下/拖拽换算为格位：按下按格翻转并确定连续绘制的目标状态，拖拽持续写入。
        /// </summary>
        private void HandleMouse(LifeBoard board, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null) return;

            if (current.type == EventType.MouseDown)
            {
                if (!TryCellAt(current.mousePosition, origin, cell, board, out int cellX, out int cellY)) return;

                _painting = true;
                _paintValue = !board.IsAlive(cellX, cellY);
                Paint(board, cellX, cellY);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && _painting)
            {
                if (TryCellAt(current.mousePosition, origin, cell, board, out int cellX, out int cellY)) Paint(board, cellX, cellY);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && _painting)
            {
                _painting = false;
                current.Use();
            }
        }

        /// <summary>按拖拽起点的目标状态写入格位：已满足则跳过，避免来回翻转。</summary>
        private void Paint(LifeBoard board, int cellX, int cellY)
        {
            if (board.IsAlive(cellX, cellY) != _paintValue) board.ToggleCell(cellX, cellY);
        }

        /// <summary>屏幕坐标转格位，落在棋盘外返回 false。</summary>
        private static bool TryCellAt(Vector2 position, Vector2 origin, float cell, LifeBoard board, out int cellX, out int cellY)
        {
            int column = Mathf.FloorToInt((position.x - origin.x) / cell);
            int row = Mathf.FloorToInt((position.y - origin.y) / cell);
            if (column < 0 || column >= board.Width || row < 0 || row >= board.Height)
            {
                cellX = 0;
                cellY = 0;
                return false;
            }

            cellX = column;
            cellY = row;
            return true;
        }

        /// <summary>格位（0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int cellX, int cellY) =>
            new Rect(origin.x + cellX * cell, origin.y + cellY * cell, Mathf.Max(1f, cell - 1f), Mathf.Max(1f, cell - 1f));

        /// <summary>细胞颜色：死细胞深色，活细胞按存活代数由亮绿渐变到蓝。</summary>
        private static Color CellColor(LifeBoard board, int cellX, int cellY)
        {
            if (!board.IsAlive(cellX, cellY)) return DeadCell;

            float t = Mathf.Clamp01((board.AgeAt(cellX, cellY) - 1) / (float)AgeColorSpan);
            return Color.Lerp(NewLife, OldLife, t);
        }

        private void DrawEventLog(LifeBoard board)
        {
            var events = board.Events;
            int count = Mathf.Min(5, events.Count);
            var builder = new System.Text.StringBuilder();
            builder.Append("事件日志: ");
            for (int i = events.Count - count; i < events.Count; i++)
            {
                if (i > events.Count - count) builder.Append("    ");
                builder.Append(events[i].ToString());
            }

            GUI.Label(new Rect(12f, Screen.height - 58f, Screen.width - 24f, 24f), builder.ToString(), LogStyle());
        }

        private void DrawHelp()
        {
            GUI.Label(new Rect(12f, Screen.height - 32f, Screen.width - 24f, 24f),
                "空格 暂停/继续    N 单步    C 清空    P 载入滑翔机枪    R 随机重撒    鼠标按下翻转格位，拖拽连续绘制",
                LogStyle());
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

        private static GUIStyle LogStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
            return style;
        }
    }
}
