using System.Collections.Generic;
using System.Text;
using Lyking.BattleCity.Core;
using UnityEngine;

namespace Lyking.BattleCity.Unity
{
    /// <summary>
    /// 坦克大战演示面板：IMGUI 绘制网格战场（钢墙、砖墙、基地分色）、玩家与敌方坦克、
    /// 子弹、基地标记、状态栏、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产与物理引擎。
    /// </summary>
    public sealed class BattleCityDemoPanel : MonoBehaviour
    {
        private static readonly Color FieldBorder = new Color(0.30f, 0.36f, 0.52f);
        private static readonly Color FieldBackground = new Color(0.07f, 0.08f, 0.06f);
        private static readonly Color SteelColor = new Color(0.58f, 0.62f, 0.68f);
        private static readonly Color BrickColor = new Color(0.72f, 0.42f, 0.20f);
        private static readonly Color BaseColor = new Color(0.95f, 0.80f, 0.25f);
        private static readonly Color BaseMarkColor = new Color(0.35f, 0.22f, 0.05f);
        private static readonly Color PlayerColor = new Color(0.35f, 0.80f, 0.95f);
        private static readonly Color PlayerInvincibleColor = new Color(0.96f, 0.96f, 0.96f);
        private static readonly Color PlayerBulletColor = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color EnemyColor = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color EnemyBulletColor = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private BattleCityDemoBootstrap? _bootstrap;
        private Texture2D? _white;

        public void Bind(BattleCityDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            if (_white == null) _white = Texture2D.whiteTexture;

            var game = _bootstrap.Game;
            string lifecycle = game.IsGameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 760f, 26f),
                $"分数 {game.Score}    关卡 {game.Level}    剩余敌人 {game.EnemiesRemaining}    生命 {game.Lives}    状态 {lifecycle}",
                StatusStyle());

            DrawField(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private void DrawField(BattleCityGame game)
        {
            var map = game.Map;
            float scale = Mathf.Min((Screen.height - 130f) / map.Height, (Screen.width - 80f) / map.Width);
            float fieldWidth = scale * map.Width;
            float fieldHeight = scale * map.Height;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), FieldBackground);

            // 网格地图：钢墙、砖墙与基地分色绘制
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.CellAt(new GridPos(x, y));
                    if (cell == BattleCityCell.Empty) continue;
                    var rect = CellRect(origin, scale, map.Height, x, y);
                    Fill(rect, cell switch
                    {
                        BattleCityCell.Steel => SteelColor,
                        BattleCityCell.Brick => BrickColor,
                        _ => BaseColor,
                    });
                }
            }

            // 基地标记：金色格心叠加深色内块
            var baseRect = CellRect(origin, scale, map.Height, map.BaseCell.X, map.BaseCell.Y);
            Fill(new Rect(
                baseRect.x + baseRect.width * 0.3f,
                baseRect.y + baseRect.height * 0.3f,
                baseRect.width * 0.4f,
                baseRect.height * 0.4f), BaseMarkColor);

            foreach (var bullet in game.Bullets)
            {
                float half = game.Rules.BulletRadius;
                Fill(WorldRect(origin, scale, map.Height, bullet.Position, half, half),
                    bullet.FromPlayer ? PlayerBulletColor : EnemyBulletColor);
            }

            foreach (var enemy in game.Enemies)
            {
                Fill(WorldRect(origin, scale, map.Height, enemy.Position, game.Rules.TankHalfSize, game.Rules.TankHalfSize), EnemyColor);
            }

            // 无敌期的玩家以白色闪烁提示
            bool invincible = game.InvincibleRemaining > 0f;
            Fill(WorldRect(origin, scale, map.Height, game.Player.Position, game.Rules.TankHalfSize, game.Rules.TankHalfSize),
                invincible ? PlayerInvincibleColor : PlayerColor);
        }

        /// <summary>格子坐标（Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为场地左上角。</summary>
        private static Rect CellRect(Vector2 origin, float scale, int mapHeight, int x, int y)
            => new Rect(origin.x + x * scale, origin.y + (mapHeight - 1 - y) * scale, scale, scale);

        /// <summary>世界坐标（中心点、Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为场地左上角。</summary>
        private static Rect WorldRect(Vector2 origin, float scale, float mapHeight, Vec2 center, float halfWidth, float halfHeight)
        {
            float left = origin.x + (center.X - halfWidth) * scale;
            float top = origin.y + (mapHeight - center.Y - halfHeight) * scale;
            return new Rect(left, top, halfWidth * 2f * scale, halfHeight * 2f * scale);
        }

        private void DrawEventLog(BattleCityGame game)
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

        private void DrawOverlay(BattleCityGame game)
        {
            if (!game.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {game.Score}    关卡 {game.Level}    按 R 重新开始", StatusStyle());
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
