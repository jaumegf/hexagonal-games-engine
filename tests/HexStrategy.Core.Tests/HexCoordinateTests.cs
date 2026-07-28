using HexStrategy.Core.Hexes;

namespace HexStrategy.Core.Tests;

public sealed class HexCoordinateTests
{
    [Fact]
    public void DistanceTo_AdjacentHex_ReturnsOne()
    {
        var origin = HexCoordinate.Origin;
        var target = new HexCoordinate(1, 0);

        Assert.Equal(1, origin.DistanceTo(target));
        Assert.True(origin.IsAdjacentTo(target));
    }
}
