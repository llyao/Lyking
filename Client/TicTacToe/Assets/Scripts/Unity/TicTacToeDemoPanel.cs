using Lyking.TicTacToe.Core;
using UnityEngine;

namespace Lyking.TicTacToe.Unity
{
    /// <summary>
    /// 井字棋演示面板：IMGUI 绘制比分栏、3x3 棋盘、终局提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，X/O 用大号字符绘制，
    /// 鼠标点击换算格子直接落子，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class TicTacToeDemoPanel : MonoBehaviour
    {
        private static readonly Color BoardBackground = new Color(0.16f, 0.18f, 0.15f);
        private static readonly Color BoardBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color GridLine = new Color(0.42f, 0.50f, 0.40f);
        private static readonly Color XColor = new Color(0.32f, 0.56f, 0.90f);
        private static readonly Color OColor = new Color(0.92f, 0.48f, 0.28f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private TicTacToeDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(TicTacToeDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasMatch) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var match = _bootstrap.Match;
            DrawScore(match);
            DrawBoard(match);
            DrawEventLog(match);
            DrawOverlay(match);
        }

        private void DrawScore(TicTacToeMatch match)
        {
            var board = match.Board;
            string state;
            if (board.IsFinished)
            {
                state = board.Outcome == TicTacToeOutcome.XWins ? "你赢了"
                    : board.Outcome == TicTacToeOutcome.OWins ? "AI 获胜" : "平局";
            }
            else
            {
                state = board.CurrentTurn == Cell.X ? "你的回合 (X)" : "AI 思考中 (O)";
            }

            GUI.Label(new Rect(12f, 8f, Screen.width - 24f, 28f),
                $"第 {match.Round} 局    玩家胜 {match.PlayerWins}    AI 胜 {match.AiWins}    平局 {match.Draws}    状态 {state}",
                StatusStyle());
        }

        private void DrawBoard(TicTacToeMatch match)
        {
            var board = match.Board;
            float cell = Mathf.Max(60f, Mathf.Min(Screen.height - 160f, Screen.width - 80f)) / TicTacToeRules.Size;
            float boardSide = cell * TicTacToeRules.Size;
            // 棋盘左上角：整体在屏幕内居中，略向下偏移给比分栏留白。
            var origin = new Vector2((Screen.width - boardSide) * 0.5f, (Screen.height - boardSide) * 0.5f + 14f);
            var boardRect = new Rect(origin.x, origin.y, boardSide, boardSide);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardSide + 8f, boardSide + 8f), BoardBorder);
            Fill(boardRect, BoardBackground);
            DrawGrid(origin, cell);

            for (int i = 0; i < board.Cells.Count; i++)
            {
                var occupant = board.Cells[i];
                if (occupant == Cell.None) continue;

                var style = MarkStyle(occupant == Cell.X ? XColor : OColor, Mathf.RoundToInt(cell * 0.55f));
                GUI.Label(CellRect(origin, cell, i), occupant == Cell.X ? "X" : "O", style);
            }

            HandleClick(boardRect, origin, cell);
        }

        private void DrawGrid(Vector2 origin, float cell)
        {
            const float thickness = 3f;
            for (int i = 1; i < TicTacToeRules.Size; i++)
            {
                float offset = i * cell;
                Fill(new Rect(origin.x + offset - thickness * 0.5f, origin.y, thickness, cell * TicTacToeRules.Size), GridLine);
                Fill(new Rect(origin.x, origin.y + offset - thickness * 0.5f, cell * TicTacToeRules.Size, thickness), GridLine);
            }
        }

        /// <summary>把格子索引换算为 IMGUI 矩形：行优先布局，索引 0 位于棋盘左上格。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int index)
        {
            int row = index / TicTacToeRules.Size;
            int col = index % TicTacToeRules.Size;
            float inset = cell * 0.12f;
            return new Rect(origin.x + col * cell + inset, origin.y + row * cell + inset, cell - inset * 2f, cell - inset * 2f);
        }

        /// <summary>左键按下时把鼠标位置换算为格子索引，交给引导层由核心规则裁决。</summary>
        private void HandleClick(Rect boardRect, Vector2 origin, float cell)
        {
            if (_bootstrap == null) return;
            var click = Event.current;
            if (click.type != EventType.MouseDown || click.button != 0) return;
            if (!boardRect.Contains(click.mousePosition)) return;

            int col = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.x - origin.x) / cell), 0, TicTacToeRules.Size - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((click.mousePosition.y - origin.y) / cell), 0, TicTacToeRules.Size - 1);
            _bootstrap.PlayCell(row * TicTacToeRules.Size + col);
            click.Use();
        }

        private void DrawEventLog(TicTacToeMatch match)
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

        private void DrawOverlay(TicTacToeMatch match)
        {
            var board = match.Board;
            if (!board.IsFinished) return;

            string title = board.Outcome == TicTacToeOutcome.XWins ? "你赢了！"
                : board.Outcome == TicTacToeOutcome.OWins ? "AI 获胜" : "平局";
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

        private static GUIStyle MarkStyle(Color color, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, fontSize), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = color;
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
