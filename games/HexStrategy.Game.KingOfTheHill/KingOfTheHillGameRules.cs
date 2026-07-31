using HexStrategy.Core.Commands;
using HexStrategy.Core.Hexes;

namespace HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillGameRules
{
    public static GameCommandResult Execute(KingOfTheHillGameState state, GameCommand command)
        => Execute(state, command, evaluateVictory: true);

    private static GameCommandResult Execute(
        KingOfTheHillGameState state,
        GameCommand command,
        bool evaluateVictory)
    {
        if (state.IsCompleted)
        {
            return GameCommandResult.Rejected(state, "The game is already complete.");
        }

        return command.Name.ToLowerInvariant() switch
        {
            "move" => ExecuteMove(state, command, evaluateVictory),
            "pass" => EndTurn(state, state, $"{state.CurrentPlayer.DisplayName} passes.", null, evaluateVictory),
            _ => GameCommandResult.Rejected(state, $"Unknown command '{command.Name}'.")
        };
    }

    private static GameCommandResult ExecuteMove(
        KingOfTheHillGameState state,
        GameCommand command,
        bool evaluateVictory)
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
        var movementDepth = unit.Strength == 1 ? 2 : 1;
        var reachableCoordinates = state.Board.GetReachableCoordinates(unit.Position, movementDepth);

        if (!state.Board.Contains(target))
        {
            return GameCommandResult.Rejected(state, $"Target {target} is outside the board.");
        }

        if (target == unit.Position)
        {
            return GameCommandResult.Rejected(state, "A unit must move to a different hex.");
        }

        if (!reachableCoordinates.Contains(target))
        {
            return GameCommandResult.Rejected(
                state,
                $"Target {target} is outside the movement range of {unit.Id} (max depth {movementDepth}).");
        }

        if (!HasTraversablePath(state, unit, target, movementDepth))
        {
            return GameCommandResult.Rejected(
                state,
                $"{unit.Id} cannot reach {target} because no traversable path exists within depth {movementDepth}.");
        }

        var targetUnit = state.FindUnitAt(target);

