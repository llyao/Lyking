using System.Text;
using Lyking.Frogger.Core;
using UnityEngine;

namespace Lyking.Frogger.Unity
{
    /// <summary>
    /// 青蛙过河演示面板：IMGUI 绘制分色场地、车辆、木排、荷花位、青蛙、状态栏、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class FroggerDemoPanel : MonoBehaviour
    {
        private static readonly Color GrassColor = new Color(0.23f, 0.45f, 0.21f);
        private static readonly Color RoadColor = new Color(0.16f, 0.16f, 0.19f);
        private static readonly Color RiverColor = new Color(0.13f, 0.29f, 0.55f);
        private static readonly Color GoalColor = new Color(0.11f, 0.34f, 0.19f);
        private static readonly Color FreeLotusColor = new Color(0.95f, 0.75f, 0.82f);
        private static readonly Color FilledLotusColor = new Color(0.95f, 0.80f, 0.30f);
        private static readonly Color CarColor = new Color(0.92f, 0.62f, 0.20f);
        private static readonly Color LogColor = new Color(0.55f, 0.38f, 0.20f);
        private static readonly Color FrogColor = new Color(0.35f, 0.85f, 0.35f);
        private static readonly Color FieldBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private FroggerDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(FroggerDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasBoard) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var board = _bootstrap.Board;
            string lifecycle = board.IsGameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"分数 {board.Score}    生命 {board.Lives}    进度 {board.GoalsFilled}/{board.Rules.GoalCount}    轮次 {board.Round}    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(board);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawBoard(FroggerBoard board)
        {
            float cell = Mathf.Min((Screen.height - 140f) / board.Height, (Screen.width - 80f) / board.Width);
            float boardWidth = cell * board.Width;
            float boardHeight = cell * board.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), FieldBorder);

            // 逐行分色：草地、马路、河道与对岸各一块，马路与河道再叠上滚动实体。
            for (int row = 0; row < board.Height; row++)
            {
                float top = origin.y + (board.Height - 1 - row) * cell;
                Fill(new Rect(origin.x, top, boardWidth, cell), RowColor(board.Rules.RowTypeFor(row)));
            }

            foreach (var lane in board.Lanes)
            {
                float halfLength = lane.EntityLength * 0.5f;
                for (int i = 0; i < lane.EntityCount; i++)
                {
                    // 环绕补画：实体越出场界时在另一侧补一次，滚动看起来首尾相接。
                    for (int wrap = -1; wrap <= 1; wrap++)
                    {
                        float center = lane.Positions[i] + wrap * board.Width;
                        if (center + halfLength < -0.5f || center - halfLength > board.Width - 0.5f) continue;
                        Fill(WorldRect(origin, cell, board.Height, center, lane.Row, halfLength, 0.35f),
                            lane.Kind == FroggerLaneKind.Road ? CarColor : LogColor);
                    }
                }
            }

            for (int i = 0; i < board.Rules.GoalColumns.Count; i++)
            {
                var rect = WorldRect(origin, cell, board.Height, board.Rules.GoalColumns[i], board.Rules.GoalRow,
                    0.32f, 0.32f);
                Fill(rect, board.IsGoalFilled(i) ? FilledLotusColor : FreeLotusColor);
            }

            Fill(WorldRect(origin, cell, board.Height, board.FrogX, board.FrogRow,
                board.Rules.FrogHalfWidth, board.Rules.FrogHalfWidth), FrogColor);
        }

        private static Color RowColor(FroggerRowType type) => type switch
        {
            FroggerRowType.Road => RoadColor,
            FroggerRowType.River => RiverColor,
            FroggerRowType.Goal => GoalColor,
            _ => GrassColor,
        };

        /// <summary>世界坐标（格中心、Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为场地左上角。</summary>
        private static Rect WorldRect(Vector2 origin, float cell, int height, float centerX, int row,
            float halfWidth, float halfHeight)
        {
            float left = origin.x + (centerX + 0.5f - halfWidth) * cell;
            float top = origin.y + (height - 1 - row + 0.5f - halfHeight) * cell;
            return new Rect(left, top, halfWidth * 2f * cell, halfHeight * 2f * cell);
        }

        private void DrawEventLog(FroggerBoard board)
        {
            var events = board.Events;
            int count = Mathf.Min(6, events.Count);
            var builder = new StringBuilder();
            builder.Append("事件日志: ");
            for (int i = events.Count - count; i < events.Count; i++)
            {
                if (i > events.Count - count) builder.Append("    ");
                builder.Append(events[i].ToString());
            }

            GUI.Label(new Rect(12f, Screen.height - 34f, Screen.width - 24f, 24f), builder.ToString(), LogStyle());
        }

        private void DrawOverlay(FroggerBoard board)
        {
            if (!board.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "生命用尽，游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {board.Score}    进度 {board.GoalsFilled}/{board.Rules.GoalCount}    按 R 重新开始", StatusStyle());
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
