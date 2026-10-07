using Lyking.Bomberman.Core;
using UnityEngine;

namespace Lyking.Bomberman.Unity
{
    /// <summary>
    /// 炸弹人演示面板：IMGUI 绘制状态栏、网格地形、出口门、火焰、炸弹（含引信倒计时）、
    /// 敌人、玩家、出口与结束提示以及事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖物理引擎与场景资产。
    /// </summary>
    public sealed class BombermanDemoPanel : MonoBehaviour
    {
        private static readonly Color FloorColor = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color BoardBorder = new Color(0.45f, 0.40f, 0.30f);
        private static readonly Color HardWallColor = new Color(0.36f, 0.38f, 0.44f);
        private static readonly Color SoftWallColor = new Color(0.62f, 0.42f, 0.26f);
        private static readonly Color BombColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color FlameColor = new Color(0.98f, 0.55f, 0.18f);
        private static readonly Color EnemyColor = new Color(0.88f, 0.30f, 0.32f);
        private static readonly Color ExitColor = new Color(0.95f, 0.78f, 0.30f);
        private static readonly Color PlayerColor = new Color(0.30f, 0.62f, 0.95f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.90f);

        private BombermanDemoBootstrap? _bootstrap;

        public void Bind(BombermanDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;

            var game = _bootstrap.Game;
            var lifecycle = game.IsGameOver ? "失败"
                : game.IsExitOpen ? "出口已打开"
                : game.IsExitRevealed ? "出口已显露" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"关卡 {game.Level}    分数 {game.Score}    剩余敌人 {game.Enemies.Count}    生命 {game.Lives}    状态 {lifecycle}",
                StatusStyle());

            DrawBoard(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawBoard(BombermanGame game)
        {
            float cell = Mathf.Min((Screen.height - 160f) / game.Height, (Screen.width - 80f) / game.Width);
            float boardWidth = cell * game.Width;
            float boardHeight = cell * game.Height;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardHeight + 8f), BoardBorder);

            // 地形：硬墙、软墙、地板分色铺满网格。
            for (int y = game.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < game.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    var tile = game.TileAt(pos);
                    Fill(CellRect(origin, cell, game.Height, pos),
                        tile == BombermanTile.HardWall ? HardWallColor :
                        tile == BombermanTile.SoftWall ? SoftWallColor : FloorColor);
                }
            }

            // 出口门：藏在软墙之下，软墙被摧毁后才显露。
            if (game.IsExitRevealed) Fill(Inset(CellRect(origin, cell, game.Height, game.Exit), cell, 0.25f), ExitColor);

            // 火焰格。
            foreach (var flame in game.Flames) Fill(CellRect(origin, cell, game.Height, flame.Position), FlameColor);

            // 炸弹：深色块叠加引信倒计时。
            foreach (var bomb in game.Bombs)
            {
                var rect = CellRect(origin, cell, game.Height, bomb.Position);
                Fill(Inset(rect, cell, 0.12f), BombColor);
                var label = new Rect(rect.x, rect.y + rect.height * 0.5f - 9f, rect.width, 18f);
                GUI.Label(label, bomb.FuseRemaining.ToString("0.0"), FuseStyle());
            }

            // 敌人与玩家：实体略缩进叠在地形之上。
            foreach (var enemy in game.Enemies) Fill(Inset(CellRect(origin, cell, game.Height, enemy.Position), cell, 0.18f), EnemyColor);
            Fill(Inset(CellRect(origin, cell, game.Height, game.Player), cell, 0.18f), PlayerColor);
        }

        /// <summary>游戏坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为棋盘左上角。</summary>
        private static Rect CellRect(Vector2 origin, float cell, int height, GridPos pos)
        {
            float left = origin.x + pos.X * cell;
            float top = origin.y + (height - 1 - pos.Y) * cell;
            return new Rect(left, top, cell - 1f, cell - 1f);
        }

        /// <summary>格子矩形向内收缩指定比例，用于出口门与实体的视觉分层。</summary>
        private static Rect Inset(Rect rect, float cell, float ratio)
        {
            float inset = cell * ratio;
            return new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);
        }

        private void DrawEventLog(BombermanGame game)
        {
            var events = game.Events;
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

        private void DrawOverlay(BombermanGame game)
        {
            if (game.IsGameOver)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "生命耗尽，游戏结束", TitleStyle());
                GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                    $"最终分数 {game.Score}    按 R 重新开始", StatusStyle());
            }
            else if (game.IsExitOpen)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 28f),
                    "出口已打开，走上出口进入下一关！", StatusStyle());
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

        private static GUIStyle FuseStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
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
