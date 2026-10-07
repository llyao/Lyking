using Lyking.Reversi.Core;
using UnityEngine;

namespace Lyking.Reversi.Unity
{
    /// <summary>
    /// 黑白棋演示面板：IMGUI 绘制比分栏、8x8 绿底棋盘格、黑白棋子、合法步提示点、
    /// 最后落子标记、光标高亮、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，鼠标点击换算格子直接落子，
    /// 不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class ReversiDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBorder = new Color(0.05f, 0.18f, 0.10f);
        private static readonly Color GridLight = new Color(0.16f, 0.44f, 0.24f);
        private static readonly Color GridDark = new Color(0.12f, 0.38f, 0.20f);
        private static readonly Color BlackColor = new Color(0.10f, 0.11f, 0.12f);
        private static readonly Color WhiteColor = new Color(0.93f, 0.94f, 0.96f);
        private static readonly Color HintColor = new Color(0.95f, 0.85f, 0.30f, 0.90f);
        private static readonly Color CursorColor = new Color(0.30f, 0.80f, 0.95f, 0.40f);
        private static readonly Color LastMoveColor = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private ReversiDemoBootstrap? _bootstrap;

        public void Bind(ReversiDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasMatch) return;

            var match = _bootstrap.Match;
            DrawScore(match);
            DrawBoard(match);
            DrawEventLog(match);
            DrawOverlay(match);
        }

        private void DrawScore(ReversiMatch match)
        {
            var board = match.Board;
            var counts = board.Counts;
            string state;
            if (board.IsFinished)
            {
                state = board.Outcome == ReversiOutcome.BlackWins ? "你赢了"
                    : board.Outcome == ReversiOutcome.WhiteWins ? "AI 获胜" : "平局";
            }
            else
            {
                state = board.CurrentTurn == Cell.Black ? "你的回合 (黑)" : "AI 思考中 (白)";
            }

            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {match.Round} 局    黑 {counts.Black} : {counts.White} 白    第 {board.MoveCount + 1} 手    状态 {state}",
                StatusStyle());
        }

        private void DrawBoard(ReversiMatch match)
        {
            var board = match.Board;
            float cell = Mathf.Max(48f, Mathf.Min(Screen.height - 190f, Screen.width - 80f)) / ReversiRules.Size;
            float boardSide = cell * ReversiRules.Size;
            // 棋盘左上角：整体在屏幕内居中，略向下偏移给比分栏留白。
            var origin = new Vector2((Screen.width - boardSide) * 0.5f, (Screen.height - boardSide) * 0.5f + 14f);
            var boardRect = new Rect(origin.x, origin.y, boardSide, boardSide);

            Fill(new Rect(origin.x - 5f, origin.y - 5f, boardSide + 10f, boardSide + 10f), BoardBorder);

            for (int row = 0; row < ReversiRules.Size; row++)
            {
                for (int col = 0; col < ReversiRules.Size; col++)
                {
                    Fill(CellRect(origin, cell, row, col), (row + col) % 2 == 0 ? GridLight : GridDark);
                }
            }

            for (int row = 0; row < ReversiRules.Size; row++)
            {
                for (int col = 0; col < ReversiRules.Size; col++)
                {
                    var occupant = board[row, col];
                    if (occupant == Cell.None) continue;

                    // 黑子白子用带内缩的纯色方块呈现。
                    float inset = cell * 0.14f;
                    var rect = new Rect(origin.x + col * cell + inset, origin.y + row * cell + inset,
                        cell - inset * 2f, cell - inset * 2f);
                    Fill(rect, occupant == Cell.Black ? BlackColor : WhiteColor);
                }
            }

            DrawLastMoveMark(board, origin, cell);
            if (match.IsPlayerTurn)
            {
                DrawCursor(origin, cell);
                DrawLegalHints(board, origin, cell);
            }

            HandleClick(boardRect, origin, cell);
        }

        /// <summary>在黑方每个合法格中心绘制提示点。</summary>
        private static void DrawLegalHints(ReversiBoard board, Vector2 origin, float cell)
        {
            var moves = board.LegalMoves(Cell.Black);
            float size = cell * 0.18f;
            for (int i = 0; i < moves.Count; i++)
            {
                var center = CellCenter(origin, cell, moves[i].Row, moves[i].Col);
                Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), HintColor);
            }
        }

        /// <summary>光标所在格叠加半透明高亮。</summary>
        private void DrawCursor(Vector2 origin, float cell)
        {
            if (_bootstrap == null) return;

            Fill(CellRect(origin, cell, _bootstrap.CursorRow, _bootstrap.CursorCol), CursorColor);
        }

        /// <summary>用四条边框标出最后一步落点。</summary>
        private static void DrawLastMoveMark(ReversiBoard board, Vector2 origin, float cell)
        {
            if (!board.LastMove.HasValue) return;

            var (row, col) = board.LastMove.Value;
            const float thickness = 3f;
            float inset = cell * 0.10f;
            var rect = new Rect(origin.x + col * cell + inset, origin.y + row * cell + inset,
                cell - inset * 2f, cell - inset * 2f);
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), LastMoveColor);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), LastMoveColor);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), LastMoveColor);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), LastMoveColor);
        }

        /// <summary>把格子坐标换算为 IMGUI 矩形：索引 [0,0] 位于棋盘左上格。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int row, int col)
        {
            return new Rect(origin.x + col * cell, origin.y + row * cell, cell, cell);
        }

        private static Vector2 CellCenter(Vector2 origin, float cell, int row, int col)
        {
            return new Vector2(origin.x + (col + 0.5f) * cell, origin.y + (row + 0.5f) * cell);
        }

        /// <summary>左键按下时把鼠标位置换算为格子坐标，交给引导层由核心规则裁决。</summary>
        private void HandleClick(Rect boardRect, Vector2 origin, float cell)
        {
            if (_bootstrap == null) return;

            var click = Event.current;
            if (click.type != EventType.MouseDown || click.button != 0) return;
            if (!boardRect.Contains(click.mousePosition)) return;

            int col = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.x - origin.x) / cell), 0, ReversiRules.Size - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.y - origin.y) / cell), 0, ReversiRules.Size - 1);
            _bootstrap.PlayAt(row, col);
            click.Use();
        }

        private void DrawEventLog(ReversiMatch match)
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

        private void DrawOverlay(ReversiMatch match)
        {
            var board = match.Board;
            if (!board.IsFinished) return;

            string title = board.Outcome == ReversiOutcome.BlackWins ? "你赢了！"
                : board.Outcome == ReversiOutcome.WhiteWins ? "AI 获胜" : "平局";
            var counts = board.Counts;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), title, TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"终局 黑 {counts.Black} : {counts.White} 白    累计比分 玩家 {match.PlayerWins} : {match.AiWins} AI，平局 {match.Draws}    按 R 再来一局",
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
