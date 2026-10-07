using System.Collections.Generic;
using Lyking.Gomoku.Core;
using UnityEngine;

namespace Lyking.Gomoku.Unity
{
    /// <summary>
    /// 五子棋演示面板：IMGUI 绘制比分栏、15x15 木色棋盘、光标、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，黑白棋子用色块绘制，
    /// 网格线、天元与星位用细矩形呈现，最后落子与获胜连线有标记高亮，
    /// 鼠标点击换算坐标直接落子，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class GomokuDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.82f, 0.66f, 0.42f);
        private static readonly Color BoardBorder = new Color(0.45f, 0.31f, 0.16f);
        private static readonly Color GridLine = new Color(0.38f, 0.26f, 0.13f);
        private static readonly Color StarPoint = new Color(0.20f, 0.13f, 0.06f);
        private static readonly Color BlackStone = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color WhiteStone = new Color(0.96f, 0.95f, 0.90f);
        private static readonly Color LastMoveMark = new Color(0.85f, 0.22f, 0.18f);
        private static readonly Color CursorColor = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color WinHighlight = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color TextColor = new Color(0.24f, 0.16f, 0.08f);

        private GomokuDemoBootstrap? _bootstrap;

        public void Bind(GomokuDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasMatch) return;

            var match = _bootstrap.Match;
            DrawScore(match);
            DrawBoard(match);
            DrawEventLog(match);
            DrawOverlay(match);
        }

        private void DrawScore(GomokuMatch match)
        {
            var board = match.Board;
            string state;
            if (board.IsFinished)
            {
                state = board.Outcome == GomokuOutcome.BlackWins ? "你赢了"
                    : board.Outcome == GomokuOutcome.WhiteWins ? "AI 获胜" : "平局";
            }
            else
            {
                state = board.CurrentTurn == Cell.Black ? "你的回合 (黑)" : "AI 思考中 (白)";
            }

            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {match.Round} 局    玩家(黑)胜 {match.PlayerWins}    AI(白)胜 {match.AiWins}    平局 {match.Draws}    状态 {state}    光标 行 {_bootstrap!.SelectedRow + 1} 列 {_bootstrap.SelectedCol + 1}",
                StatusStyle());
        }

        private void DrawBoard(GomokuMatch match)
        {
            var board = match.Board;
            float cell = Mathf.Max(28f, Mathf.Min(
                (Screen.width - 80f) / GomokuRules.Size,
                (Screen.height - 170f) / GomokuRules.Size));
            float side = cell * GomokuRules.Size;
            // 棋盘左上角：整体在屏幕内居中，略向下偏移给比分栏留白。
            var origin = new Vector2((Screen.width - side) * 0.5f, (Screen.height - side) * 0.5f + 14f);
            var boardRect = new Rect(origin.x, origin.y, side, side);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, side + 8f, side + 8f), BoardBorder);
            Fill(boardRect, BoardBackground);
            DrawGrid(origin, cell);
            DrawStarPoints(origin, cell);

            // 光标高亮先于棋子绘制，空格上清晰可见。
            Fill(CellRect(origin, cell, _bootstrap!.SelectedRow, _bootstrap.SelectedCol, 0.06f), CursorColor);

            for (int row = 0; row < GomokuRules.Size; row++)
            {
                for (int col = 0; col < GomokuRules.Size; col++)
                {
                    var occupant = board[row, col];
                    if (occupant == Cell.None) continue;

                    Fill(CellRect(origin, cell, row, col, 0.14f), occupant == Cell.Black ? BlackStone : WhiteStone);
                }
            }

            if (board.LastMove is CellPos last) DrawLastMoveMark(origin, cell, last);
            var line = board.FindWinningLine();
            if (line != null) DrawWinHighlight(origin, cell, line);

            HandleClick(boardRect, origin, cell);
        }

        /// <summary>画格子间的细分隔线。</summary>
        private static void DrawGrid(Vector2 origin, float cell)
        {
            float thickness = Mathf.Max(1f, cell * 0.03f);
            float side = cell * GomokuRules.Size;
            for (int i = 1; i < GomokuRules.Size; i++)
            {
                float offset = i * cell;
                Fill(new Rect(origin.x + offset - thickness * 0.5f, origin.y, thickness, side), GridLine);
                Fill(new Rect(origin.x, origin.y + offset - thickness * 0.5f, side, thickness), GridLine);
            }
        }

        /// <summary>画天元与四个星位小点（15 路棋盘星位在 3、7、11 线交点）。</summary>
        private static void DrawStarPoints(Vector2 origin, float cell)
        {
            int edge = 3;
            int far = GomokuRules.Size - 1 - edge;
            DrawStar(origin, cell, edge, edge);
            DrawStar(origin, cell, edge, far);
            DrawStar(origin, cell, far, edge);
            DrawStar(origin, cell, far, far);
            DrawStar(origin, cell, GomokuRules.Center, GomokuRules.Center);
        }

        private static void DrawStar(Vector2 origin, float cell, int row, int col)
        {
            float size = Mathf.Max(3f, cell * 0.14f);
            Fill(new Rect(
                origin.x + col * cell + (cell - size) * 0.5f,
                origin.y + row * cell + (cell - size) * 0.5f,
                size, size), StarPoint);
        }

        /// <summary>把 [行,列] 坐标换算为 IMGUI 矩形：行 0 位于棋盘最上行，insetRatio 控制色块大小。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int col, float insetRatio)
        {
            float inset = cell * insetRatio;
            return new Rect(origin.x + col * cell + inset, origin.y + row * cell + inset, cell - inset * 2f, cell - inset * 2f);
        }

        /// <summary>在最后一手棋子中心画一枚红色小点标记。</summary>
        private static void DrawLastMoveMark(Vector2 origin, float cell, CellPos last)
        {
            float size = Mathf.Max(3f, cell * 0.14f);
            var stoneRect = CellRect(origin, cell, last.Row, last.Col, 0.14f);
            Fill(new Rect(stoneRect.center.x - size * 0.5f, stoneRect.center.y - size * 0.5f, size, size), LastMoveMark);
        }

        /// <summary>给获胜连线的每枚棋子画一圈白色描边。</summary>
        private static void DrawWinHighlight(Vector2 origin, float cell, IReadOnlyList<CellPos> line)
        {
            float thickness = Mathf.Max(2f, cell * 0.06f);
            foreach (var pos in line)
            {
                var rect = CellRect(origin, cell, pos.Row, pos.Col, 0.10f);
                Fill(new Rect(rect.x, rect.y, rect.width, thickness), WinHighlight);
                Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), WinHighlight);
                Fill(new Rect(rect.x, rect.y, thickness, rect.height), WinHighlight);
                Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), WinHighlight);
            }
        }

        /// <summary>左键按下时把鼠标位置换算为行列坐标，交给引导层由核心规则裁决。</summary>
        private void HandleClick(Rect boardRect, Vector2 origin, float cell)
        {
            if (_bootstrap == null) return;
            var click = Event.current;
            if (click.type != EventType.MouseDown || click.button != 0) return;
            if (!boardRect.Contains(click.mousePosition)) return;

            int col = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.x - origin.x) / cell), 0, GomokuRules.Size - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.y - origin.y) / cell), 0, GomokuRules.Size - 1);
            _bootstrap.SelectCell(row, col);
            _bootstrap.PlayAt(row, col);
            click.Use();
        }

        private void DrawEventLog(GomokuMatch match)
        {
            var events = match.Events;
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

        private void DrawOverlay(GomokuMatch match)
        {
            var board = match.Board;
            if (!board.IsFinished) return;

            string title = board.Outcome == GomokuOutcome.BlackWins ? "你赢了！"
                : board.Outcome == GomokuOutcome.WhiteWins ? "AI 获胜" : "平局";
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"累计比分 玩家(黑) {match.PlayerWins} : {match.AiWins} AI(白)，平局 {match.Draws}    按 R 再来一局",
                StatusStyle());
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
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
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
