namespace HexStrategy.Core.Hexes;

public sealed class HexBoard
{
    private readonly HashSet<(HexCoordinate From, HexCoordinate To)> adjacentPairs = [];

    public HexBoard(int radius)
        : this(CreateHexagonCoordinates(radius))
    {
    }

    public HexBoard(IEnumerable<HexCoordinate> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        var normalizedCoordinates = coordinates
            .Distinct()
            .OrderBy(coordinate => coordinate.R)
            .ThenBy(coordinate => coordinate.Q)
            .ToArray();

        if (normalizedCoordinates.Length == 0)
        {
            throw new ArgumentException("Board must contain at least one coordinate.", nameof(coordinates));
        }

        Radius = normalizedCoordinates.Max(coordinate => coordinate.DistanceTo(HexCoordinate.Origin));
        Coordinates = normalizedCoordinates;
        adjacentPairs = BuildAdjacentPairs(normalizedCoordinates);
    }

    public int Radius { get; }

    public IReadOnlyList<HexCoordinate> Coordinates { get; }

    public bool Contains(HexCoordinate coordinate) => Coordinates.Contains(coordinate);

    public bool AreAdjacent(HexCoordinate from, HexCoordinate to) =>
        adjacentPairs.Contains((from, to));

    public IReadOnlyList<HexCoordinate> GetCoordinatesForRow(int r) =>
        Coordinates.Where(coordinate => coordinate.R == r).ToArray();

    private static HashSet<(HexCoordinate From, HexCoordinate To)> BuildAdjacentPairs(
        IReadOnlyList<HexCoordinate> coordinates)
    {
        var rows = coordinates
            .GroupBy(coordinate => coordinate.R)
            .OrderBy(group => group.Key)
            .Select((group, rowIndex) =>
            {
                var orderedCoordinates = group.OrderBy(coordinate => coordinate.Q).ToArray();
                var startX = -(orderedCoordinates.Length - 1) / 2.0;

                return new
                {
                    RowIndex = rowIndex,
                    Tiles = orderedCoordinates
                        .Select((coordinate, columnIndex) => new
                        {
                            Coordinate = coordinate,
                            X = startX + columnIndex
                        })
                        .ToArray()
                };
            })
            .ToArray();

        var pairs = new HashSet<(HexCoordinate From, HexCoordinate To)>();

        foreach (var row in rows)
        {
            foreach (var pair in row.Tiles.Zip(row.Tiles.Skip(1)))
            {
                AddPair(pairs, pair.First.Coordinate, pair.Second.Coordinate);
            }
        }

        foreach (var rowPair in rows.Zip(rows.Skip(1)))
        {
            foreach (var upper in rowPair.First.Tiles)
            {
                foreach (var lower in rowPair.Second.Tiles)
                {
                    if (Math.Abs(upper.X - lower.X) == 0.5)
                    {
                        AddPair(pairs, upper.Coordinate, lower.Coordinate);
                    }
                }
            }
        }

        return pairs;
    }

    private static void AddPair(
        ISet<(HexCoordinate From, HexCoordinate To)> pairs,
        HexCoordinate from,
        HexCoordinate to)
    {
        pairs.Add((from, to));
        pairs.Add((to, from));
    }

    private static IEnumerable<HexCoordinate> CreateHexagonCoordinates(int radius)
    {
        if (radius < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Board radius must be at least 1.");
        }

        for (var r = -radius; r <= radius; r++)
        {
            var minQ = Math.Max(-radius, -r - radius);
            var maxQ = Math.Min(radius, -r + radius);

            for (var q = minQ; q <= maxQ; q++)
            {
                yield return new HexCoordinate(q, r);
            }
        }
    }
}
