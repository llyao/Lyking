using Lyking.Simon.Core;
using UnityEngine;

namespace Lyking.Simon.Unity
{
    /// <summary>
    /// 西蒙记忆演示面板：IMGUI 绘制状态栏、2x2 四色大块、失败提示与事件日志。
    /// 全部使用 <see cref="Texture2D.whiteTexture"/> 与纯色矩形，不依赖任何场景资产；
    /// 复述阶段的鼠标点击请求输入并闪白反馈，序列播放与判定节奏由核心完成。
    /// </summary>
    public sealed class SimonDemoPanel : MonoBehaviour
    {
        private const float FlashSeconds = 0.15f;

        private static readonly Color BoardFrame = new Color(0.45f, 0.42f, 0.58f);
        private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);

        // 四色大块的暗态配色，索引与核心的色块编号一一对应：0 红、1 绿、2 蓝、3 黄。
        private static readonly Color[] PadDarkColors =
        {
            new Color(0.36f, 0.14f, 0.13f),
            new Color(0.14f, 0.30f, 0.15f),
            new Color(0.13f, 0.21f, 0.37f),
            new Color(0.38f, 0.32f, 0.12f),
        };

        // 播放阶段点亮时的亮态配色。
        private static readonly Color[] PadLitColors =
        {
            new Color(0.92f, 0.30f, 0.26f),
            new Color(0.36f, 0.82f, 0.38f),
            new Color(0.30f, 0.56f, 0.95f),
            new Color(0.98f, 0.84f, 0.30f),
        };

        private SimonDemoBootstrap? _bootstrap;

        // 复述点击后的闪白反馈：色块编号与剩余秒数。
        private int _flashPad = -1;
        private float _flashRemaining;

        public void Bind(SimonDemoBootstrap bootstrap) => _bootstrap = bootstrap;

        private void Update()
        {
            if (_flashRemaining <= 0f) return;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0f) _flashPad = -1;
        }

        private void OnGUI()
        {
            var bootstrap = _bootstrap;
            if (bootstrap == null || !bootstrap.HasGame) return;

            var game = bootstrap.Game;
            GUI.Label(new Rect(12f, 8f, 640f, 26f),
                $"等级 {game.Level}    最长纪录 {game.BestLevel}    状态 {Lifecycle(game)}{ProgressText(game)}",
                StatusStyle());

            DrawPads(game, bootstrap);
            DrawEventLog(game);
            DrawOverlay(game);
        }

        private static string Lifecycle(SimonGame game)
        {
            switch (game.Phase)
            {
                case SimonPhase.Showing: return "播放";
                case SimonPhase.Input: return "复述";
                case SimonPhase.GameOver: return "结束";
                default: return "等待开始";
            }
        }

        private static string ProgressText(SimonGame game) =>
            game.Phase == SimonPhase.Input ? $"    进度 {game.InputProgress}/{game.Level}" : string.Empty;

        private void DrawPads(SimonGame game, SimonDemoBootstrap bootstrap)
        {
            int side = SimonRules.PadCount / 2;
            float cell = Mathf.Min((Screen.height - 160f) / side, (Screen.width - 80f) / side);
            float boardWidth = cell * side;
            var origin = new Vector2((Screen.width - boardWidth) * 0.5f, (Screen.height - boardWidth) * 0.5f + 10f);

            Fill(new Rect(origin.x - 4f, origin.y - 4f, boardWidth + 8f, boardWidth + 8f), BoardFrame);

            HandleClick(game, bootstrap, origin, cell);

            for (int index = 0; index < SimonRules.PadCount; index++)
            {
                Fill(PadRect(origin, cell, index), PadColor(game, index));
            }
        }

        /// <summary>色块颜色：播放阶段按核心点亮状态取亮色，复述闪白反馈按剩余时间渐变，其余为暗色。</summary>
        private Color PadColor(SimonGame game, int index)
        {
            if (_flashPad == index)
            {
                return Color.Lerp(PadDarkColors[index], Color.white, Mathf.Clamp01(_flashRemaining / FlashSeconds));
            }

            if (game.Phase == SimonPhase.Showing && game.LitPadIndex == index) return PadLitColors[index];
            return PadDarkColors[index];
        }

        /// <summary>把鼠标按下沿换算为色块编号并请求输入；点击落在色块区外时忽略。</summary>
        private void HandleClick(SimonGame game, SimonDemoBootstrap bootstrap, Vector2 origin, float cell)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown) return;

            float localX = current.mousePosition.x - origin.x;
            float localY = current.mousePosition.y - origin.y;
            int side = SimonRules.PadCount / 2;
            int column = Mathf.FloorToInt(localX / cell);
            int row = Mathf.FloorToInt(localY / cell);
            if (column < 0 || column >= side || row < 0 || row >= side) return;

            int padIndex = row * side + column;
            bool wasInput = game.Phase == SimonPhase.Input;
            bootstrap.Press(padIndex);
            if (wasInput)
            {
                _flashPad = padIndex;
                _flashRemaining = FlashSeconds;
            }

            current.Use();
        }

        /// <summary>色块编号（0 号在左上）转 IMGUI 矩形，<paramref name="origin"/> 为色块区左上角。</summary>
        private static Rect PadRect(Vector2 origin, float cell, int index)
        {
            int column = index % (SimonRules.PadCount / 2);
            int row = index / (SimonRules.PadCount / 2);
            return new Rect(origin.x + column * cell, origin.y + row * cell, cell - 1f, cell - 1f);
        }

        private void DrawEventLog(SimonGame game)
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

        private static void DrawOverlay(SimonGame game)
        {
            if (game.Phase == SimonPhase.Ready)
            {
                GUI.Label(new Rect(0f, Screen.height - 70f, Screen.width, 28f),
                    "点击任意色块，或按数字 1-4 / 方向键开始", StatusStyle());
                return;
            }

            if (game.Phase == SimonPhase.GameOver)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 44f), "复述失败，游戏结束", TitleStyle());
                GUI.Label(new Rect(0f, Screen.height * 0.40f + 48f, Screen.width, 28f),
                    $"完整复述 {game.BestLevel} 轮    按 R 重新开始", StatusStyle());
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
