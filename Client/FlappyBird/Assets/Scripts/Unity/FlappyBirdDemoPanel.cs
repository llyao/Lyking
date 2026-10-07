using Lyking.FlappyBird.Core;
using UnityEngine;

namespace Lyking.FlappyBird.Unity
{
    /// <summary>
    /// Flappy 演示面板：IMGUI 绘制天空、地面、管道、小鸟、顶部分数、开始/结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与精灵。
    /// </summary>
    public sealed class FlappyBirdDemoPanel : MonoBehaviour
    {
        private static readonly Color SkyColor = new Color(0.53f, 0.81f, 0.92f);
        private static readonly Color FieldBorder = new Color(0.35f, 0.42f, 0.32f);
        private static readonly Color GroundColor = new Color(0.76f, 0.60f, 0.35f);
        private static readonly Color PipeColor = new Color(0.22f, 0.65f, 0.28f);
        private static readonly Color BirdColor = new Color(0.98f, 0.80f, 0.20f);
        private static readonly Color TextColor = new Color(0.15f, 0.20f, 0.24f);

        private FlappyBirdDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(FlappyBirdDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasBoard) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var board = _bootstrap.Board;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), SkyColor);
            DrawField(board);
            DrawStatus(board);
            DrawEventLog(board);
            DrawOverlay(board);
        }

        private void DrawField(FlappyBirdGame board)
        {
            float cell = Mathf.Min((Screen.height - 140f) / board.FieldHeight, (Screen.width - 80f) / board.FieldWidth);
            float fieldWidth = cell * board.FieldWidth;
            float fieldHeight = cell * board.FieldHeight;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), SkyColor);

            var rules = board.Rules;
            // 地面条纹画在场地底部，提示触地即死。
            Fill(FieldRect(origin, cell, board.FieldHeight, 0f, 0f, board.FieldWidth, 0.4f), GroundColor);

            float gapHalf = rules.GapHeight * 0.5f;
            foreach (var pipe in board.Pipes)
            {
                float left = pipe.Center.X - rules.PipeWidth * 0.5f;
                float bottomHeight = Mathf.Max(0f, pipe.Center.Y - gapHalf);
                float topStart = pipe.Center.Y + gapHalf;
                Fill(FieldRect(origin, cell, board.FieldHeight, left, 0f, rules.PipeWidth, bottomHeight), PipeColor);
                Fill(FieldRect(origin, cell, board.FieldHeight, left, topStart, rules.PipeWidth, board.FieldHeight - topStart), PipeColor);
            }

            var bird = board.Position;
            Fill(FieldRect(origin, cell, board.FieldHeight,
                bird.X - rules.BirdHalfWidth, bird.Y - rules.BirdHalfHeight,
                rules.BirdHalfWidth * 2f, rules.BirdHalfHeight * 2f), BirdColor);
        }

        /// <summary>游戏矩形（逻辑坐标 Y 向上，给定左下角与尺寸）转 IMGUI 矩形（Y 向下），origin 为场地左上角。</summary>
        private static Rect FieldRect(Vector2 origin, float cell, float fieldHeight, float x, float y, float width, float height)
        {
            float left = origin.x + x * cell;
            float top = origin.y + (fieldHeight - y - height) * cell;
            return new Rect(left, top, width * cell, height * cell);
        }

        private void DrawStatus(FlappyBirdGame board)
        {
            string phase = board.Phase switch
            {
                FlappyPhase.Ready => "就绪",
                FlappyPhase.Running => "飞行中",
                _ => "结束",
            };
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"分数 {board.Score}    状态 {phase}    管道 {board.Pipes.Count}    物理步 {board.StepIndex}",
                StatusStyle());
        }

        private void DrawEventLog(FlappyBirdGame board)
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

        private void DrawOverlay(FlappyBirdGame board)
        {
            if (board.Phase == FlappyPhase.Ready)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 40f), "点按空格或鼠标左键起飞", TitleStyle());
            }
            else if (board.IsFinished)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 44f), "游戏结束", TitleStyle());
                GUI.Label(new Rect(0f, Screen.height * 0.38f + 48f, Screen.width, 28f), $"最终分数 {board.Score}    按 R 重新开始", StatusStyle());
            }
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
