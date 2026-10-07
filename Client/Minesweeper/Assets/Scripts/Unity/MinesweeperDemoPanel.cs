#nullable enable
using System.Collections.Generic;
using Lyking.Minesweeper.Core;
using UnityEngine;

namespace Lyking.Minesweeper.Unity
{
    /// <summary>
    /// IMGUI 演示面板：左侧状态区、难度与重开按钮、事件日志，右侧可点击棋盘网格。
    /// 左键翻开，右键插旗；网格布局常量保存在实例字段，供坐标命中测试复用。
    /// </summary>
    public sealed class MinesweeperDemoPanel : MonoBehaviour
    {
        private const float GridLeft = 330f;
        private const float GridTop = 16f;
        private const float CellGap = 3f;
        private const float MaxCellSize = 34f;
        private const float MinCellSize = 14f;

        private static readonly Color NumberOne = new(0.16f, 0.35f, 0.9f);
        private static readonly Color NumberTwo = new(0.1f, 0.55f, 0.2f);
        private static readonly Color NumberThree = new(0.85f, 0.15f, 0.15f);
        private static readonly Color NumberFour = new(0.1f, 0.15f, 0.65f);
        private static readonly Color NumberFive = new(0.6f, 0.1f, 0.1f);
        private static readonly Color NumberSix = new(0.1f, 0.5f, 0.5f);
        private static readonly Color FlagColor = new(0.9f, 0.6f, 0.1f);
        private static readonly Color MineColor = new(0.15f, 0.15f, 0.15f);
        private static readonly Color WrongFlagColor = new(0.9f, 0.2f, 0.2f);

        private readonly Queue<string> _events = new Queue<string>();
        private MinesweeperDemoBootstrap? _bootstrap;
        private string _message = string.Empty;
        private Vector2 _gridOrigin = new(GridLeft, GridTop);
        private float _cellSize = MaxCellSize;

        public void Bind(MinesweeperDemoBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
        }

        public void SetMessage(string message)
        {
            _message = message;
        }

        public void AppendEvent(MinesweeperEvent gameplayEvent)
        {
            _events.Enqueue(Describe(gameplayEvent));
            while (_events.Count > 8)
            {
                _events.Dequeue();
            }
        }

        public void ClearLog()
        {
            _events.Clear();
        }

        /// <summary>把 GUI 坐标映射为格子坐标；点在间隙上或越界时视为未命中。</summary>
        public bool TryGetCellIndex(Vector2 guiPosition, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (_bootstrap is null || !_bootstrap.HasBoard)
            {
                return false;
            }

            MinesweeperBoard board = _bootstrap.Board;
            int cellX = Mathf.FloorToInt((guiPosition.x - _gridOrigin.x) / (_cellSize + CellGap));
            int cellY = Mathf.FloorToInt((guiPosition.y - _gridOrigin.y) / (_cellSize + CellGap));
            if (cellX < 0 || cellY < 0 || cellX >= board.Width || cellY >= board.Height)
            {
                return false;
            }

            float offsetX = guiPosition.x - _gridOrigin.x - cellX * (_cellSize + CellGap);
            float offsetY = guiPosition.y - _gridOrigin.y - cellY * (_cellSize + CellGap);
            if (offsetX > _cellSize || offsetY > _cellSize)
            {
                return false;
            }

            x = cellX;
            y = cellY;
            return true;
        }

        private static string Describe(MinesweeperEvent gameplayEvent)
        {
            return gameplayEvent.Type switch
            {
                MinesweeperEventType.Revealed => $"翻开 ({gameplayEvent.X},{gameplayEvent.Y})",
                MinesweeperEventType.FloodRevealed => $"洪水展开 ({gameplayEvent.X},{gameplayEvent.Y}) 共 {gameplayEvent.Count} 格",
                MinesweeperEventType.Flagged => $"插旗 ({gameplayEvent.X},{gameplayEvent.Y})",
                MinesweeperEventType.Unflagged => $"拔旗 ({gameplayEvent.X},{gameplayEvent.Y})",
                MinesweeperEventType.MineHit => $"踩雷 ({gameplayEvent.X},{gameplayEvent.Y})",
                MinesweeperEventType.Won => "胜利",
                MinesweeperEventType.Lost => "失败",
                MinesweeperEventType.NewGame => "新对局",
                _ => gameplayEvent.Type.ToString(),
            };
        }

        private static string StateText(GameState state)
        {
            return state switch
            {
                GameState.Ready => "待首击",
                GameState.Playing => "进行中",
                GameState.Won => "胜利",
                GameState.Lost => "失败",
                _ => state.ToString(),
            };
        }

