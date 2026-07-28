using HexStrategy.Core.Commands;
using HexStrategy.Core.Hexes;

namespace HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillGameRules
{
    private const int RequiredControlScoreToWin = 3;

    public static GameCommandResult Execute(KingOfTheHillGameState state, GameCommand command)
    {
        if (state.IsCompleted)
        {
            return GameCommandResult.Rejected(state, "The game is already complete.");
        }

        return command.Name.ToLowerInvariant() switch
        {
            "move" => ExecuteMove(state, command),
            "pass" => EndTurn(state, $"{state.CurrentPlayer.DisplayName} passes."),
            _ => GameCommandResult.Rejected(state, $"Unknown command '{command.Name}'.")
        };
    }

    private static GameCommandResult ExecuteMove(KingOfTheHillGameState state, GameCommand command)
    {
        var unitId = command.GetRequiredArgument("unitId");

        if (!int.TryParse(command.GetRequiredArgument("q"), out var q) ||
            !int.TryParse(command.GetRequiredArgument("r"), out var r))
        {
            return GameCommandResult.Rejected(state, "Coordinates must be valid integers.");
        }

        var unit = state.FindUnit(unitId);

        if (unit is null)
        {
            return GameCommandResult.Rejected(state, $"Unit '{unitId}' does not exist.");
        }

        if (!string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return GameCommandResult.Rejected(state, $"Unit '{unitId}' does not belong to {state.CurrentPlayer.DisplayName}.");
        }

        var target = new HexCoordinate(q, r);

        if (!state.Board.Contains(target))
        {
            return GameCommandResult.Rejected(state, $"Target {target} is outside the board.");
        }

        if (target == unit.Position)
        {
            return GameCommandResult.Rejected(state, "A unit must move to an adjacent hex.");
        }

        if (!unit.Position.IsAdjacentTo(target))
        {
            return GameCommandResult.Rejected(state, $"Target {target} is not adjacent to {unit.Position}.");
        }

        if (state.IsOccupied(target))
        {
            return GameCommandResult.Rejected(state, $"Target {target} is already occupied.");
        }

        var updatedUnits = state.Units
            .Select(existingUnit => existingUnit.Id == unit.Id ? existingUnit with { Position = target } : existingUnit)
            .ToArray();

        var movedState = state with { Units = updatedUnits };

        return EndTurn(
            movedState,
            $"{state.CurrentPlayer.DisplayName} moved {unit.Id} to {target}.");
    }

    private static GameCommandResult EndTurn(KingOfTheHillGameState state, string actionMessage)
    {
        var updatedScores = state.ControlScores.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        var currentPlayerHasObjective = state.Units.Any(unit =>
            unit.OwnerPlayerId == state.CurrentPlayerId &&
            unit.Position == HexCoordinate.Origin);

        var controlAwarded = false;

        if (currentPlayerHasObjective)
        {
            updatedScores[state.CurrentPlayerId] += 1;
            controlAwarded = true;
        }

        if (updatedScores[state.CurrentPlayerId] >= RequiredControlScoreToWin)
        {
            var winningState = state with
            {
                ControlScores = updatedScores,
                IsCompleted = true,
                WinnerPlayerId = state.CurrentPlayerId
            };

            return GameCommandResult.Success(
                winningState,
                $"{actionMessage} {state.CurrentPlayer.DisplayName} controls the hill and wins.");
        }

        var nextPlayerId = state.Players.First(player => player.Id != state.CurrentPlayerId).Id;
        var nextState = state with
        {
            ControlScores = updatedScores,
            CurrentPlayerId = nextPlayerId,
            TurnNumber = state.TurnNumber + 1
        };

        var scoreMessage = controlAwarded
            ? $"{state.CurrentPlayer.DisplayName} gains 1 control point."
            : $"{state.CurrentPlayer.DisplayName} does not control the hill.";

        return GameCommandResult.Success(nextState, $"{actionMessage} {scoreMessage}");
    }
}
