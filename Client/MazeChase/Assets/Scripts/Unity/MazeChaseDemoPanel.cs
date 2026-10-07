using System.Text;
using Lyking.MazeChase.Core;
using UnityEngine;

namespace Lyking.MazeChase.Unity
{
    /// <summary>
    /// 吃豆迷宫演示面板：IMGUI 绘制状态栏、迷宫、角色、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产。
    /// </summary>
    public sealed class MazeChaseDemoPanel : MonoBehaviour
    {
        private static readonly Color WallColor = new Color(0.18f, 0.24f, 0.62f);
        private static readonly Color CorridorColor = new Color(0.04f, 0.04f, 0.08f);
        private static readonly Color PelletColor = new Color(0.95f, 0.86f, 0.66f);
        private static readonly Color PowerPelletColor = new Color(0.98f, 0.72f, 0.32f);
        private static readonly Color PlayerColor = new Color(0.98f, 0.85f, 0.20f);
        private static readonly Color FrightenedColor = new Color(0.28f, 0.38f, 0.95f);
        private static readonly Color RespawningColor = new Color(0.30f, 0.30f, 0.34f);
        private static readonly Color[] GhostColors =
        {
            new Color(0.90f, 0.28f, 0.26f),
            new Color(0.96f, 0.58f, 0.78f),
            new Color(0.36f, 0.86f, 0.86f),
        };
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private MazeChaseDemoBootstrap? _bootstrap;

        public void Bind(MazeChaseDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            var game = _bootstrap.Game;

            string state = game.IsGameOver ? "结束" : game.IsFrightened ? "惊惧" : "追击";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"分数 {game.Score}    剩余豆 {game.PelletsRemaining}    生命 {game.Lives}    第 {game.Level} 关    惊惧剩余 {game.FrightenedRemaining:0.0}s    状态 {state}",
                StatusStyle());

            DrawBoard(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawBoard(MazeChaseGame game)
        {
            float cell = Mathf.Min((Screen.height - 150f) / game.Height, (Screen.width - 60f) / game.Width);
            float boardWidth = cell * game.Width;
            float boardHeight = cell * game.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 12f);

            // 先铺迷宫与豆子，再画玩家与幽灵，保证角色压在通道之上。
            for (int y = 0; y < game.Height; y++)
            {
                for (int x = 0; x < game.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    var rect = CellRect(origin, cell, game.Height, x, y);
                    if (game.Map.IsWall(pos))
                    {
                        Fill(rect, WallColor);
                        continue;
                    }

                    Fill(rect, CorridorColor);
                    if (game.PelletCells.Contains(pos))
                        Fill(CenterRect(origin, cell, game.Height, x, y, cell * 0.28f), PelletColor);
                    else if (game.PowerPelletCells.Contains(pos))
                        Fill(CenterRect(origin, cell, game.Height, x, y, cell * 0.58f), PowerPelletColor);
                }
            }

            Fill(CenterRect(origin, cell, game.Height, game.PlayerX, game.PlayerY, cell * 0.68f), PlayerColor);
            for (int i = game.Ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = game.Ghosts[i];
                Color color = ghost.IsRespawning ? RespawningColor
                    : ghost.IsFrightened ? FrightenedColor
                    : GhostColors[ghost.Index % GhostColors.Length];
                Fill(CenterRect(origin, cell, game.Height, ghost.X, ghost.Y, cell * 0.68f), color);
            }
        }

        /// <summary>格点（整数坐标中心）转 IMGUI 矩形（Y 向下），每个格占满一个 cell 方块。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int height, int x, int y) =>
            new Rect(origin.x + x * cell, origin.y + (height - 1 - y) * cell, cell, cell);

        /// <summary>连续坐标（格点为整数中心）转以中心对齐的 IMGUI 矩形（Y 向下）。</summary>
        private static Rect CenterRect(Vector2 origin, float cell, int height, float x, float y, float size) =>
            new Rect(origin.x + (x + 0.5f) * cell - size * 0.5f,
                origin.y + (height - 1 - y + 0.5f) * cell - size * 0.5f,
                size, size);

        private void DrawEventLog(MazeChaseGame game)
        {
            var events = game.Events;
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

        private void DrawOverlay(MazeChaseGame game)
        {
            if (!game.IsGameOver) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {game.Score}    到达第 {game.Level} 关    按 R 重新开始", StatusStyle());
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
