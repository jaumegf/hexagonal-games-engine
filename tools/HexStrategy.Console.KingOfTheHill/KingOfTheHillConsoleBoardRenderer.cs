using HexStrategy.Core.Hexes;
using HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillConsoleBoardRenderer
{
    public static string Render(KingOfTheHillGameState state)
    {
        var lines = new List<string>
        {
            $"Board extent: radius {state.Board.Radius}",
            "Legend: [q,r:cell]",
            "  cell = .. empty, ** objective, 1A or 1Ax2 stacked block"
        };

        var rows = state.Board.Coordinates
            .GroupBy(coordinate => coordinate.R)
            .OrderBy(group => group.Key)
            .Select(group => group
                .OrderBy(coordinate => coordinate.Q)
                .ToArray())
            .ToArray();
        var maxRowLength = rows.Max(row => row.Length);

        foreach (var coordinates in rows)
        {
            var indent = new string(' ', Math.Max(0, (maxRowLength - coordinates.Length) * 3 / 2));
            var cells = coordinates.Select(coordinate => FormatCell(state, coordinate));
            lines.Add($"{indent}{string.Join(" ", cells)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCell(KingOfTheHillGameState state, HexCoordinate coordinate)
    {
        var unit = state.FindUnitAt(coordinate);
        var occupant = unit is null
            ? (coordinate == HexCoordinate.Origin ? "**" : "..")
            : unit.Strength == 1
                ? unit.Id
                : $"{unit.Id}x{unit.Strength}";

        return $"[{coordinate.Q,2},{coordinate.R,2}:{occupant,-3}]";
    }
}
