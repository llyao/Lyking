using Lyking.Sudoku.Core;
using UnityEngine;

namespace Lyking.Sudoku.Unity
{
    /// <summary>
    /// 数独演示面板：IMGUI 绘制状态栏、9x9 棋盘、完成提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与场景资产；
    /// 粗线划分 3x3 宫，给定格深底白字、玩家数亮色、冲突格红字，鼠标点击选格，
    /// 键盘光标格以描边高亮。
    /// </summary>
    public sealed class SudokuDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.14f, 0.15f, 0.20f);
        private static readonly Color BoardBorder = new Color(0.58f, 0.52f, 0.32f);
        private static readonly Color LineColor = new Color(0.34f, 0.35f, 0.42f);
        private static readonly Color GivenCellColor = new Color(0.22f, 0.23f, 0.28f);
        private static readonly Color PlayerCellColor = new Color(0.27f, 0.30f, 0.38f);
        private static readonly Color CursorFrame = new Color(0.40f, 0.74f, 0.94f);
        private static readonly Color GivenTextColor = new Color(0.95f, 0.95f, 0.92f);
        private static readonly Color PlayerTextColor = new Color(0.45f, 0.78f, 0.98f);
        private static readonly Color ConflictTextColor = new Color(0.96f, 0.36f, 0.30f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private SudokuDemoBootstrap? _bootstrap;

        public void Bind(SudokuDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasPuzzle) return;

            var puzzle = bootstrap.Puzzle;
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"难度 {DifficultyName(puzzle.Difficulty)}    剩余空格 {puzzle.EmptyCount}    步数 {puzzle.MoveCount}    状态 {StatusText(puzzle)}",
                StatusStyle());

            DrawBoard(puzzle, bootstrap);
            DrawEventLog(puzzle);
            DrawOverlay(puzzle);
        }

        private void DrawBoard(SudokuPuzzle puzzle, SudokuDemoBootstrap bootstrap)
        {
            float cell = Mathf.Min((Screen.height - 170f) / SudokuRules.Size, (Screen.width - 120f) / SudokuRules.Size);
            float boardWidth = cell * SudokuRules.Size;
            float boardHeight = cell * SudokuRules.Size;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(new Rect(origin.x, origin.y, boardWidth, boardHeight), LineColor);

            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    Fill(CellRect(origin, cell, row, column), CellColor(puzzle, row, column));
                }
            }

            // 3x3 宫边界用粗线：在 3、6 倍格位处画 4 像素横竖条。
            for (int i = SudokuRules.BoxSize; i < SudokuRules.Size; i += SudokuRules.BoxSize)
            {
                Fill(new Rect(origin.x + i * cell - 2f, origin.y, 4f, boardHeight), BoardBorder);
                Fill(new Rect(origin.x, origin.y + i * cell - 2f, boardWidth, 4f), BoardBorder);
            }

            HandleClick(bootstrap, origin, cell);

            // 键盘光标：先画描边再回填格底色，形成高亮边框，与鼠标点击并存。
            var cursor = CellRect(origin, cell, bootstrap.CursorRow, bootstrap.CursorColumn);
            Fill(new Rect(cursor.x - 3f, cursor.y - 3f, cursor.width + 6f, cursor.height + 6f), CursorFrame);
            Fill(cursor, CellColor(puzzle, bootstrap.CursorRow, bootstrap.CursorColumn));

            DrawNumbers(puzzle, origin, cell);
        }

        /// <summary>把鼠标按下沿换算为格位并移动光标；点击落在棋盘外时忽略。</summary>
        private void HandleClick(SudokuDemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= SudokuRules.Size || row < 0 || row >= SudokuRules.Size) return;

            bootstrap.MoveCursor(row, column);
            current.Use();
        }

        private void DrawNumbers(SudokuPuzzle puzzle, Vector2 origin, float cell)
        {
            for (int row = 0; row < SudokuRules.Size; row++)
            {
                for (int column = 0; column < SudokuRules.Size; column++)
                {
                    int value = puzzle.ValueAt(row, column);
                    if (value == 0) continue;

                    GUIStyle style = puzzle.IsConflict(row, column) ? ConflictStyle(cell)
                        : puzzle.IsGiven(row, column) ? GivenStyle(cell) : PlayerStyle(cell);
                    GUI.Label(CellRect(origin, cell, row, column), value.ToString(), style);
                }
            }
        }

        private static Color CellColor(SudokuPuzzle puzzle, int row, int column) =>
            puzzle.IsGiven(row, column) ? GivenCellColor : PlayerCellColor;

        /// <summary>格位（0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int column) =>
            new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);

        private static string StatusText(SudokuPuzzle puzzle)
        {
            if (puzzle.IsSolved) return "已通关";
            int conflicts = CountConflicts(puzzle);
            return conflicts > 0 ? $"冲突 {conflicts} 格" : "进行中";
        }

        private static int CountConflicts(SudokuPuzzle puzzle)
        {
            int count = 0;
            foreach (bool conflict in puzzle.Conflicts)
            {
                if (conflict) count++;
            }

            return count;
        }

        private static string DifficultyName(SudokuDifficulty difficulty) => difficulty switch
        {
            SudokuDifficulty.Easy => "简单",
            SudokuDifficulty.Medium => "中等",
            SudokuDifficulty.Hard => "困难",
            _ => difficulty.ToString(),
        };

        private void DrawEventLog(SudokuPuzzle puzzle)
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

        private void DrawOverlay(SudokuPuzzle puzzle)
        {
            if (!puzzle.IsSolved) return;

            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "全部填对，通关！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                "按 R 生成新谜题", StatusStyle());
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

        private static GUIStyle NumberStyle(float cell, Color color) => new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(12, Mathf.FloorToInt(cell * 0.52f)),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color },
        };

        private static GUIStyle GivenStyle(float cell) => NumberStyle(cell, GivenTextColor);

        private static GUIStyle PlayerStyle(float cell) => NumberStyle(cell, PlayerTextColor);

        private static GUIStyle ConflictStyle(float cell) => NumberStyle(cell, ConflictTextColor);
    }
}
