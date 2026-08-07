using HexStrategy.Core.Commands;
using HexStrategy.Core.Hexes;

namespace HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillGameRules
{
    public static GameCommandResult Execute(KingOfTheHillGameState state, GameCommand command)
        => Execute(state, command, evaluateVictory: true);

    internal static GameCommandResult Preview(KingOfTheHillGameState state, GameCommand command)
        => Execute(state, command, evaluateVictory: false);

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
        var movementDepth = GetMovementDepth(unit, target);
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

        if (!CanRoleOccupyCoordinate(unit, target))
        {
            return GameCommandResult.Rejected(
                state,
                $"{unit.Id} ({unit.Role}) cannot enter {DescribeRing(target)}.");
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
            if (unit.Role == KingOfTheHillUnitRole.Defender || targetUnit.Role == KingOfTheHillUnitRole.Defender)
            {
                return GameCommandResult.Rejected(
                    state,
                    $"Defender units cannot merge.");
            }

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

        var movedState = state with
        {
            Units = updatedUnits
        };

        return EndTurn(state, movedState, actionMessage, null, evaluateVictory);
    }

    private static int GetMovementDepth(
        KingOfTheHillUnitState unit,
        HexCoordinate target)
    {
        if (unit.Role != KingOfTheHillUnitRole.Single)
        {
            return 1;
        }

        var sourceRing = unit.Position.DistanceTo(HexCoordinate.Origin);
        var targetRing = target.DistanceTo(HexCoordinate.Origin);

        return targetRing < sourceRing ? 1 : 2;
    }

    private static bool CanRoleOccupyCoordinate(
        KingOfTheHillUnitState unit,
        HexCoordinate coordinate)
    {
        var ring = coordinate.DistanceTo(HexCoordinate.Origin);

        return unit.Role switch
        {
            KingOfTheHillUnitRole.Single => true,
            KingOfTheHillUnitRole.Double => ring >= 1,
            KingOfTheHillUnitRole.Defender => ring >= 2,
            KingOfTheHillUnitRole.Attacker => ring >= 2,
            _ => false
        };
    }

    private static string DescribeRing(HexCoordinate coordinate)
    {
        var ring = coordinate.DistanceTo(HexCoordinate.Origin);
        return ring switch
        {
            0 => "Objective",
            1 => "r1",
            _ => $"r{ring}"
        };
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

    private static bool IsDefenderIdentifier(string unitId) =>
        unitId is "1T" or "1V" or "1X" or "2T" or "2V" or "2X";

    private static GameCommandResult EndTurn(
        KingOfTheHillGameState turnStartState,
        KingOfTheHillGameState state,
        string actionMessage,
        string? defenderRoleMessage,
        bool evaluateVictory)
    {
        var singleOnObjective = state.Units.SingleOrDefault(unit =>
            unit.Position == HexCoordinate.Origin &&
            unit.Role == KingOfTheHillUnitRole.Single);

        if (evaluateVictory && singleOnObjective is not null)
        {
            var winner = state.Players.First(player => player.Id == singleOnObjective.OwnerPlayerId);
            var completedState = state with
            {
                IsCompleted = true,
                WinnerPlayerId = winner.Id
            };

            return GameCommandResult.Success(
                completedState,
                ComposeTurnMessage(
                    actionMessage,
                    defenderRoleMessage,
                    null,
                    $"{winner.DisplayName} captures the Hill and wins."));
        }

        var nextPlayerId = state.Players.First(player => player.Id != state.CurrentPlayerId).Id;
        var firstPlayerId = state.Players[0].Id;
        var nextState = state with
        {
            CurrentPlayerId = nextPlayerId,
            TurnNumber = string.Equals(nextPlayerId, firstPlayerId, StringComparison.OrdinalIgnoreCase)
                ? state.TurnNumber + 1
                : state.TurnNumber
        };

        return GameCommandResult.Success(
            nextState,
            ComposeTurnMessage(actionMessage, defenderRoleMessage, null, $"{state.CurrentPlayer.DisplayName} ends the turn."));
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

        if (CanPlayerStillRetakeObjective(state, trailingPlayerId))
        {
            return false;
        }

        winnerPlayerId = objectiveHolder.OwnerPlayerId;
        return true;
    }

    private static bool CanPlayerStillRetakeObjective(
        KingOfTheHillGameState state,
        string playerId)
    {
        var searchState = state with
        {
            CurrentPlayerId = playerId,
            IsCompleted = false,
            WinnerPlayerId = null
        };

        return CanPlayerBreakHillDefenseWithinTurns(searchState, playerId, remainingTurns: 2);
    }

    private static bool CanPlayerBreakHillDefenseWithinTurns(
        KingOfTheHillGameState state,
        string playerId,
        int remainingTurns)
    {
        if (remainingTurns <= 0)
        {
            return false;
        }

        var legalCommands = KingOfTheHillAiMoveGenerator.GenerateLegalCommands(state, evaluateVictory: false);

        foreach (var command in legalCommands)
        {
            var preview = Preview(state, command);
            if (!preview.Accepted || preview.State is not KingOfTheHillGameState previewState)
            {
                continue;
            }

            if (DoesStateBreakHillDefense(previewState, playerId))
            {
                return true;
            }

            var continuedState = previewState with
            {
                CurrentPlayerId = playerId,
                IsCompleted = false,
                WinnerPlayerId = null
            };

            if (CanPlayerBreakHillDefenseWithinTurns(continuedState, playerId, remainingTurns - 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DoesStateBreakHillDefense(
        KingOfTheHillGameState state,
        string playerId)
    {
        var objectiveHolder = state.FindUnitAt(HexCoordinate.Origin);
        if (objectiveHolder is null)
        {
            return true;
        }

        if (string.Equals(objectiveHolder.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var attackerAdjacentStrength = GetAdjacentStrength(state, playerId);
        var defenderTotalHillDefense = GetTotalHillDefense(state, objectiveHolder);

        return attackerAdjacentStrength > defenderTotalHillDefense;
    }

    private static int GetAdjacentStrength(
        KingOfTheHillGameState state,
        string playerId) =>
        state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

    private static int GetTotalHillDefense(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState objectiveHolder)
    {
        var adjacentSupport = state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, objectiveHolder.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
                unit.Id != objectiveHolder.Id &&
                state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        return objectiveHolder.Strength + adjacentSupport;
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

        var adjacentPressure = GetAdjacentStrength(state, state.CurrentPlayerId);
        var totalDefense = GetTotalHillDefense(state, defender);

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
