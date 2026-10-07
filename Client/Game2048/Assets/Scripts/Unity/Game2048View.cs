#nullable enable
using System;
using Lyking.Game2048.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Lyking.Game2048.Unity
{
    /// <summary>2048 的 uGUI 视图：代码构建棋盘界面并按对局状态刷新，不处理输入与规则。</summary>
    public sealed class Game2048View : MonoBehaviour
    {
        private const float BoardSize = 480f;
        private const float CellSize = 105f;
        private const float CellGap = 12f;

        private static readonly Color BackgroundColor = FromRgb(0xFA, 0xF8, 0xEF);
        private static readonly Color BoardColor = FromRgb(0xBB, 0xAD, 0xA0);
        private static readonly Color EmptyCellColor = FromRgb(0xCD, 0xC1, 0xB4);
        private static readonly Color TextDarkColor = FromRgb(0x77, 0x6E, 0x65);
        private static readonly Color TextLightColor = FromRgb(0xF9, 0xF6, 0xF2);
        private static readonly Color ButtonColor = FromRgb(0x8F, 0x7A, 0x66);

        private readonly Image[,] tileImages = new Image[GameBoard.DefaultSize, GameBoard.DefaultSize];
        private readonly Text[,] tileTexts = new Text[GameBoard.DefaultSize, GameBoard.DefaultSize];
        private Text scoreText = null!;
        private Text statusText = null!;
        private Font font = null!;

        public event Action? RestartClicked;

        public void Build(int rows, int columns)
        {
            font = LoadBuiltinFont();
            Canvas canvas = CreateCanvas();

            CreateImage(canvas.transform, "Background", stretch: true, color: BackgroundColor);
            CreateText(canvas.transform, "Title", "2048", 52, TextDarkColor, new Vector2(-280, 440), TextAnchor.MiddleLeft, width: 220);
            scoreText = CreateText(canvas.transform, "Score", string.Empty, 32, TextDarkColor, new Vector2(0, 310), TextAnchor.MiddleCenter, width: 400, height: 50);
            statusText = CreateText(canvas.transform, "Status", string.Empty, 24, TextDarkColor, new Vector2(0, 370), TextAnchor.MiddleCenter, width: 760, height: 44);
            CreateRestartButton(canvas.transform, new Vector2(280, 440));

            RectTransform boardRect = CreateImage(canvas.transform, "Board", stretch: false, color: BoardColor, size: new Vector2(BoardSize, BoardSize), position: new Vector2(0, -50));
            for (int row = 0; row < rows && row < tileImages.GetLength(0); row++)
            {
                for (int column = 0; column < columns && column < tileImages.GetLength(1); column++)
                {
                    CreateTile(boardRect, row, column);
                }
            }
        }

        public void Render(Game2048Game game)
        {
            if (game is null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            scoreText.text = $"分数：{game.Score}";
            statusText.text = GetStatusMessage(game);
            for (int row = 0; row < game.Board.Rows && row < tileImages.GetLength(0); row++)
            {
                for (int column = 0; column < game.Board.Columns && column < tileImages.GetLength(1); column++)
                {
                    int value = game.Board[row, column];
                    tileImages[row, column].color = value == 0 ? EmptyCellColor : GetTileColor(value);
                    tileTexts[row, column].text = value == 0 ? string.Empty : value.ToString();
                    tileTexts[row, column].fontSize = GetTileFontSize(value);
                    tileTexts[row, column].color = GetTileTextColor(value);
                }
            }
        }

        /// <summary>经典 2048 配色：底色随数值从米色过渡到橙黄，超过 2048 转深灰。</summary>
        public static Color GetTileColor(int value)
        {
            return value switch
            {
                0 => EmptyCellColor,
                2 => FromRgb(0xEE, 0xE4, 0xDA),
                4 => FromRgb(0xED, 0xE0, 0xC8),
                8 => FromRgb(0xF2, 0xB1, 0x79),
                16 => FromRgb(0xF5, 0x95, 0x63),
                32 => FromRgb(0xF6, 0x7C, 0x5F),
                64 => FromRgb(0xF6, 0x5E, 0x3B),
                128 => FromRgb(0xED, 0xCF, 0x72),
                256 => FromRgb(0xED, 0xCC, 0x61),
                512 => FromRgb(0xED, 0xC8, 0x50),
                1024 => FromRgb(0xED, 0xC5, 0x3F),
                2048 => FromRgb(0xED, 0xC2, 0x2E),
                _ => FromRgb(0x3C, 0x3A, 0x32),
            };
        }

        public static Color GetTileTextColor(int value)
        {
            return value >= 8 ? TextLightColor : TextDarkColor;
        }

        public static string GetStatusMessage(Game2048Game game)
        {
            if (game.Status == GameStatus.Over)
            {
                return $"游戏结束，最终分数 {game.Score}。按 R 重新开始。";
            }

            if (game.HasWon)
            {
                return "合成 2048，胜利！可以继续挑战更高分。";
            }

            return "方向键或 WASD 滑动，R 键重新开始。";
        }

        private void NotifyRestartClicked() => RestartClicked?.Invoke();

        private void CreateTile(RectTransform board, int row, int column)
        {
            float half = BoardSize / 2f;
            float x = -half + CellGap + (CellSize / 2f) + (column * (CellSize + CellGap));
            float y = half - CellGap - (CellSize / 2f) - (row * (CellSize + CellGap));
            Image image = CreateImage(board, $"Tile_{row}_{column}", stretch: false, color: EmptyCellColor, size: new Vector2(CellSize, CellSize), position: new Vector2(x, y));
            Text text = CreateText(image.transform, "Value", string.Empty, 40, TextDarkColor, Vector2.zero, TextAnchor.MiddleCenter, width: CellSize, height: CellSize);
            tileImages[row, column] = image;
            tileTexts[row, column] = text;
        }

        private void CreateRestartButton(Canvas canvas, Vector2 position)
        {
            Image buttonImage = CreateImage(canvas.transform, "RestartButton", stretch: false, color: ButtonColor, size: new Vector2(160, 48), position: position);
            buttonImage.gameObject.AddComponent<Button>();
            CreateText(buttonImage.transform, "Label", "重新开始", 22, TextLightColor, Vector2.zero, TextAnchor.MiddleCenter, width: 160, height: 48);
            buttonImage.gameObject.GetComponent<Button>().onClick.AddListener(NotifyRestartClicked);
        }

        private Canvas CreateCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 1000);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static Image CreateImage(Transform parent, string name, bool stretch, Color color, Vector2? size = null, Vector2? position = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size ?? new Vector2(100, 100);
                rect.anchoredPosition = position ?? Vector2.zero;
            }

            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 position, TextAnchor alignment, float width = 300, float height = 60)
        {
            Text text = CreateImage(parent, name, stretch: false, color: Color.clear, size: new Vector2(width, height), position: position).gameObject.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font LoadBuiltinFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private static Color FromRgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private static int GetTileFontSize(int value)
        {
            return value.ToString().Length switch
            {
                <= 2 => 40,
                3 => 32,
                4 => 26,
                _ => 20,
            };
        }
    }
}
