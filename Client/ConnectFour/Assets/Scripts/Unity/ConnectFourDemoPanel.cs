using System.Collections.Generic;
using Lyking.ConnectFour.Core;
using UnityEngine;

namespace Lyking.ConnectFour.Unity
{
    /// <summary>
    /// 四子连珠演示面板：IMGUI 绘制比分栏、7x6 棋盘、列光标、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，蓝色底板上的圆形空洞
    /// 简化为格内色块，红/黄棋子用色块绘制，鼠标点击换算列直接落子，
    /// 不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class ConnectFourDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.13f, 0.35f, 0.72f);
        private static readonly Color BoardBorder = new Color(0.09f, 0.24f, 0.52f);
        private static readonly Color HoleColor = new Color(0.05f, 0.07f, 0.12f);
        private static readonly Color RedColor = new Color(0.85f, 0.20f, 0.16f);
        private static readonly Color YellowColor = new Color(0.95f, 0.78f, 0.15f);
        private static readonly Color CursorColor = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color WinHighlight = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private ConnectFourDemoBootstrap? _bootstrap;

        public void Bind(ConnectFourDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasMatch) return;

            var match = _bootstrap.Match;
            DrawScore(match);
            DrawBoard(match);
            DrawEventLog(match);
            DrawOverlay(match);
        }

        private void DrawScore(ConnectFourMatch match)
        {
            var board = match.Board;
            string state;
            if (board.IsFinished)
            {
                state = board.Outcome == ConnectFourOutcome.RedWins ? "你赢了"
                    : board.Outcome == ConnectFourOutcome.YellowWins ? "AI 获胜" : "平局";
            }
            else
            {
                state = board.CurrentTurn == Cell.Red ? "你的回合 (红)" : "AI 思考中 (黄)";
            }

            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {match.Round} 局    玩家胜 {match.PlayerWins}    AI 胜 {match.AiWins}    平局 {match.Draws}    状态 {state}    光标 第 {_bootstrap!.SelectedColumn + 1} 列",
                StatusStyle());
        }

        private void DrawBoard(ConnectFourMatch match)
        {
            var board = match.Board;
            float cell = Mathf.Max(40f, Mathf.Min(
                (Screen.width - 80f) / ConnectFourRules.Columns,
                (Screen.height - 170f) / ConnectFourRules.Rows));
            float boardWidth = cell * ConnectFourRules.Columns;
            float boardHeight = cell * ConnectFourRules.Rows;
            // 棋盘左上角：整体在屏幕内居中，略向下偏移给比分栏留白。
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);
            var boardRect = new Rect(origin.x, origin.y, boardWidth, boardHeight);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);
            Fill(boardRect, BoardBackground);

            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                for (int row = 0; row < ConnectFourRules.Rows; row++)
                {
                    Fill(HoleRect(origin, cell, col, row, 0.14f), HoleColor);
                }
            }

            for (int col = 0; col < ConnectFourRules.Columns; col++)
            {
                for (int row = 0; row < ConnectFourRules.Rows; row++)
                {
                    var occupant = board[col, row];
                    if (occupant == Cell.None) continue;

                    Fill(HoleRect(origin, cell, col, row, 0.08f), occupant == Cell.Red ? RedColor : YellowColor);
                }
            }

            if (board.WinningLine != null) DrawWinHighlight(origin, cell, board.WinningLine);
            Fill(new Rect(origin.x + _bootstrap!.SelectedColumn * cell, origin.y, cell, boardHeight), CursorColor);

            HandleClick(boardRect, origin, cell);
        }

        /// <summary>给获胜四连的每枚棋子画一圈白色描边。</summary>
        private static void DrawWinHighlight(Vector2 origin, float cell, IReadOnlyList<CellPos> line)
        {
            float thickness = Mathf.Max(2f, cell * 0.06f);
            foreach (var pos in line)
            {
                var rect = HoleRect(origin, cell, pos.Col, pos.Row, 0.10f);
                Fill(new Rect(rect.x, rect.y, rect.width, thickness), WinHighlight);
                Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), WinHighlight);
                Fill(new Rect(rect.x, rect.y, thickness, rect.height), WinHighlight);
                Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), WinHighlight);
            }
        }

        /// <summary>把 [列,行] 换算为 IMGUI 矩形：行 0 位于棋盘最底层，inset 控制圆点大小。</summary>
        private static Rect HoleRect(Vector2 origin, float cell, int col, int row, float insetRatio)
        {
            float inset = cell * insetRatio;
            float x = origin.x + col * cell + inset;
            float y = origin.y + (ConnectFourRules.Rows - 1 - row) * cell + inset;
            return new Rect(x, y, cell - inset * 2f, cell - inset * 2f);
        }

        /// <summary>左键按下时把鼠标位置换算为列，交给引导层由核心规则裁决。</summary>
        private void HandleClick(Rect boardRect, Vector2 origin, float cell)
        {
            if (_bootstrap == null) return;
            var click = Event.current;
            if (click.type != EventType.MouseDown || click.button != 0) return;
            if (!boardRect.Contains(click.mousePosition)) return;

            int col = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.x - origin.x) / cell), 0, ConnectFourRules.Columns - 1);
            _bootstrap.SelectColumn(col);
            _bootstrap.PlayColumn(col);
            click.Use();
        }

        private void DrawEventLog(ConnectFourMatch match)
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

        private void DrawOverlay(ConnectFourMatch match)
        {
            var board = match.Board;
            if (!board.IsFinished) return;

            string title = board.Outcome == ConnectFourOutcome.RedWins ? "你赢了！"
                : board.Outcome == ConnectFourOutcome.YellowWins ? "AI 获胜" : "平局";
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"累计比分 玩家 {match.PlayerWins} : {match.AiWins} AI，平局 {match.Draws}    按 R 再来一局",
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
