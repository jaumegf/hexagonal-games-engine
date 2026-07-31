using HexStrategy.Core.Hexes;

namespace HexStrategy.Core.Tests;

public sealed class HexBoardTests
{
    [Fact]
    public void GetReachableCoordinates_DepthOne_ReturnsAdjacentCoordinates()
    {
        var board = new HexBoard(2);

        var reachable = board.GetReachableCoordinates(HexCoordinate.Origin, 1);

        Assert.Equal(6, reachable.Count);
        Assert.All(reachable, coordinate => Assert.True(board.AreAdjacent(HexCoordinate.Origin, coordinate)));
    }

    [Fact]
    public void GetReachableCoordinates_DepthTwo_ExpandsBeyondAdjacentCoordinates()
    {
        var board = new HexBoard(3);

        var reachable = board.GetReachableCoordinates(HexCoordinate.Origin, 2);

        Assert.Contains(new HexCoordinate(2, 0), reachable);
        Assert.Contains(new HexCoordinate(0, 2), reachable);
        Assert.DoesNotContain(HexCoordinate.Origin, reachable);
    }

    [Fact]
    public void GetReachableCoordinates_OffBoardOrigin_ReturnsEmpty()
    {
        var board = new HexBoard(2);

        var reachable = board.GetReachableCoordinates(new HexCoordinate(4, 4), 2);

        Assert.Empty(reachable);
    }
}
