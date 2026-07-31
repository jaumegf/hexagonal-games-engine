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

    public IReadOnlyList<HexCoordinate> GetReachableCoordinates(HexCoordinate origin, int maxDepth)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Depth must be zero or greater.");
        }

        if (!Contains(origin) || maxDepth == 0)
        {
            return [];
        }

        var visited = new HashSet<HexCoordinate> { origin };
        var depths = new Dictionary<HexCoordinate, int> { [origin] = 0 };
        var queue = new Queue<HexCoordinate>();
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDepth = depths[current];

            if (currentDepth >= maxDepth)
            {
                continue;
            }

            foreach (var neighbor in GetAdjacentCoordinates(current))
            {
                if (!visited.Add(neighbor))
                {
                    continue;
                }

                depths[neighbor] = currentDepth + 1;
                queue.Enqueue(neighbor);
            }
        }

        return Coordinates
            .Where(coordinate => coordinate != origin && visited.Contains(coordinate))
            .ToArray();
    }

    public IReadOnlyList<HexCoordinate> GetCoordinatesForRow(int r) =>
        Coordinates.Where(coordinate => coordinate.R == r).ToArray();

    public IReadOnlyList<HexCoordinate> GetAdjacentCoordinates(HexCoordinate coordinate) =>
        Coordinates
            .Where(candidate => adjacentPairs.Contains((coordinate, candidate)))
            .ToArray();

    private static HashSet<(HexCoordinate From, HexCoordinate To)> BuildAdjacentPairs(
        IReadOnlyList<HexCoordinate> coordinates)
    {
        var pairs = new HashSet<(HexCoordinate From, HexCoordinate To)>();
        for (var index = 0; index < coordinates.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < coordinates.Count; otherIndex++)
            {
                if (coordinates[index].DistanceTo(coordinates[otherIndex]) == 1)
                {
                    AddPair(pairs, coordinates[index], coordinates[otherIndex]);
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
