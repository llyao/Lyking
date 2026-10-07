using System.Collections.Generic;
using System.Text;
using Lyking.Asteroids.Core;
using UnityEngine;

namespace Lyking.Asteroids.Unity
{
    /// <summary>
    /// 小行星演示面板：IMGUI 绘制星空场地、飞船、子弹、三档小行星、状态栏、结束提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形（飞船与小行星按半径画方块近似），不依赖任何场景资产与物理引擎。
    /// 重生无敌期飞船按固定节奏闪烁，仅作表现、不影响核心判定。
    /// </summary>
    public sealed class AsteroidsDemoPanel : MonoBehaviour
    {
        private static readonly Color SpaceBackground = new Color(0.03f, 0.04f, 0.09f);
        private static readonly Color FieldBorder = new Color(0.30f, 0.36f, 0.52f);
        private static readonly Color StarColor = new Color(0.72f, 0.76f, 0.88f);
        private static readonly Color ShipColor = new Color(0.35f, 0.80f, 0.95f);
        private static readonly Color BulletColor = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color LargeAsteroidColor = new Color(0.55f, 0.42f, 0.32f);
        private static readonly Color MediumAsteroidColor = new Color(0.68f, 0.55f, 0.40f);
        private static readonly Color SmallAsteroidColor = new Color(0.82f, 0.72f, 0.55f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        private const int StarCount = 90;
        private const float InvulnerableBlinkHz = 6f;

        private AsteroidsDemoBootstrap? _bootstrap;
        private Texture2D? _white;
        private readonly List<Vector2> _stars = new List<Vector2>();

        public void Bind(AsteroidsDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            if (_bootstrap == null || !_bootstrap.HasGame) return;
            if (_white == null) _white = Texture2D.whiteTexture;
            if (_stars.Count == 0) GenerateStars();

            var game = _bootstrap.Game;
            string lifecycle = game.IsGameOver ? "结束" : game.IsInvulnerable ? $"无敌 {game.InvulnerableRemaining:0.0}s" : "进行中";
            GUI.Label(new Rect(12f, 8f, 720f, 26f),
                $"分数 {game.Score}    生命 {game.Lives}    波次 {game.Wave}    状态 {lifecycle}",
                StatusStyle());

            DrawField(game);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        /// <summary>星空仅为装饰：以固定种子的 Random 生成归一化坐标，每次运行完全一致。</summary>
        private void GenerateStars()
        {
            var random = new System.Random(20261006);
            for (int i = 0; i < StarCount; i++)
            {
                _stars.Add(new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
            }
        }

        private void DrawField(AsteroidsGame game)
        {
            float scale = Mathf.Min((Screen.height - 130f) / game.Height, (Screen.width - 80f) / game.Width);
            float fieldWidth = scale * game.Width;
            float fieldHeight = scale * game.Height;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), SpaceBackground);

            foreach (var star in _stars)
            {
                Fill(new Rect(origin.x + star.x * fieldWidth, origin.y + star.y * fieldHeight, 2f, 2f), StarColor);
            }

            foreach (var asteroid in game.Asteroids)
            {
                float half = game.Rules.AsteroidRadiusFor(asteroid.Size);
                Fill(WorldRect(origin, scale, game.Height, asteroid.Position, half), AsteroidColor(asteroid.Size));
            }

            foreach (var bullet in game.Bullets)
            {
                Fill(WorldRect(origin, scale, game.Height, bullet.Position, game.Rules.BulletRadius), BulletColor);
            }

            // 无敌期飞船闪烁：按固定频率跳过绘制，仅表现、不影响判定。
            bool blinkOff = game.IsInvulnerable && (Time.realtimeSinceStartup * InvulnerableBlinkHz) % 2f >= 1f;
            if (!blinkOff)
            {
                Fill(WorldRect(origin, scale, game.Height, game.Ship.Position, game.Rules.ShipRadius), ShipColor);
            }
        }

        private static Color AsteroidColor(AsteroidSize size) => size switch
        {
            AsteroidSize.Large => LargeAsteroidColor,
            AsteroidSize.Medium => MediumAsteroidColor,
            _ => SmallAsteroidColor,
        };

        /// <summary>世界坐标（中心点、Y 向上）转 IMGUI 矩形（Y 向下），<paramref name="origin"/> 为场地左上角。</summary>
        private static Rect WorldRect(Vector2 origin, float scale, float height, Vec2 center, float halfSize)
        {
            float left = origin.x + (center.X - halfSize) * scale;
            float top = origin.y + (height - center.Y - halfSize) * scale;
            float side = halfSize * 2f * scale;
            return new Rect(left, top, side, side);
        }

        private void DrawEventLog(AsteroidsGame game)
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

        private void DrawOverlay(AsteroidsGame game)
        {
            if (!game.IsFinished) return;
            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "游戏结束", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {game.Score}    波次 {game.Wave}    按 R 重新开始", StatusStyle());
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
