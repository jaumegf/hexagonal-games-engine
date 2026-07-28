namespace HexStrategy.Core.Hexes;

public readonly record struct HexCoordinate(int Q, int R)
{
    public static HexCoordinate Origin => new(0, 0);

    public int S => -Q - R;

    public int DistanceTo(HexCoordinate other)
    {
        var deltaQ = Math.Abs(Q - other.Q);
        var deltaR = Math.Abs(R - other.R);
        var deltaS = Math.Abs(S - other.S);

        return Math.Max(deltaQ, Math.Max(deltaR, deltaS));
    }

    public bool IsAdjacentTo(HexCoordinate other) => DistanceTo(other) == 1;

    public override string ToString() => $"({Q},{R})";
}
