using Lyking.Game2048.Core;
using Xunit;

namespace Lyking.Game2048.Core.Tests;

public sealed class GameBoardTests
{
    [Fact]
    public void 默认棋盘为4x4全空()
    {
        var board = new GameBoard();

        Assert.Equal(4, board.Rows);
        Assert.Equal(4, board.Columns);
        Assert.Equal(0, board.CountNonEmpty);
        Assert.False(board.IsFull);
        Assert.True(board.HasEmpty);
    }

    [Fact]
    public void 索引器读写指定格子()
    {
        var board = new GameBoard();
        board[2, 3] = 16;

        Assert.Equal(16, board[2, 3]);
        Assert.Equal(1, board.CountNonEmpty);
    }

    [Fact]
    public void 满盘状态正确()
    {
        var board = new GameBoard();
        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Columns; col++)
            {
                board[row, col] = 2;
            }
        }

        Assert.True(board.IsFull);
        Assert.False(board.HasEmpty);
    }

    [Fact]
    public void 克隆棋盘与原盘独立()
    {
        var board = new GameBoard();
        board[0, 0] = 8;
        GameBoard clone = board.Clone();
        clone[0, 0] = 64;
        clone[1, 1] = 32;

        Assert.Equal(8, board[0, 0]);
        Assert.Equal(0, board[1, 1]);
        Assert.Equal(64, clone[0, 0]);
        Assert.Equal(32, clone[1, 1]);
        Assert.True(board.Clone().Equals(board));
        Assert.False(board.Equals(clone));
    }

    [Fact]
    public void 非法尺寸被拒绝()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameBoard(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameBoard(4, 0));
    }

    [Fact]
    public void 越界访问被拒绝()
    {
        var board = new GameBoard();

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => _ = board[4, 0]);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => board[0, -1] = 2);
    }
}
