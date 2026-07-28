using HexStrategy.Core.Hexes;
using HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillConsoleBoardRenderer
{
    public static string Render(KingOfTheHillGameState state)
    {
        var lines = new List<string>
        {
            $"Board radius: {state.Board.Radius}",
            "Legend: [q,r:cell]",
            "  cell = .. empty, ** objective, p1a/p1b/p2a/p2b unit"
        };

        for (var r = -state.Board.Radius; r <= state.Board.Radius; r++)
        {
            var coordinates = CoordinatesForRow(state.Board.Radius, r).ToArray();
            var indent = new string(' ', Math.Max(0, state.Board.Radius - coordinates.Length + 1));
            var cells = coordinates.Select(coordinate => FormatCell(state, coordinate));
            lines.Add($"{indent}{string.Join(" ", cells)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<HexCoordinate> CoordinatesForRow(int radius, int r)
    {
        var minQ = Math.Max(-radius, -r - radius);
        var maxQ = Math.Min(radius, -r + radius);

        for (var q = minQ; q <= maxQ; q++)
        {
            yield return new HexCoordinate(q, r);
        }
    }

    private static string FormatCell(KingOfTheHillGameState state, HexCoordinate coordinate)
    {
        var unit = state.Units.SingleOrDefault(existingUnit => existingUnit.Position == coordinate);
        var occupant = unit?.Id ?? (coordinate == HexCoordinate.Origin ? "**" : "..");

        return $"[{coordinate.Q,2},{coordinate.R,2}:{occupant,-3}]";
    }
}
