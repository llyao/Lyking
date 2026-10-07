using Lyking.Game2048.Core;
using Xunit;

namespace Lyking.Game2048.Core.Tests;

public sealed class Game2048GameTests
{
    [Fact]
    public void 新开局恰好生成两个新块()
    {
        var game = new Game2048Game(random: new Random(42));

        game.NewGame();

        Assert.Equal(2, game.Board.CountNonEmpty);
        Assert.Equal(0, game.Score);
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.False(game.HasWon);
    }

    [Fact]
    public void 同一种子开局盘面一致()
    {
        var first = new Game2048Game(random: new Random(99));
        var second = new Game2048Game(random: new Random(99));

        first.NewGame();
        second.NewGame();

        Assert.True(first.Board.Equals(second.Board));
    }

    [Fact]
    public void 有效移动后得分累加并生成新块()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();

        int scoreBefore = game.Score;
        int tilesBefore = game.Board.CountNonEmpty;
        MoveResult result = MoveUntilChanged(game);

        Assert.True(result.Changed);
        Assert.Equal(scoreBefore + result.GainedScore, game.Score);
        Assert.Equal(tilesBefore + 1, game.Board.CountNonEmpty);
    }

    [Fact]
    public void 无效移动不生成新块()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();
        GameRules.Move(game.Board, MoveDirection.Up);
        // 构造一个四个方向都无法改变的局面：满盘且无相邻相等
        FillBoard(game.Board, new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });
        int tilesBefore = game.Board.CountNonEmpty;

        foreach (MoveDirection direction in Enum.GetValues<MoveDirection>())
        {
            game.Move(direction);
        }

        Assert.Equal(tilesBefore, game.Board.CountNonEmpty);
        Assert.Equal(GameStatus.Over, game.Status);
    }

    [Fact]
    public void 结束后的移动不再改变棋盘()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();
        FillBoard(game.Board, new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });

        MoveResult result = game.Move(MoveDirection.Left);

        Assert.False(result.Changed);
        Assert.Equal(GameStatus.Over, game.Status);
    }

    [Fact]
    public void 首次合成2048标记胜利且可继续()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();
        FillBoard(game.Board, new[,] { { 1024, 1024, 2, 4 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });

        game.Move(MoveDirection.Left);

        Assert.True(game.HasWon);
        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void 满盘且无可合并时状态为结束()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();
        FillBoard(game.Board, new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });

        game.Move(MoveDirection.Left);

        Assert.Equal(GameStatus.Over, game.Status);
    }

    [Fact]
    public void 重新开始清空得分与胜利标记()
    {
        var game = new Game2048Game(random: new Random(42));
        game.NewGame();
        FillBoard(game.Board, new[,] { { 1024, 1024, 2, 4 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });
        game.Move(MoveDirection.Left);

        game.NewGame();

        Assert.Equal(0, game.Score);
        Assert.False(game.HasWon);
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(2, game.Board.CountNonEmpty);
    }

    private static MoveResult MoveUntilChanged(Game2048Game game)
    {
        foreach (MoveDirection direction in Enum.GetValues<MoveDirection>())
        {
            MoveResult result = game.Move(direction);
            if (result.Changed)
            {
                return result;
            }
        }

        throw new InvalidOperationException("测试局面应存在至少一个有效移动方向。");
    }

    private static void FillBoard(GameBoard board, int[,] cells)
    {
        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Columns; col++)
            {
                board[row, col] = cells[row, col];
            }
        }
    }
}
