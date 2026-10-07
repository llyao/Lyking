using Lyking.Sokoban.Core;
using UnityEngine;

namespace Lyking.Sokoban.Unity
{
    /// <summary>
    /// 推箱子演示面板：IMGUI 绘制状态栏、棋盘、通关提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与场景资产。
    /// </summary>
    public sealed class SokobanDemoPanel : MonoBehaviour
    {
        private static readonly Color FloorColor = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color BoardBorder = new Color(0.45f, 0.40f, 0.30f);
        private static readonly Color WallColor = new Color(0.42f, 0.38f, 0.33f);
        private static readonly Color GoalColor = new Color(0.95f, 0.78f, 0.30f);
        private static readonly Color BoxColor = new Color(0.72f, 0.47f, 0.24f);
        private static readonly Color BoxOnGoalColor = new Color(0.42f, 0.82f, 0.40f);
        private static readonly Color PlayerColor = new Color(0.30f, 0.62f, 0.95f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private SokobanDemoBootstrap? _bootstrap;

        public void Bind(SokobanDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasPuzzle) return;

            var puzzle = _bootstrap.Puzzle;
            var lifecycle = puzzle.IsAllCompleted ? "全部通关" : puzzle.IsSolved ? "本关通关" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"关卡 {puzzle.LevelIndex + 1}/{puzzle.LevelCount}    步数 {puzzle.MoveCount}    推动 {puzzle.PushCount}    {puzzle.CurrentLevel.Name}    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(puzzle);
            DrawEventLog(puzzle);
            DrawOverlay(puzzle);
        }

        private void DrawBoard(SokobanPuzzle puzzle)
        {
            var level = puzzle.CurrentLevel;
            float cell = Mathf.Min((Screen.height - 160f) / level.Height, (Screen.width - 80f) / level.Width);
            float boardWidth = cell * level.Width;
            float boardHeight = cell * level.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), FloorColor);

            for (int y = level.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (level.IsWall(pos)) Fill(CellRect(origin, cell, level.Height, pos), WallColor);
                }
            }

            foreach (var goal in level.Goals)
            {
                var rect = CellRect(origin, cell, level.Height, goal);
                rect.x += cell * 0.32f;
                rect.y += cell * 0.32f;
                rect.width -= cell * 0.64f;
                rect.height -= cell * 0.64f;
                Fill(rect, GoalColor);
            }

            for (int i = 0; i < puzzle.Boxes.Count; i++)
            {
                var box = puzzle.Boxes[i];
                var rect = CellRect(origin, cell, level.Height, box);
                rect.x += cell * 0.10f;
                rect.y += cell * 0.10f;
                rect.width -= cell * 0.20f;
                rect.height -= cell * 0.20f;
                Fill(rect, level.IsGoal(box) ? BoxOnGoalColor : BoxColor);
            }

            var playerRect = CellRect(origin, cell, level.Height, puzzle.Player);
            playerRect.x += cell * 0.18f;
            playerRect.y += cell * 0.18f;
            playerRect.width -= cell * 0.36f;
            playerRect.height -= cell * 0.36f;
            Fill(playerRect, PlayerColor);
        }

        /// <summary>游戏坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int height, GridPos pos)
        {
            float left = origin.x + pos.X * cell;
            float top = origin.y + (height - 1 - pos.Y) * cell;
            return new Rect(left, top, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(SokobanPuzzle puzzle)
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

        private void DrawOverlay(SokobanPuzzle puzzle)
        {
            if (!puzzle.IsSolved) return;
            string title = puzzle.IsAllCompleted ? "全部通关，太厉害了！" : "本关通关！";
            string hint = puzzle.IsAllCompleted ? "按 R 重新挑战" : "按 N 进入下一关    按 Z 撤销复盘";
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
