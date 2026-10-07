#nullable enable
using System;
using Lyking.Game2048.Core;
using UnityEngine;

namespace Lyking.Game2048.Unity
{
    /// <summary>2048 对局启动器：装配核心对局与视图，读取键盘输入并驱动移动。</summary>
    public sealed class Game2048Bootstrap : MonoBehaviour
    {
        [SerializeField] private int boardSize = GameBoard.DefaultSize;

        private Game2048Game game = null!;
        private Game2048View? view;

        public Game2048Game Game => game;

        public Game2048View? View => view;

        private void Start()
        {
            if (boardSize < 2)
            {
                boardSize = GameBoard.DefaultSize;
            }

            game = new Game2048Game(boardSize);
            view = GetComponent<Game2048View>() ?? gameObject.AddComponent<Game2048View>();
            view.RestartClicked += Restart;
            view.Build(boardSize, boardSize);
            view.Render(game);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Restart();
                return;
            }

            MoveDirection? direction = ReadPressedDirection();
            if (direction.HasValue)
            {
                ProcessInput(direction.Value);
            }
        }

        /// <summary>处理一次滑动输入并刷新视图，返回棋盘是否发生变化。</summary>
        public bool ProcessInput(MoveDirection direction)
        {
            MoveResult result = game.Move(direction);
            view?.Render(game);
            return result.Changed;
        }

        public void Restart()
        {
            game.NewGame();
            view?.Render(game);
        }

        /// <summary>读取本帧按下的方向键；输入读取与移动处理分开，便于测试替换。</summary>
        public static MoveDirection? ReadPressedDirection()
        {
            foreach (KeyCode key in DirectionKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    return Game2048InputMap.MapKey(key);
                }
            }

            return null;
        }

        private static readonly KeyCode[] DirectionKeys =
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        };
    }
}