        if (targetUnit is not null &&
            !string.Equals(targetUnit.OwnerPlayerId, unit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            if (unit.Strength <= targetUnit.Strength)
            {
                return GameCommandResult.Rejected(
                    state,
                    $"{unit.Id} (S{unit.Strength}) cannot defeat {targetUnit.Id} (S{targetUnit.Strength}) at {target}.");
            }
        }

        IReadOnlyList<KingOfTheHillUnitState> updatedUnits;
        string actionMessage;

        if (targetUnit is null)
        {
            updatedUnits = state.Units
                .Select(existingUnit => existingUnit.Id == unit.Id ? existingUnit with { Position = target } : existingUnit)
                .OrderBy(existingUnit => existingUnit.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            actionMessage = $"{state.CurrentPlayer.DisplayName} moved {unit.Id} to {target}.";
        }
        else if (string.Equals(targetUnit.OwnerPlayerId, unit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            var mergedStrength = unit.Strength + targetUnit.Strength;
            if (mergedStrength > KingOfTheHillGameState.MaximumBlockStrength)
            {
                return GameCommandResult.Rejected(
                    state,
                    $"{unit.Id} cannot merge into {targetUnit.Id} because blocks cannot exceed S{KingOfTheHillGameState.MaximumBlockStrength}.");
            }

            var mergedMemberIds = unit.MemberUnitIds
                .Concat(targetUnit.MemberUnitIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(memberId => memberId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var mergedUnit = new KingOfTheHillUnitState(
                mergedMemberIds[0],
                unit.OwnerPlayerId,
                target,
                mergedMemberIds);

            updatedUnits = state.Units
                .Where(existingUnit => existingUnit.Id != unit.Id && existingUnit.Id != targetUnit.Id)
                .Append(mergedUnit)
                .OrderBy(existingUnit => existingUnit.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            actionMessage = $"{state.CurrentPlayer.DisplayName} merged {unit.Id} into {targetUnit.Id} at {target} (S{mergedUnit.Strength}).";
        }
        else
        {
            updatedUnits = state.Units
                .Where(existingUnit => existingUnit.Id != unit.Id && existingUnit.Id != targetUnit.Id)
                .Append(unit with { Position = target })
                .OrderBy(existingUnit => existingUnit.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            actionMessage = $"{state.CurrentPlayer.DisplayName} attacked and eliminated {targetUnit.Id} with {unit.Id} at {target}.";
        }

        var updatedRetiredDefenderIds = GetUpdatedRetiredDefenderIds(state, updatedUnits, unit, targetUnit, target);
        var defenderRoleMessage = BuildDefenderRoleMessage(state.RetiredDefenderIds, updatedRetiredDefenderIds);
        var movedState = state with
        {
            Units = updatedUnits,
            RetiredDefenderIds = updatedRetiredDefenderIds
        };

        return EndTurn(state, movedState, actionMessage, defenderRoleMessage, evaluateVictory);
    }

    private static bool HasTraversablePath(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState movingUnit,
        HexCoordinate target,
        int maxDepth)
    {
        if (maxDepth <= 0 || target == movingUnit.Position)
        {
            return false;
        }

        var visited = new HashSet<HexCoordinate> { movingUnit.Position };
        var queue = new Queue<(HexCoordinate Coordinate, int Depth)>();
        queue.Enqueue((movingUnit.Position, 0));

        while (queue.Count > 0)
        {
            var (coordinate, depth) = queue.Dequeue();

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var neighbor in state.Board.GetAdjacentCoordinates(coordinate))
            {
                var stepDepth = depth + 1;

                if (neighbor == target)
                {
                    return true;
                }

                if (!CanTraverseIntermediateCoordinate(state, movingUnit, neighbor) ||
                    !visited.Add(neighbor))
                {
                    continue;
                }

                queue.Enqueue((neighbor, stepDepth));
            }
        }

        return false;
    }

    private static bool CanTraverseIntermediateCoordinate(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState movingUnit,
        HexCoordinate coordinate)
    {
        if (!state.Board.Contains(coordinate) || state.FindUnitAt(coordinate) is not null)
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyCollection<string> GetUpdatedRetiredDefenderIds(
        KingOfTheHillGameState state,
        IReadOnlyList<KingOfTheHillUnitState> updatedUnits,
        KingOfTheHillUnitState movingUnit,
        KingOfTheHillUnitState? targetUnit,
        HexCoordinate target)
    {
        var retiredIds = new HashSet<string>(state.RetiredDefenderIds, StringComparer.OrdinalIgnoreCase);

        if (target.DistanceTo(HexCoordinate.Origin) <= 1 &&
            IsDefenderIdentifier(movingUnit.Id))
        {
            retiredIds.Add(movingUnit.Id);
        }

        if (target.DistanceTo(HexCoordinate.Origin) <= 1 &&
            targetUnit is not null &&
            string.Equals(targetUnit.OwnerPlayerId, movingUnit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
            IsDefenderIdentifier(targetUnit.Id))
        {
            retiredIds.Add(targetUnit.Id);
        }

        var activeDefenderIds = updatedUnits
            .Where(unit => IsDefenderIdentifier(unit.Id) && !retiredIds.Contains(unit.Id))
            .Select(unit => unit.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (activeDefenderIds.Length < 2)
        {
            foreach (var activeDefenderId in activeDefenderIds)
            {
                retiredIds.Add(activeDefenderId);
            }
        }

        return retiredIds.ToArray();
    }

    private static bool IsDefenderIdentifier(string unitId) =>
        unitId is "1T" or "1V" or "1X" or "2T" or "2V" or "2X";

    private static GameCommandResult EndTurn(
        KingOfTheHillGameState turnStartState,
        KingOfTheHillGameState state,
        string actionMessage,
        string? defenderRoleMessage,
        bool evaluateVictory)
    {
        var siegeState = ApplyObjectiveSiegePressure(state, out var siegeMessage);
        var updatedScores = state.ControlScores.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        var currentPlayerHasObjective = siegeState.Units.Any(unit =>
            unit.OwnerPlayerId == state.CurrentPlayerId &&
            unit.Position == HexCoordinate.Origin);

        var controlAwarded = false;

        if (currentPlayerHasObjective)
        {
            updatedScores[state.CurrentPlayerId] += 1;
            controlAwarded = true;
        }

        var nextPlayerId = siegeState.Players.First(player => player.Id != state.CurrentPlayerId).Id;
        var firstPlayerId = siegeState.Players[0].Id;
        var scoredState = siegeState with
        {
            ControlScores = updatedScores
        };

        if (evaluateVictory &&
            TryResolveVictoryByMaterialExhaustion(scoredState, out var winnerPlayerId))
        {
            var winner = scoredState.Players.First(player => player.Id == winnerPlayerId);
            var completedState = scoredState with
            {
                IsCompleted = true,
                WinnerPlayerId = winnerPlayerId
            };

            return GameCommandResult.Success(
                completedState,
                ComposeTurnMessage(
                    actionMessage,
                    defenderRoleMessage,
                    siegeMessage,
                    $"{winner.DisplayName} wins. The opponent can no longer exceed the strength on Objective."));
        }

        var nextState = scoredState with
        {
            CurrentPlayerId = nextPlayerId,
            TurnNumber = string.Equals(nextPlayerId, firstPlayerId, StringComparison.OrdinalIgnoreCase)
                ? scoredState.TurnNumber + 1
                : scoredState.TurnNumber
        };

        var scoreMessage = controlAwarded
            ? $"{state.CurrentPlayer.DisplayName} gains 1 control point."
            : $"{state.CurrentPlayer.DisplayName} does not control the hill.";

        return GameCommandResult.Success(nextState, ComposeTurnMessage(actionMessage, defenderRoleMessage, siegeMessage, scoreMessage));
    }

    private static string? BuildDefenderRoleMessage(
        IReadOnlyCollection<string> previousRetiredDefenderIds,
        IReadOnlyCollection<string> updatedRetiredDefenderIds)
    {
        var newlyRetiredIds = updatedRetiredDefenderIds
            .Where(id => !previousRetiredDefenderIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (newlyRetiredIds.Length == 0)
        {
            return null;
        }

        return newlyRetiredIds.Length == 1
            ? $"Defender role retired for {newlyRetiredIds[0]}."
            : $"Defender roles retired for {string.Join(", ", newlyRetiredIds)}.";
    }

    private static bool TryResolveVictoryByMaterialExhaustion(
        KingOfTheHillGameState state,
        out string winnerPlayerId)
    {
        winnerPlayerId = string.Empty;

        var objectiveHolder = state.FindUnitAt(HexCoordinate.Origin);
        if (objectiveHolder is null)
        {
            return false;
        }

        var trailingPlayerId = state.Players
            .Select(player => player.Id)
            .FirstOrDefault(playerId => !string.Equals(playerId, objectiveHolder.OwnerPlayerId, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(trailingPlayerId))
        {
            return false;
        }

        if (CanPlayerStillRetakeObjective(state, trailingPlayerId, objectiveHolder.Strength))
        {
            return false;
        }

        winnerPlayerId = objectiveHolder.OwnerPlayerId;
        return true;
    }

    private static bool CanPlayerStillRetakeObjective(
        KingOfTheHillGameState state,
        string playerId,
        int defenderStrength)
    {
        var remainingStrength = state.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .Sum(unit => unit.Strength);

        return remainingStrength > defenderStrength;
    }

    private static KingOfTheHillGameState ApplyObjectiveSiegePressure(
        KingOfTheHillGameState state,
        out string? siegeMessage)
    {
        siegeMessage = null;

        var defender = state.FindUnitAt(HexCoordinate.Origin);
        if (defender is null ||
            string.Equals(defender.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return state;
        }

        var adjacentPressure = state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
                state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        var defenderAdjacentSupport = state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, defender.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
                unit.Id != defender.Id &&
                state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        var totalDefense = defender.Strength + defenderAdjacentSupport;

        if (adjacentPressure <= totalDefense)
        {
            return state;
        }

        if (defender.Strength <= 1)
        {
            var eliminatedUnits = state.Units
                .Where(unit => unit.Id != defender.Id)
                .OrderBy(unit => unit.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            siegeMessage = $"Siege pressure eliminates {defender.Id} on the Hill.";
            return state with { Units = eliminatedUnits };
        }

        var survivingMemberIds = defender.MemberUnitIds
            .OrderBy(memberId => memberId, StringComparer.OrdinalIgnoreCase)
            .Take(defender.Strength - 1)
            .ToArray();

        var reducedDefender = new KingOfTheHillUnitState(
            survivingMemberIds[0],
            defender.OwnerPlayerId,
            defender.Position,
            survivingMemberIds);

        var updatedUnits = state.Units
            .Where(unit => unit.Id != defender.Id)
            .Append(reducedDefender)
            .OrderBy(unit => unit.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        siegeMessage = $"Siege pressure reduces {defender.Id} on the Hill from S{defender.Strength} to S{reducedDefender.Strength}.";
        return state with { Units = updatedUnits };
    }

    private static string ComposeTurnMessage(
        string actionMessage,
        string? defenderRoleMessage,
        string? siegeMessage,
        string scoreMessage)
    {
        var segments = new[]
        {
            actionMessage,
            defenderRoleMessage,
            siegeMessage,
            scoreMessage
        }
        .Where(segment => !string.IsNullOrWhiteSpace(segment));

        return string.Join(" ", segments);
    }
}
