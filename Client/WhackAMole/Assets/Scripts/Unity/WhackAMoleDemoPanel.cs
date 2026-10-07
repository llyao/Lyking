using Lyking.WhackAMole.Core;
using UnityEngine;

namespace Lyking.WhackAMole.Unity
{
    /// <summary>
    /// 打地鼠演示面板：IMGUI 绘制状态栏、3x3 地洞网格、冒头地鼠、命中闪白、开始与结束提示及事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 鼠标点击与数字键 1-9 请求打击，调度与计分由核心完成。
    /// </summary>
    public sealed class WhackAMoleDemoPanel : MonoBehaviour
    {
        private static readonly Color FieldBackground = new Color(0.22f, 0.28f, 0.17f);
        private static readonly Color FieldBorder = new Color(0.45f, 0.55f, 0.32f);
        private static readonly Color HoleFrame = new Color(0.36f, 0.27f, 0.19f);
        private static readonly Color HoleEmpty = new Color(0.12f, 0.09f, 0.07f);
        private static readonly Color MoleNormal = new Color(0.96f, 0.76f, 0.28f);
        private static readonly Color MoleBomb = new Color(0.16f, 0.14f, 0.19f);
        private static readonly Color BombSpark = new Color(0.90f, 0.32f, 0.26f);
        private static readonly Color HitFlash = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color BombFlash = new Color(0.95f, 0.35f, 0.30f, 0.85f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.90f);

        /// <summary>命中闪白的持续时长（秒），由面板自行表现，不影响核心判定。</summary>
        private const float HitFlashSeconds = 0.18f;

        private WhackAMoleDemoBootstrap? _bootstrap;
        private int _seenEvents;
        private int _flashHole = -1;
        private Color _flashColor = HitFlash;
        private float _flashStartTime = -100f;

        public void Bind(WhackAMoleDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasGame) return;

            var game = bootstrap.Game;
            TrackHitFlash(game);

            string lifecycle = game.Phase == WhackAMolePhase.Ready ? "未开始"
                : game.Phase == WhackAMolePhase.GameOver ? "结束" : "进行中";
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"分数 {game.Score}    剩余 {game.TimeRemaining:0.0} 秒    连击 {game.Combo}    状态 {lifecycle}",
                StatusStyle());

            DrawField(game, bootstrap);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        /// <summary>消费核心新增事件：命中普通地鼠闪白、命中炸弹闪红，鼠标与键盘打击同样生效。</summary>
        private void TrackHitFlash(WhackAMoleGame game)
        {
            var events = game.Events;
            if (events.Count < _seenEvents) _seenEvents = 0;

            for (int i = _seenEvents; i < events.Count; i++)
            {
                var entry = events[i];
                if (entry.Type != WhackAMoleEventType.Hit && entry.Type != WhackAMoleEventType.BombHit) continue;

                _flashHole = entry.HoleIndex;
                _flashColor = entry.Type == WhackAMoleEventType.Hit ? HitFlash : BombFlash;
                _flashStartTime = Time.unscaledTime;
            }

            _seenEvents = events.Count;
        }

        private void DrawField(WhackAMoleGame game, WhackAMoleDemoBootstrap bootstrap)
        {
            float cell = Mathf.Min(
                (Screen.height - 140f) / WhackAMoleRules.HoleRows,
                (Screen.width - 80f) / WhackAMoleRules.HoleColumns);
            float fieldWidth = cell * WhackAMoleRules.HoleColumns;
            float fieldHeight = cell * WhackAMoleRules.HoleRows;
            var origin = new Vector2((Screen.width - fieldWidth) * 0.5f, (Screen.height - fieldHeight) * 0.5f + 14f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, fieldWidth + 8f, fieldHeight + 8f), FieldBorder);
            Fill(new Rect(origin.x, origin.y, fieldWidth, fieldHeight), FieldBackground);

            HandleClick(bootstrap, origin, cell);

            var numberStyle = NumberStyle(cell);
            bool flashActive = Time.unscaledTime - _flashStartTime < HitFlashSeconds;
            for (int hole = 0; hole < WhackAMoleRules.HoleCount; hole++)
            {
                var rect = HoleRect(origin, cell, hole);
                Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), HoleFrame);
                Fill(rect, HoleEmpty);

                if (game.HasMole && game.MoleHole == hole)
                {
                    // 冒头地鼠：洞内收缩一块，普通为金色，炸弹为深色并带红色引信。
                    var moleRect = Inset(rect, 0.16f);
                    Fill(moleRect, game.MoleIsBomb ? MoleBomb : MoleNormal);
                    if (game.MoleIsBomb) Fill(Inset(moleRect, 0.30f), BombSpark);
                }

                GUI.Label(rect, (hole + 1).ToString(), numberStyle);

                if (flashActive && _flashHole == hole) Fill(rect, _flashColor);
            }
        }

        /// <summary>把鼠标按下沿换算为地洞并请求打击；点击落在场地外时忽略。</summary>
        private void HandleClick(WhackAMoleDemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= WhackAMoleRules.HoleColumns || row < 0 || row >= WhackAMoleRules.HoleRows) return;

            bootstrap.Whack(row * WhackAMoleRules.HoleColumns + column);
            current.Use();
        }

        /// <summary>地洞编号（行优先、0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为场地左上角。</summary>
        private static Rect HoleRect(Vector2 origin, float cell, int hole)
        {
            int column = hole % WhackAMoleRules.HoleColumns;
            int row = hole / WhackAMoleRules.HoleColumns;
            float margin = cell * 0.08f;
            return new Rect(origin.x + column * cell + margin, origin.y + row * cell + margin,
                cell - 2f * margin - 1f, cell - 2f * margin - 1f);
        }

        /// <summary>按比例收缩矩形，用于洞内地鼠与炸弹引信的内嵌色块。</summary>
        private static Rect Inset(Rect rect, float fraction)
        {
            float dx = rect.width * fraction;
            float dy = rect.height * fraction;
            return new Rect(rect.x + dx, rect.y + dy, rect.width - 2f * dx, rect.height - 2f * dy);
        }

        private void DrawEventLog(WhackAMoleGame game)
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

        private void DrawOverlay(WhackAMoleGame game)
        {
            if (game.Phase == WhackAMolePhase.Ready)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 36f),
                    "点击地洞或按数字键 1-9 开始", HintStyle());
                return;
            }

            if (!game.IsFinished) return;

            GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "时间到，结束！", TitleStyle());
            GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                $"最终分数 {game.Score}    按 R 重新开始", StatusStyle());
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

        private static GUIStyle HintStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = TextColor;
            return style;
        }

        private static GUIStyle LogStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
            return style;
        }

        private static GUIStyle NumberStyle(float cell)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp((int)(cell * 0.22f), 11, 32),
                alignment = TextAnchor.UpperLeft,
            };
            style.normal.textColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.55f);
            return style;
        }
    }
}
