using Lyking.Game2048.Core;
using Xunit;

namespace Lyking.Game2048.Core.Tests;

public sealed class GameRulesTests
{
    [Fact]
    public void 无可合并且已靠边的行滑动不产生变化()
    {
        var board = CreateBoard(new[,] { { 2, 4, 2, 4 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        MoveResult result = GameRules.Move(board, MoveDirection.Left);

        Assert.False(result.Changed);
        Assert.Equal(0, result.GainedScore);
        Assert.Equal(0, result.MergedCount);
    }

    [Fact]
    public void 相邻两块向左合并并计分()
    {
        var board = CreateBoard(new[,] { { 2, 2, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        MoveResult result = GameRules.Move(board, MoveDirection.Left);

        Assert.True(result.Changed);
        Assert.Equal(4, board[0, 0]);
        Assert.Equal(0, board[0, 1]);
        Assert.Equal(4, result.GainedScore);
        Assert.Equal(1, result.MergedCount);
    }

    [Fact]
    public void 四个相等块向左只合并为两块()
    {
        var board = CreateBoard(new[,] { { 2, 2, 2, 2 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        MoveResult result = GameRules.Move(board, MoveDirection.Left);

        Assert.Equal(4, board[0, 0]);
        Assert.Equal(4, board[0, 1]);
        Assert.Equal(0, board[0, 2]);
        Assert.Equal(0, board[0, 3]);
        Assert.Equal(8, result.GainedScore);
        Assert.Equal(2, result.MergedCount);
    }

    [Fact]
    public void 合并后的块不再参与本次移动的后续合并()
    {
        var board = CreateBoard(new[,] { { 2, 2, 2, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Left);

        Assert.Equal(4, board[0, 0]);
        Assert.Equal(2, board[0, 1]);
        Assert.Equal(0, board[0, 2]);
    }

    [Fact]
    public void 向左移动压缩中间空位()
    {
        var board = CreateBoard(new[,] { { 0, 2, 0, 2 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Left);

        Assert.Equal(4, board[0, 0]);
        Assert.Equal(0, board[0, 1]);
        Assert.Equal(0, board[0, 2]);
        Assert.Equal(0, board[0, 3]);
    }

    [Fact]
    public void 向右移动在行尾合并()
    {
        var board = CreateBoard(new[,] { { 2, 2, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Right);

        Assert.Equal(4, board[0, 3]);
        Assert.Equal(0, board[0, 0]);
    }

    [Fact]
    public void 向上移动按列合并()
    {
        var board = CreateBoard(new[,] { { 2, 0, 0, 0 }, { 2, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Up);

        Assert.Equal(4, board[0, 0]);
        Assert.Equal(0, board[1, 0]);
        Assert.Equal(0, board[2, 0]);
        Assert.Equal(0, board[3, 0]);
    }

    [Fact]
    public void 向下移动按列合并()
    {
        var board = CreateBoard(new[,] { { 2, 0, 0, 0 }, { 2, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Down);

        Assert.Equal(4, board[3, 0]);
        Assert.Equal(0, board[0, 0]);
        Assert.Equal(0, board[1, 0]);
        Assert.Equal(0, board[2, 0]);
    }

    [Fact]
    public void 移动只影响对应行列()
    {
        var board = CreateBoard(new[,] { { 2, 2, 0, 0 }, { 4, 8, 16, 32 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        GameRules.Move(board, MoveDirection.Left);

        Assert.Equal(4, board[0, 0]);
        Assert.Equal(4, board[1, 0]);
        Assert.Equal(8, board[1, 1]);
        Assert.Equal(16, board[1, 2]);
        Assert.Equal(32, board[1, 3]);
    }

    [Fact]
    public void 棋盘满且无相邻相等时判定结束()
    {
        var board = CreateBoard(new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });

        Assert.True(GameRules.IsGameOver(board));
    }

    [Fact]
    public void 棋盘满但存在可合并对时未结束()
    {
        var board = CreateBoard(new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 2, 2 } });

        Assert.False(GameRules.IsGameOver(board));
    }

    [Fact]
    public void 存在空位时未结束()
    {
        var board = CreateBoard(new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 0 } });

        Assert.False(GameRules.IsGameOver(board));
    }

    [Fact]
    public void 达到目标值时判定胜利()
    {
        var board = CreateBoard(new[,] { { 1024, 1024, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } });

        Assert.False(GameRules.ContainsTarget(board));
        board[0, 1] = 2048;
        Assert.True(GameRules.ContainsTarget(board));
    }

    [Fact]
    public void 空位随机生成成功且只产出2或4()
    {
        var board = new GameBoard();
        var random = new Random(20260101);

        bool spawned = GameRules.TrySpawnRandomTile(board, random, out int value);

        Assert.True(spawned);
        Assert.True(value is 2 or 4);
        Assert.Equal(1, board.CountNonEmpty);
    }

    [Fact]
    public void 满盘随机生成失败()
    {
        var board = CreateBoard(new[,] { { 2, 4, 8, 16 }, { 16, 8, 4, 2 }, { 2, 4, 8, 16 }, { 16, 8, 4, 2 } });

        bool spawned = GameRules.TrySpawnRandomTile(board, new Random(1), out int value);

        Assert.False(spawned);
        Assert.Equal(0, value);
    }

    [Fact]
    public void 同一随机种子生成结果确定()
    {
        var first = new GameBoard();
        var second = new GameBoard();

        GameRules.TrySpawnRandomTile(first, new Random(7), out int firstValue);
        GameRules.TrySpawnRandomTile(second, new Random(7), out int secondValue);

        Assert.Equal(firstValue, secondValue);
        Assert.Equal(first[0, 0], second[0, 0]);
    }

    [Fact]
    public void 大量生成的新块分布偏向2()
    {
        int twos = 0;
        const int total = 1000;
        for (int i = 0; i < total; i++)
        {
            var board = new GameBoard();
            GameRules.TrySpawnRandomTile(board, new Random(i), out int value);
            if (value == 2)
            {
                twos++;
            }
        }

        double ratio = (double)twos / total;
        Assert.InRange(ratio, 0.8, 0.99);
    }

    private static GameBoard CreateBoard(int[,] cells)
    {
        var board = new GameBoard(cells.GetLength(0), cells.GetLength(1));
        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Columns; col++)
            {
                board[row, col] = cells[row, col];
            }
        }

        return board;
    }
}
