using System;

namespace Lyking.Game2048.Core
{
    /// <summary>一局 2048 对局：封装棋盘、得分、状态与移动流程。</summary>
    public sealed class Game2048Game
    {
        private readonly Random random;

        public Game2048Game(int size = GameBoard.DefaultSize, Random? random = null)
        {
            this.random = random ?? new Random();
            Board = new GameBoard(size, size);
            Score = 0;
            Status = GameStatus.Playing;
            HasWon = false;
            NewGame();
        }

        public GameBoard Board { get; private set; }

        public int Score { get; private set; }

        public GameStatus Status { get; private set; }

        /// <summary>一旦合成出 2048 即保持为真；对局可以继续。</summary>
        public bool HasWon { get; private set; }

        public void NewGame()
        {
            Board = new GameBoard(Board.Rows, Board.Columns);
            Score = 0;
            Status = GameStatus.Playing;
            HasWon = false;
            GameRules.TrySpawnRandomTile(Board, random, out _);
            GameRules.TrySpawnRandomTile(Board, random, out _);
        }

        public MoveResult Move(MoveDirection direction)
        {
            if (Status == GameStatus.Over)
            {
                return new MoveResult(false, 0, 0);
            }

            MoveResult result = GameRules.Move(Board, direction);
            if (!result.Changed)
            {
                // 无效移动不生成新块，但仍需在盘面已无可动时进入结束状态。
                if (GameRules.IsGameOver(Board))
                {
                    Status = GameStatus.Over;
                }

                return result;
            }

            Score += result.GainedScore;
            GameRules.TrySpawnRandomTile(Board, random, out _);
            if (GameRules.ContainsTarget(Board))
            {
                HasWon = true;
            }

            Status = GameRules.IsGameOver(Board) ? GameStatus.Over : GameStatus.Playing;
            return result;
        }
    }
}
