using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Game.KingOfTheHill;

var catalog = new GameCatalog(new[] { new KingOfTheHillGameDefinition() });
var matchService = new GameMatchService(catalog);
var currentMatch = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

Console.WriteLine("HexStrategy console sample");
Console.WriteLine("Type 'help' to see commands.");
Console.WriteLine();

while (true)
{
    var state = (KingOfTheHillGameState)currentMatch.State;

    Console.WriteLine(KingOfTheHillConsoleBoardRenderer.Render(state));
    Console.WriteLine($"Current player: {state.CurrentPlayer.DisplayName}");
    Console.WriteLine($"Score: P1={state.ControlScores["P1"]} | P2={state.ControlScores["P2"]}");

    if (state.IsCompleted)
    {
        Console.WriteLine($"Winner: {state.WinnerPlayerId}");
        break;
    }

    Console.Write("> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("Enter a command. Type 'help' for usage.");
        Console.WriteLine();
        continue;
    }

    var trimmedInput = input.Trim();

    if (trimmedInput.Equals("help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  move <unitId> <q> <r>");
        Console.WriteLine("  pass");
        Console.WriteLine("  show");
        Console.WriteLine("  help");
        Console.WriteLine();
        continue;
    }

    if (trimmedInput.Equals("show", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine();
        continue;
    }

    if (!TryParseCommand(trimmedInput, out var command, out var parseError))
    {
        Console.WriteLine(parseError);
        Console.WriteLine();
        continue;
    }

    var commandResult = matchService.Execute(currentMatch, command!);
    currentMatch = commandResult.Match;

    Console.WriteLine(commandResult.Message);
    Console.WriteLine();
}

static bool TryParseCommand(string input, out GameCommand? command, out string error)
{
    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 0)
    {
        command = null;
        error = "Enter a command.";
        return false;
    }

    if (parts[0].Equals("pass", StringComparison.OrdinalIgnoreCase))
    {
        command = new GameCommand("pass");
        error = string.Empty;
        return true;
    }

    if (parts[0].Equals("move", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length != 4)
        {
            command = null;
            error = "Usage: move <unitId> <q> <r>";
            return false;
        }

        command = new GameCommand(
            "move",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitId"] = parts[1],
                ["q"] = parts[2],
                ["r"] = parts[3]
            });
        error = string.Empty;
        return true;
    }

    command = null;
    error = $"Unknown command '{input}'. Type 'help' for usage.";
    return false;
}
