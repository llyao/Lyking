using Lyking.Tetris.Core;
using UnityEngine;

namespace Lyking.Tetris.Unity
{
    /// <summary>
    /// 俄罗斯方块演示面板：IMGUI 绘制状态栏、井体、Next 预览、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产。
    /// </summary>
    public sealed class TetrisDemoPanel : MonoBehaviour
    {
        private static readonly Color WellBackground = new Color(0.14f, 0.15f, 0.18f);
        private static readonly Color WellBorder = new Color(0.36f, 0.42f, 0.52f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private TetrisDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(TetrisDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasBoard) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var board = _bootstrap.Board;
            string lifecycle = board.IsGameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 760f, 26f),
                $"分数 {board.Score}    等级 {board.Level}    消行 {board.LinesCleared}    步进 {board.StepSeconds:0.00} 秒    下一块 {board.NextKind}    状态 {lifecycle}",
                StatusStyle());

            DrawWell(board);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawWell(TetrisBoard board)
        {
            float cell = Mathf.Min((Screen.height - 140f) / board.Height, (Screen.width - 120f) / board.Width);
            float wellWidth = cell * board.Width;
            float wellHeight = cell * board.Height;
            var origin = new Vector2((Screen.width - wellWidth) * 0.5f, (Screen.height - wellHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, wellWidth + 8f, wellHeight + 8f), WellBorder);
            Fill(new Rect(origin.x, origin.y, wellWidth, wellHeight), WellBackground);

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var kind = board.CellAt(x, y);
                    if (kind is null) continue;
                    var rect = CellRect(origin, cell, board.Height, new GridPos(x, y));
                    Fill(rect, Color.Lerp(KindColor(kind.Value), Color.black, 0.25f));
                }
            }

            var pieceColor = KindColor(board.CurrentKind);
            foreach (var pos in board.PieceCells)
            {
                Fill(CellRect(origin, cell, board.Height, pos), pieceColor);
            }

            DrawNextPreview(board, origin, wellWidth, wellHeight, cell);
        }

        /// <summary>在井体右侧绘制 Next 预览：取旋转态 0 的形状，在 4 格见方的框内居中摆放。</summary>
        private void DrawNextPreview(TetrisBoard board, Vector2 wellOrigin, float wellWidth, float wellHeight, float cell)
        {
            float previewCell = cell * 0.6f;
            float boxSize = previewCell * 4f;
            var boxOrigin = new Vector2(wellOrigin.x + wellWidth + 18f, wellOrigin.y + wellHeight - boxSize);

            Fill(new Rect(boxOrigin.x - 4f, boxOrigin.y - 4f, boxSize + 8f, boxSize + 8f), WellBorder);
            Fill(new Rect(boxOrigin.x, boxOrigin.y, boxSize, boxSize), WellBackground);

            var cells = TetrominoTable.Cells(board.NextKind, 0);
            int minDx = int.MaxValue, maxDx = int.MinValue, minDy = int.MaxValue, maxDy = int.MinValue;
            foreach (var offset in cells)
            {
                minDx = Mathf.Min(minDx, offset.X);
                maxDx = Mathf.Max(maxDx, offset.X);
                minDy = Mathf.Min(minDy, offset.Y);
                maxDy = Mathf.Max(maxDy, offset.Y);
            }

            float left0 = boxOrigin.x + (boxSize - (maxDx - minDx + 1) * previewCell) * 0.5f;
            float top0 = boxOrigin.y + (boxSize - (maxDy - minDy + 1) * previewCell) * 0.5f;
            foreach (var offset in cells)
            {
                float left = left0 + (offset.X - minDx) * previewCell;
                float top = top0 + (maxDy - offset.Y) * previewCell;
                Fill(new Rect(left, top, previewCell - 1f, previewCell - 1f), KindColor(board.NextKind));
            }
        }

        /// <summary>井格坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为井体左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int height, GridPos pos)
        {
            float left = origin.x + pos.X * cell;
            float top = origin.y + (height - 1 - pos.Y) * cell;
            return new Rect(left, top, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(TetrisBoard board)
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

        private void DrawOverlay(TetrisBoard board)
        {
            if (!board.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {board.Score}    消行 {board.LinesCleared}    按 R 重新开始", StatusStyle());
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        /// <summary>七种方块的代表色：I 青、O 黄、T 紫、S 绿、Z 红、J 蓝、L 橙。</summary>
        private static Color KindColor(TetrominoKind kind) => kind switch
        {
            TetrominoKind.I => new Color(0.30f, 0.80f, 0.90f),
            TetrominoKind.O => new Color(0.92f, 0.78f, 0.24f),
            TetrominoKind.T => new Color(0.72f, 0.42f, 0.88f),
            TetrominoKind.S => new Color(0.42f, 0.85f, 0.42f),
            TetrominoKind.Z => new Color(0.90f, 0.36f, 0.30f),
            TetrominoKind.J => new Color(0.36f, 0.52f, 0.92f),
            _ => new Color(0.92f, 0.62f, 0.24f),
        };

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
