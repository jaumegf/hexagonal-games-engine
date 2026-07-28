namespace HexStrategy.Core.Hexes;

public sealed class HexBoard
{
    public HexBoard(int radius)
    {
        if (radius < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Board radius must be at least 1.");
        }

        Radius = radius;
    }

    public int Radius { get; }

    public bool Contains(HexCoordinate coordinate) => coordinate.DistanceTo(HexCoordinate.Origin) <= Radius;
}