        private static string DifficultyText(MinesweeperDemoBootstrap.Difficulty difficulty)
        {
            return difficulty switch
            {
                MinesweeperDemoBootstrap.Difficulty.Intermediate => "中级",
                MinesweeperDemoBootstrap.Difficulty.Expert => "高级",
                _ => "初级",
            };
        }

        private static Color NumberColor(int adjacentMines)
        {
            return adjacentMines switch
            {
                1 => NumberOne,
                2 => NumberTwo,
                3 => NumberThree,
                4 => NumberFour,
                5 => NumberFive,
                6 => NumberSix,
                7 => Color.black,
                8 => new Color(0.45f, 0.45f, 0.45f),
                _ => Color.black,
            };
        }

        private void OnGUI()
        {
            if (_bootstrap is null || !_bootstrap.HasBoard)
            {
                return;
            }

            MinesweeperBoard board = _bootstrap.Board;
            DrawStatusArea(board);
            DrawGrid(board);
        }

        private void DrawStatusArea(MinesweeperBoard board)
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 300f, Screen.height - 24f), GUI.skin.box);
            GUILayout.Label("Lyking 扫雷");
            GUILayout.Label($"难度：{DifficultyText(_bootstrap!.CurrentDifficulty)}    剩余雷数：{board.RemainingMineCount}");
            GUILayout.Label($"用时：{_bootstrap.ElapsedSeconds:0.0} 秒    状态：{StateText(board.State)}");

            if (GUILayout.Button("重开（R）"))
            {
                _bootstrap.Restart();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("初级（1）"))
            {
                _bootstrap.SelectDifficulty(MinesweeperDemoBootstrap.Difficulty.Beginner);
            }

            if (GUILayout.Button("中级（2）"))
            {
                _bootstrap.SelectDifficulty(MinesweeperDemoBootstrap.Difficulty.Intermediate);
            }

            if (GUILayout.Button("高级（3）"))
            {
                _bootstrap.SelectDifficulty(MinesweeperDemoBootstrap.Difficulty.Expert);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label(_message);
            GUILayout.Label("事件：");
            foreach (string item in _events)
            {
                GUILayout.Label("- " + item);
            }

            GUILayout.EndArea();
        }

        private void DrawGrid(MinesweeperBoard board)
        {
            // 依据宽度自适应格子尺寸，保证高级 30 列也能在常见 Game 视图宽度内显示。
            float available = Screen.width - GridLeft - 16f;
            _cellSize = Mathf.Clamp((available - (board.Width - 1) * CellGap) / board.Width, MinCellSize, MaxCellSize);
            _gridOrigin = new Vector2(GridLeft, GridTop);

            Event guiEvent = Event.current;
            if (guiEvent.type == EventType.MouseDown
                && guiEvent.button == 1
                && TryGetCellIndex(guiEvent.mousePosition, out int flagX, out int flagY))
            {
                _bootstrap!.ToggleFlagCell(flagX, flagY);
                guiEvent.Use();
            }

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    DrawCell(board, x, y);
                }
            }
        }

        private void DrawCell(MinesweeperBoard board, int x, int y)
        {
            var cellRect = new Rect(
                _gridOrigin.x + x * (_cellSize + CellGap),
                _gridOrigin.y + y * (_cellSize + CellGap),
                _cellSize,
                _cellSize);
            MinesweeperCell cell = board.CellAt(x, y);

            string label;
            Color content = Color.white;
            bool revealed = cell.State == CellState.Revealed;
            if (revealed && cell.HasMine)
            {
                label = "*";
                content = MineColor;
            }
            else if (board.IsWrongFlag(x, y))
            {
                label = "X";
                content = WrongFlagColor;
            }
            else if (cell.State == CellState.Flagged)
            {
                label = "F";
                content = FlagColor;
            }
            else if (revealed)
            {
                label = cell.AdjacentMines > 0 ? cell.AdjacentMines.ToString() : string.Empty;
                content = NumberColor(cell.AdjacentMines);
            }
            else
            {
                label = string.Empty;
            }

            Color previous = GUI.contentColor;
            GUI.contentColor = content;
            if (revealed)
            {
                GUI.Label(cellRect, label, GUI.skin.box);
            }
            else if (GUI.Button(cellRect, label))
            {
                _bootstrap!.RevealCell(x, y);
            }

            GUI.contentColor = previous;
        }
    }
}
