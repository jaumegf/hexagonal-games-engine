using HexStrategy.Core.Commands;
using HexStrategy.Core.Hexes;
using HexStrategy.Core.Players;
using System.Diagnostics;

namespace HexStrategy.Game.KingOfTheHill;

internal static class KingOfTheHillAiController
{
    private static readonly IKingOfTheHillAiPlayer ReferenceAiPlayer = new KingOfTheHillAiLevel4Player();

    private static readonly IReadOnlyDictionary<PlayerControllerType, IKingOfTheHillAiPlayer> Players =
        new Dictionary<PlayerControllerType, IKingOfTheHillAiPlayer>
        {
            [PlayerControllerType.IaLevel1] = ReferenceAiPlayer,
            [PlayerControllerType.IaLevel2] = ReferenceAiPlayer,
            [PlayerControllerType.IaLevel3] = ReferenceAiPlayer,
            [PlayerControllerType.IaLevel4] = ReferenceAiPlayer
        };

    public static AutomatedDecisionResult ChooseCommand(KingOfTheHillGameState state, PlayerToken player)
    {
        if (!Players.TryGetValue(player.ControllerType, out var aiPlayer))
        {
            throw new InvalidOperationException(
                $"Player controller '{player.ControllerType}' is not a supported AI profile.");
        }

        return aiPlayer.ChooseCommand(state, player);
    }
}

internal interface IKingOfTheHillAiPlayer
{
    PlayerControllerType ControllerType { get; }

    AutomatedDecisionResult ChooseCommand(KingOfTheHillGameState state, PlayerToken player);
}

internal sealed record KingOfTheHillAiConfiguration(
    int SearchDepth,
    int TimeBudgetMilliseconds,
    double SecondChoiceProbability,
    int MaxCandidateCount,
    int ReservedMergeCandidates,
    int StrategicMergeRadius,
    double DistanceOneMergeProbability,
    double DistanceTwoMergeProbability);

internal enum DecisionFamily
{
    Objective,
    Siege,
    Defender,
    Survival,
    Merge,
    Tactical,
    Fallback
}

internal abstract class KingOfTheHillMinimaxAiPlayer : IKingOfTheHillAiPlayer
{
    protected abstract KingOfTheHillAiConfiguration Configuration { get; }

    public abstract PlayerControllerType ControllerType { get; }

    public AutomatedDecisionResult ChooseCommand(KingOfTheHillGameState state, PlayerToken player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);

        var stopwatch = Stopwatch.StartNew();
        var instrumentation = new SearchInstrumentation();
        var rankedEntries = KingOfTheHillAiMoveGenerator
            .GenerateLegalCommands(state)
            .Select(command => new PreviewedCommand(
                command,
                PreviewCommandScore(state, command, state.CurrentPlayerId)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Command.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        instrumentation.LegalCommandCount = rankedEntries.Length;

        if (rankedEntries.Length == 0)
        {
            stopwatch.Stop();
            return BuildDecisionResult(
                state,
                player,
                new PreviewedCommand(new GameCommand("pass"), int.MinValue, "KH-900", "No legal move fallback"),
                stopwatch.Elapsed.TotalMilliseconds,
                instrumentation);
        }

        instrumentation.CandidateCommandCount = rankedEntries.Length;
        instrumentation.NodesVisited = rankedEntries.Length;
        instrumentation.LeafEvaluations = rankedEntries.Length;

        var chosenCommand = ChooseRankedCommand(state, rankedEntries, instrumentation);
        if (chosenCommand is null)
        {
            chosenCommand = rankedEntries[0];
        }

        if (chosenCommand == rankedEntries[0] &&
            rankedEntries.Length > 1 &&
            Configuration.SecondChoiceProbability > 0 &&
            Random.Shared.NextDouble() < Configuration.SecondChoiceProbability)
        {
            chosenCommand = rankedEntries[1] with
            {
                DecisionRuleCode = "KH-210",
                DecisionRuleName = "Second-choice variance"
            };
        }

        stopwatch.Stop();
        return BuildDecisionResult(
            state,
            player,
            chosenCommand,
            stopwatch.Elapsed.TotalMilliseconds,
            instrumentation);
    }

    private static int Evaluate(KingOfTheHillGameState state, string maximizingPlayerId)
    {
        var minimizingPlayerId = state.Players.Single(player => player.Id != maximizingPlayerId).Id;

        if (state.IsCompleted)
        {
            if (string.Equals(state.WinnerPlayerId, maximizingPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return 1_000_000 - state.TurnNumber;
            }

            if (string.Equals(state.WinnerPlayerId, minimizingPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return -1_000_000 + state.TurnNumber;
            }
        }

        var score = 0;
        score += (state.ControlScores[maximizingPlayerId] - state.ControlScores[minimizingPlayerId]) * 12_000;
        score += EvaluateObjectiveControl(state, maximizingPlayerId);
        score -= EvaluateObjectiveControl(state, minimizingPlayerId);
        score += EvaluateObjectivePressure(state, maximizingPlayerId);
        score -= EvaluateObjectivePressure(state, minimizingPlayerId);
        score += EvaluateObjectiveDefenseUrgency(state, maximizingPlayerId);
        score -= EvaluateObjectiveDefenseUrgency(state, minimizingPlayerId);
        score += EvaluateCenterReadyArmy(state, maximizingPlayerId);
        score -= EvaluateCenterReadyArmy(state, minimizingPlayerId);

        return score;
    }

    private static int EvaluateObjectiveControl(KingOfTheHillGameState state, string playerId)
    {
        var unitOnObjective = state.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        if (unitOnObjective is null)
        {
            return 0;
        }

        return 18_000 + unitOnObjective.Strength * 3_000;
    }

    private static int EvaluateObjectivePressure(KingOfTheHillGameState state, string playerId)
    {
        var units = state.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pressure = 0;
        foreach (var unit in units)
        {
            if (state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            {
                pressure += 2_000 + unit.Strength * 260;
            }

            if (unit.Strength == 1 &&
                state.Board.GetReachableCoordinates(unit.Position, 2).Contains(HexCoordinate.Origin))
            {
                pressure += 1_500;
            }
            else if (unit.Strength > 1 &&
                     state.Board.GetReachableCoordinates(unit.Position, 1).Contains(HexCoordinate.Origin))
            {
                pressure += 1_200;
            }
        }

        return pressure;
    }

    private static int GetAdjacentObjectiveStrength(KingOfTheHillGameState state, string playerId) =>
        state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

    private static int GetObjectiveZoneDefenseStrength(KingOfTheHillGameState state, string playerId)
    {
        var unitOnObjective = state.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        return Math.Max(unitOnObjective?.Strength ?? 0, GetAdjacentObjectiveStrength(state, playerId));
    }

    private static int EvaluateCenterReadyArmy(KingOfTheHillGameState state, string playerId)
    {
        var units = state.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (units.Length == 0)
        {
            return -10_000;
        }

        var score = 0;
        foreach (var unit in units)
        {
            var distanceToCenter = unit.Position.DistanceTo(HexCoordinate.Origin);
            var centerProximity = (state.Board.Radius + 2 - distanceToCenter) * 220;
            var centerOccupation = unit.Position == HexCoordinate.Origin ? 16_000 + unit.Strength * 2_000 : 0;
            var adjacentPressure = state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin) ? 1_200 + unit.Strength * 160 : 0;
            var material = unit.Strength * 700;
            var mobility = EstimateMobility(state, unit) * 35;
            var attackPotential = EstimateAttackPotential(state, unit) * 240;
            var exposurePenalty = EstimateExposurePenalty(state, unit) * 220;
            var unsafeAdvancePenalty = EstimateUnsafeAdvancePenalty(state, unit) * 1_800;
            var centerReadyMerge = EstimateCenterReadyMergePotential(state, unit) * 1_200;

            score +=
                centerProximity +
                centerOccupation +
                adjacentPressure +
                material +
                mobility +
                attackPotential +
                centerReadyMerge -
                exposurePenalty -
                unsafeAdvancePenalty;
        }

        score += units.Max(unit => unit.Strength) * 260;
        score -= units.Count(unit => unit.Strength == 1 && unit.Position.DistanceTo(HexCoordinate.Origin) <= 2) * 110;

        return score;
    }

    private static int EvaluateObjectiveDefenseUrgency(KingOfTheHillGameState state, string playerId)
    {
        var opponentPlayerId = state.Players.Single(player => player.Id != playerId).Id;

        var friendlyUnits = state.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var enemyUnits = state.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, opponentPlayerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var friendlyOnObjective = friendlyUnits.SingleOrDefault(unit => unit.Position == HexCoordinate.Origin);
        var enemyOnObjective = enemyUnits.SingleOrDefault(unit => unit.Position == HexCoordinate.Origin);

        var score = 0;

        if (enemyOnObjective is not null)
        {
            var contestCapacity = friendlyUnits
                .Where(unit => CanThreatenObjectiveOnNextTurn(state, unit))
                .Max(unit => (int?)unit.Strength)
                ?? 0;
            var adjacentDefenseStrength = GetAdjacentObjectiveStrength(state, playerId);
            var zoneDefenseStrength = Math.Max(contestCapacity, adjacentDefenseStrength);

            score -= 22_000 + enemyOnObjective.Strength * 3_500;
            if (contestCapacity > enemyOnObjective.Strength)
            {
                score += 18_000;
            }
            else if (contestCapacity == enemyOnObjective.Strength)
            {
                score += 6_000;
            }
            else if (contestCapacity > 0)
            {
                score += contestCapacity * 1_200;
            }
            else
            {
                score -= 8_000;
            }

            if (adjacentDefenseStrength > 0)
            {
                score += adjacentDefenseStrength * 1_350;

                if (adjacentDefenseStrength > enemyOnObjective.Strength)
                {
                    score += 9_000;
                }
                else if (adjacentDefenseStrength == enemyOnObjective.Strength)
                {
                    score += 3_000;
                }
            }

            if (zoneDefenseStrength > enemyOnObjective.Strength)
            {
                score += 4_000;
            }
        }
        else if (friendlyOnObjective is not null)
        {
            var nearbyEnemyPressure = enemyUnits
                .Where(unit => state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
                .Sum(unit => unit.Strength);
            var nearbyFriendlySupport = friendlyUnits
                .Where(unit => unit.Id != friendlyOnObjective.Id && state.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
                .Sum(unit => unit.Strength);

            score += 8_000 + friendlyOnObjective.Strength * 1_800;
            score += nearbyFriendlySupport * 1_100;
            score -= nearbyEnemyPressure * 1_350;
        }
        else
        {
            var friendlyObjectiveAccess = friendlyUnits.Count(unit => CanThreatenObjectiveOnNextTurn(state, unit));
            var enemyObjectiveAccess = enemyUnits.Count(unit => CanThreatenObjectiveOnNextTurn(state, unit));

            score += friendlyObjectiveAccess * 1_600;
            score -= enemyObjectiveAccess * 1_900;
        }

        return score;
    }

    private static int ApplyPhaseBias(
        KingOfTheHillGameState state,
        DecisionFamily family,
        int baseScore)
    {
        if (baseScore <= 0)
        {
            return baseScore;
        }

        return baseScore + GetPhaseBias(GetMatchPhase(state), family);
    }

    private static int GetPhaseBias(MatchPhase phase, DecisionFamily family) =>
        phase switch
        {
            MatchPhase.Opening => family switch
            {
                DecisionFamily.Defender => 12_000,
                DecisionFamily.Siege => -4_000,
                DecisionFamily.Merge => -2_000,
                _ => 0
            },
            MatchPhase.Midgame => family switch
            {
                DecisionFamily.Objective => 6_000,
                DecisionFamily.Siege => 8_000,
                DecisionFamily.Merge => 4_000,
                DecisionFamily.Tactical => 2_000,
                _ => 0
            },
            MatchPhase.Endgame => family switch
            {
                DecisionFamily.Objective => 14_000,
                DecisionFamily.Siege => 6_000,
                DecisionFamily.Defender => -8_000,
                DecisionFamily.Survival => 4_000,
                DecisionFamily.Merge => 2_000,
                _ => 0
            },
            _ => 0
        };

    private PreviewedCommand? ChooseRankedCommand(
        KingOfTheHillGameState state,
        IReadOnlyList<PreviewedCommand> rankedEntries,
        SearchInstrumentation instrumentation)
    {
        rankedEntries = ApplyDefenderAdvanceRestrictions(state, rankedEntries);

        var currentObjectiveHolder = state.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        if (currentObjectiveHolder is not null)
        {
            var bestObjectiveReinforcementEntry = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Objective,
                        EvaluateObjectiveReinforcementScore(state, entry.Command, currentObjectiveHolder))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestObjectiveReinforcementEntry is not null)
            {
                return FindRankedEntry(rankedEntries, bestObjectiveReinforcementEntry.Command, "KH-010", "Objective reinforcement");
            }

            var bestObjectiveSupportApproachEntry = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Objective,
                        EvaluateObjectiveSupportApproachScore(state, entry.Command, currentObjectiveHolder))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestObjectiveSupportApproachEntry is not null)
            {
                return FindRankedEntry(rankedEntries, bestObjectiveSupportApproachEntry.Command, "KH-020", "Objective support approach");
            }

            var bestObjectiveEmergencyRetreatEntry = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Objective,
                        EvaluateObjectiveEmergencyRetreatScore(state, entry.Command, currentObjectiveHolder))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestObjectiveEmergencyRetreatEntry is not null)
            {
                return FindRankedEntry(rankedEntries, bestObjectiveEmergencyRetreatEntry.Command, "KH-030", "Objective emergency retreat");
            }
        }

        if (currentObjectiveHolder is not null &&
            !IsObjectiveHoldClearlyLost(state, currentObjectiveHolder))
        {
            var friendlyUnitCount = state.Units.Count(unit =>
                string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase));
            var passEntry = rankedEntries.FirstOrDefault(entry =>
                string.Equals(entry.Command.Name, "pass", StringComparison.OrdinalIgnoreCase));

            if (friendlyUnitCount == 1 && passEntry is not null)
            {
                return passEntry with { DecisionRuleCode = "KH-040", DecisionRuleName = "Preserve solo objective holder" };
            }

            var bestObjectiveHoldingEntry = rankedEntries.FirstOrDefault(entry =>
            {
                var result = KingOfTheHillGameRules.Execute(state, entry.Command);
                if (!result.Accepted)
                {
                    return false;
                }

                var nextState = (KingOfTheHillGameState)result.State;
                return nextState.Units.Any(unit =>
                    string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
                    unit.Position == HexCoordinate.Origin);
            });

            if (bestObjectiveHoldingEntry is not null)
            {
                return bestObjectiveHoldingEntry with { DecisionRuleCode = "KH-050", DecisionRuleName = "Keep holding Objective" };
            }
        }

        if (GetMatchPhase(state) == MatchPhase.Endgame)
        {
            var bestObjectiveEntry = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Objective,
                        EvaluateObjectiveEntryTimingScore(state, entry.Command))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestObjectiveEntry is not null)
            {
                return FindRankedEntry(rankedEntries, bestObjectiveEntry.Command, "KH-065", "Objective entry timing");
            }
        }

        var bestObjectiveOverrunSetup = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Objective,
                    EvaluateObjectiveOverrunSetupScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestObjectiveOverrunSetup is not null)
        {
            return FindRankedEntry(rankedEntries, bestObjectiveOverrunSetup.Command, "KH-070", "Objective assault posture");
        }

        if (Configuration.SearchDepth > 1)
        {
            var bestObjectiveReserveMobilization = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Objective,
                        EvaluateObjectiveReserveMobilizationScore(state, entry.Command))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestObjectiveReserveMobilization is not null)
            {
                return FindRankedEntry(rankedEntries, bestObjectiveReserveMobilization.Command, "KH-080", "Objective reserve mobilization");
            }
        }

        var bestDefenderIntercept = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Defender,
                    EvaluateDefenderInterceptScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestDefenderIntercept is not null)
        {
            return FindRankedEntry(rankedEntries, bestDefenderIntercept.Command, "KH-085", "Defender intercept");
        }

        var bestDefenderLaneDenial = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Defender,
                    EvaluateDefenderLaneDenialScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestDefenderLaneDenial is not null)
        {
            return FindRankedEntry(rankedEntries, bestDefenderLaneDenial.Command, "KH-088", "Defender lane denial");
        }

        var bestSiegeSearchEntry = ChooseObjectiveSiegeSearchCommand(state, rankedEntries, instrumentation);
        if (bestSiegeSearchEntry is not null)
        {
            return bestSiegeSearchEntry with { DecisionRuleCode = "KH-090", DecisionRuleName = "Objective siege search" };
        }

        var bestObjectiveBreakthroughApproach = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Siege,
                    EvaluateObjectiveBreakthroughApproachScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestObjectiveBreakthroughApproach is not null)
        {
            return FindRankedEntry(rankedEntries, bestObjectiveBreakthroughApproach.Command, "KH-100", "Objective breakthrough approach");
        }

        if (ControllerType == PlayerControllerType.IaLevel4)
        {
            var bestLevelFourSiegeApproach = rankedEntries
                .Select(entry => new ScoredCommand(entry.Command, EvaluateLevelFourSiegeApproachScore(state, entry.Command)))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestLevelFourSiegeApproach is not null)
            {
                return FindRankedEntry(rankedEntries, bestLevelFourSiegeApproach.Command, "KH-110", "IA4 strong siege approach");
            }
        }

        var bestObjectiveSiegeMerge = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Siege,
                    EvaluateObjectiveSiegeMergeScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestObjectiveSiegeMerge is not null)
        {
            return FindRankedEntry(rankedEntries, bestObjectiveSiegeMerge.Command, "KH-120", "Objective siege merge");
        }

        var bestObjectiveSiegeApproach = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Siege,
                    EvaluateObjectiveSiegeApproachScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestObjectiveSiegeApproach is not null)
        {
            return FindRankedEntry(rankedEntries, bestObjectiveSiegeApproach.Command, "KH-130", "Objective siege approach");
        }

        if (ControllerType == PlayerControllerType.IaLevel4)
        {
            var bestDefenderReset = rankedEntries
                .Select(entry => new ScoredCommand(
                    entry.Command,
                    ApplyPhaseBias(
                        state,
                        DecisionFamily.Defender,
                        EvaluateDefenderResetScore(state, entry.Command))))
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestDefenderReset is not null)
            {
                return FindRankedEntry(rankedEntries, bestDefenderReset.Command, "KH-055", "IA4 defender reset");
            }
        }

        var bestSurvivalRetreat = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Survival,
                    EvaluateSurvivalRetreatScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestSurvivalRetreat is not null)
        {
            return FindRankedEntry(rankedEntries, bestSurvivalRetreat.Command, "KH-140", "Survival retreat");
        }

        var bestInnerKill = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Tactical,
                    EvaluateKillSelectionScore(state, entry.Command, innerOrSameRing: true))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestInnerKill is not null)
        {
            return FindRankedEntry(rankedEntries, bestInnerKill.Command, "KH-150", "Inner-ring kill");
        }

        var bestForcedInnerThreat = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Tactical,
                    EvaluateForcedInnerThreatScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestForcedInnerThreat is not null)
        {
            return FindRankedEntry(rankedEntries, bestForcedInnerThreat.Command, "KH-160", "Forced inner threat");
        }

        var bestDefensiveMerge = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Merge,
                    EvaluateDefensiveMergeScore(state, entry.Command))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestDefensiveMerge is not null)
        {
            return FindRankedEntry(rankedEntries, bestDefensiveMerge.Command, "KH-170", "Defensive merge");
        }

        var bestOuterSafeKill = rankedEntries
            .Select(entry => new ScoredCommand(
                entry.Command,
                ApplyPhaseBias(
                    state,
                    DecisionFamily.Tactical,
                    EvaluateKillSelectionScore(state, entry.Command, innerOrSameRing: false))))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestOuterSafeKill is not null)
        {
            return FindRankedEntry(rankedEntries, bestOuterSafeKill.Command, "KH-180", "Outer safe kill");
        }

        var bestDistanceOneMerge = rankedEntries
            .FirstOrDefault(entry => EvaluateMergeOpportunity(state, entry.Command) == MergeOpportunity.DistanceOneFavorable);

        if (bestDistanceOneMerge is not null &&
            Configuration.DistanceOneMergeProbability > 0 &&
            Random.Shared.NextDouble() < Configuration.DistanceOneMergeProbability)
        {
            return bestDistanceOneMerge with { DecisionRuleCode = "KH-190", DecisionRuleName = "Distance-1 favorable merge" };
        }

        var bestDistanceTwoMerge = rankedEntries
            .FirstOrDefault(entry => EvaluateMergeOpportunity(state, entry.Command) == MergeOpportunity.DistanceTwoFavorable);

        if (bestDistanceTwoMerge is not null &&
            Configuration.DistanceTwoMergeProbability > 0 &&
            Random.Shared.NextDouble() < Configuration.DistanceTwoMergeProbability)
        {
            return bestDistanceTwoMerge with { DecisionRuleCode = "KH-200", DecisionRuleName = "Distance-2 favorable merge" };
        }

        return rankedEntries[0] with { DecisionRuleCode = "KH-220", DecisionRuleName = "Ranked fallback" };
    }

    private PreviewedCommand? ChooseObjectiveSiegeSearchCommand(
        KingOfTheHillGameState state,
        IReadOnlyList<PreviewedCommand> rankedEntries,
        SearchInstrumentation instrumentation)
    {
        if (Configuration.SearchDepth <= 1 ||
            !NeedsObjectiveSiege(state, out _, out _))
        {
            return null;
        }

        var rootCandidates = rankedEntries
            .Take(Math.Clamp(Configuration.MaxCandidateCount, 1, rankedEntries.Count))
            .ToArray();

        if (rootCandidates.Length == 0)
        {
            return null;
        }

        var bestEntry = default(PreviewedCommand?);
        var bestScore = int.MinValue;
        var alpha = int.MinValue;
        var beta = int.MaxValue;

        foreach (var candidate in rootCandidates)
        {
            var result = KingOfTheHillGameRules.Execute(state, candidate.Command);
            if (!result.Accepted)
            {
                continue;
            }

            var nextState = (KingOfTheHillGameState)result.State;
            var score = SearchObjectiveSiegeValue(
                nextState,
                state.CurrentPlayerId,
                Configuration.SearchDepth - 1,
                alpha,
                beta,
                instrumentation);

            if (score > bestScore)
            {
                bestScore = score;
                bestEntry = candidate with { Score = score };
            }

            alpha = Math.Max(alpha, bestScore);
        }

        return bestEntry;
    }

    private int SearchObjectiveSiegeValue(
        KingOfTheHillGameState state,
        string maximizingPlayerId,
        int depth,
        int alpha,
        int beta,
        SearchInstrumentation instrumentation)
    {
        if (depth <= 0 || state.IsCompleted)
        {
            instrumentation.LeafEvaluations++;
            return Evaluate(state, maximizingPlayerId) + EvaluateSiegeProjection(state, maximizingPlayerId);
        }

        var rankedCommands = KingOfTheHillAiMoveGenerator
            .GenerateLegalCommands(state)
            .Select(command => new PreviewedCommand(
                command,
                PreviewCommandScore(state, command, maximizingPlayerId)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Command.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => FormatCommand(entry.Command), StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(Configuration.MaxCandidateCount, 1, int.MaxValue))
            .ToArray();

        if (rankedCommands.Length == 0)
        {
            instrumentation.LeafEvaluations++;
            return Evaluate(state, maximizingPlayerId) + EvaluateSiegeProjection(state, maximizingPlayerId);
        }

        instrumentation.CandidateCommandCount += rankedCommands.Length;
        instrumentation.NodesVisited += rankedCommands.Length;

        var maximizingTurn = string.Equals(state.CurrentPlayerId, maximizingPlayerId, StringComparison.OrdinalIgnoreCase);

        if (maximizingTurn)
        {
            var bestScore = int.MinValue;

            foreach (var candidate in rankedCommands)
            {
                var result = KingOfTheHillGameRules.Execute(state, candidate.Command);
                if (!result.Accepted)
                {
                    continue;
                }

                var nextState = (KingOfTheHillGameState)result.State;
                var score = SearchObjectiveSiegeValue(
                    nextState,
                    maximizingPlayerId,
                    depth - 1,
                    alpha,
                    beta,
                    instrumentation);

                bestScore = Math.Max(bestScore, score);
                alpha = Math.Max(alpha, bestScore);
                if (beta <= alpha)
                {
                    break;
                }
            }

            return bestScore == int.MinValue
                ? Evaluate(state, maximizingPlayerId) + EvaluateSiegeProjection(state, maximizingPlayerId)
                : bestScore;
        }

        var worstScore = int.MaxValue;

        foreach (var candidate in rankedCommands)
        {
            var result = KingOfTheHillGameRules.Execute(state, candidate.Command);
            if (!result.Accepted)
            {
                continue;
            }

            var nextState = (KingOfTheHillGameState)result.State;
            var score = SearchObjectiveSiegeValue(
                nextState,
                maximizingPlayerId,
                depth - 1,
                alpha,
                beta,
                instrumentation);

            worstScore = Math.Min(worstScore, score);
            beta = Math.Min(beta, worstScore);
            if (beta <= alpha)
            {
                break;
            }
        }

        return worstScore == int.MaxValue
            ? Evaluate(state, maximizingPlayerId) + EvaluateSiegeProjection(state, maximizingPlayerId)
            : worstScore;
    }

    private static int PreviewCommandScore(
        KingOfTheHillGameState state,
        GameCommand command,
        string maximizingPlayerId)
    {
        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return int.MinValue;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        return Evaluate(nextState, maximizingPlayerId) + EvaluateImmediateCommandBias(state, nextState, command);
    }

    private static int EstimateMobility(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var movementDepth = unit.Strength == 1 ? 2 : 1;
        return state.Board.GetReachableCoordinates(unit.Position, movementDepth)
            .Count(target => KingOfTheHillGameRules.Execute(state, CreateMoveCommand(unit.Id, target)).Accepted);
    }

    private static int EstimateAttackPotential(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var movementDepth = unit.Strength == 1 ? 2 : 1;
        var reachableTargets = state.Board.GetReachableCoordinates(unit.Position, movementDepth);

        return reachableTargets
            .Select(state.FindUnitAt)
            .Where(target => target is not null && target.OwnerPlayerId != unit.OwnerPlayerId && unit.Strength > target.Strength)
            .Sum(target => 1 + ((target!.Strength + 1) * 2));
    }

    private static int EstimateCenterReadyMergePotential(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        if (unit.Strength > 1)
        {
            return 0;
        }

        var reachableTargets = state.Board.GetReachableCoordinates(unit.Position, 2)
            .Select(state.FindUnitAt)
            .Where(target => target is not null && target.OwnerPlayerId == unit.OwnerPlayerId)
            .Cast<KingOfTheHillUnitState>()
            .ToArray();

        var totalScore = 0;
        foreach (var friendlyTarget in reachableTargets)
        {
            var mergedStrength = unit.Strength + friendlyTarget.Strength;
            if (mergedStrength > KingOfTheHillGameState.MaximumBlockStrength)
            {
                continue;
            }

            var mergedPosition = friendlyTarget.Position;

            if (mergedPosition.DistanceTo(HexCoordinate.Origin) > 2)
            {
                continue;
            }

            var canReachObjectiveNextTurn = mergedPosition == HexCoordinate.Origin ||
                state.Board.AreAdjacent(mergedPosition, HexCoordinate.Origin);
            var canContestNearbyEnemy = state.Units.Any(enemy =>
                enemy.OwnerPlayerId != unit.OwnerPlayerId &&
                enemy.Position.DistanceTo(HexCoordinate.Origin) <= 2 &&
                state.Board.AreAdjacent(enemy.Position, mergedPosition) &&
                mergedStrength > enemy.Strength);

            if (!canReachObjectiveNextTurn && !canContestNearbyEnemy)
            {
                continue;
            }

            totalScore += mergedStrength * 2;

            if (canReachObjectiveNextTurn)
            {
                totalScore += 8;
            }

            if (canContestNearbyEnemy)
            {
                totalScore += 6;
            }
        }

        return totalScore;
    }

    private static int EstimateExposurePenalty(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var adjacentEnemyStrength = state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position))
            .Sum(other => other.Strength);

        return Math.Max(0, adjacentEnemyStrength - unit.Strength);
    }

    private static int EstimateUnsafeAdvancePenalty(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var distanceToCenter = unit.Position.DistanceTo(HexCoordinate.Origin);
        if (distanceToCenter > 2)
        {
            return 0;
        }

        var strongestAdjacentEnemy = state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position))
            .Max(other => (int?)other.Strength) ?? 0;

        if (strongestAdjacentEnemy <= unit.Strength)
        {
            return 0;
        }

        var friendlyAdjacentSupport = state.Units
            .Where(other =>
                other.Id != unit.Id &&
                other.OwnerPlayerId == unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position))
            .Sum(other => other.Strength);

        var enemyAdjacentPressure = state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position))
            .Sum(other => other.Strength);

        var isOnObjective = unit.Position == HexCoordinate.Origin;
        var isObjectiveRing = distanceToCenter == 1;

        var penalty = strongestAdjacentEnemy - unit.Strength;

        if (friendlyAdjacentSupport < enemyAdjacentPressure)
        {
            penalty += enemyAdjacentPressure - friendlyAdjacentSupport;
        }

        if (isOnObjective)
        {
            penalty += 3;
        }
        else if (isObjectiveRing)
        {
            penalty += 2;
        }

        return penalty;
    }

    private static bool IsObjectiveHoldClearlyLost(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState objectiveUnit)
    {
        if (objectiveUnit.Position != HexCoordinate.Origin)
        {
            return false;
        }

        var adjacentEnemies = state.Units
            .Where(other =>
                other.OwnerPlayerId != objectiveUnit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, HexCoordinate.Origin))
            .ToArray();

        var strongestAdjacentEnemy = adjacentEnemies.Max(other => (int?)other.Strength) ?? 0;
        var combinedAdjacentEnemyStrength = adjacentEnemies.Sum(other => other.Strength);

        if (strongestAdjacentEnemy > objectiveUnit.Strength)
        {
            return true;
        }

        if (combinedAdjacentEnemyStrength > objectiveUnit.Strength)
        {
            return true;
        }

        var enemyThreatNextTurn = state.Units
            .Where(other => other.OwnerPlayerId != objectiveUnit.OwnerPlayerId)
            .Any(other =>
                other.Strength > objectiveUnit.Strength &&
                CanThreatenObjectiveOnNextTurn(state, other));

        if (!enemyThreatNextTurn)
        {
            return false;
        }

        var friendlyAdjacentSupport = state.Units
            .Where(other =>
                other.Id != objectiveUnit.Id &&
                other.OwnerPlayerId == objectiveUnit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, HexCoordinate.Origin))
            .Sum(other => other.Strength);

        var enemyAdjacentPressure = combinedAdjacentEnemyStrength;

        var zoneDefenseStrength = Math.Max(objectiveUnit.Strength, friendlyAdjacentSupport);
        return enemyAdjacentPressure > objectiveUnit.Strength + friendlyAdjacentSupport &&
               enemyAdjacentPressure > zoneDefenseStrength;
    }

    private static bool CanThreatenObjectiveOnNextTurn(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var movementDepth = unit.Strength == 1 ? 2 : 1;
        return state.Board.GetReachableCoordinates(unit.Position, movementDepth).Contains(HexCoordinate.Origin);
    }

    private static bool CanThreatenObjectiveWithinTurns(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState unit,
        int turnCount)
    {
        if (turnCount <= 1)
        {
            return CanThreatenObjectiveOnNextTurn(state, unit);
        }

        var currentFrontier = new HashSet<HexCoordinate> { unit.Position };
        var visited = new HashSet<HexCoordinate> { unit.Position };

        for (var turn = 0; turn < turnCount; turn++)
        {
            var nextFrontier = new HashSet<HexCoordinate>();

            foreach (var position in currentFrontier)
            {
                var depth = unit.Strength == 1 ? 2 : 1;
                foreach (var reachable in state.Board.GetReachableCoordinates(position, depth))
                {
                    if (reachable == HexCoordinate.Origin)
                    {
                        return true;
                    }

                    if (visited.Add(reachable))
                    {
                        nextFrontier.Add(reachable);
                    }
                }
            }

            currentFrontier = nextFrontier;
            if (currentFrontier.Count == 0)
            {
                break;
            }
        }

        return false;
    }

    private static int EvaluateImmediateCommandBias(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command)
    {
        var classification = ClassifyCommand(previousState, command);
        var currentPlayerId = previousState.CurrentPlayerId;
        var leaveObjectiveBias = EvaluateLeaveObjectiveBias(previousState, nextState, command, currentPlayerId);

        if (leaveObjectiveBias != 0)
        {
            return leaveObjectiveBias;
        }

        return classification switch
        {
            CandidateClassification.Objective => 42_000,
            CandidateClassification.KillInnerOrSameRing => EvaluateKillCommandBias(previousState, nextState, command, currentPlayerId, true),
            CandidateClassification.KillOuterSafe => EvaluateKillCommandBias(previousState, nextState, command, currentPlayerId, false),
            CandidateClassification.MergeTowardObjective => EvaluateMergeCommandBias(previousState, nextState, command, currentPlayerId, nearObjective: true),
            CandidateClassification.Merge => EvaluateMergeCommandBias(previousState, nextState, command, currentPlayerId, nearObjective: false),
            _ => EvaluatePositionalCommandBias(previousState, nextState, command, currentPlayerId)
        };
    }

    private static int EvaluateLeaveObjectiveBias(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command,
        string playerId)
    {
        if (!TryGetMoveContext(previousState, nextState, command, playerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        if (sourceUnit.Position != HexCoordinate.Origin || movedUnit.Position == HexCoordinate.Origin)
        {
            return 0;
        }

        return IsObjectiveHoldClearlyLost(previousState, sourceUnit)
            ? -8_000
            : -60_000;
    }

    private static int EvaluateKillCommandBias(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command,
        string playerId,
        bool innerOrSameRing)
    {
        if (command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return 0;
        }

        var target = new HexCoordinate(q, r);
        var attackingUnit = nextState.FindUnitAt(target);
        if (attackingUnit is null || attackingUnit.OwnerPlayerId != playerId)
        {
            return 0;
        }

        var sourceUnit = previousState.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null)
        {
            return 0;
        }

        var bias = innerOrSameRing ? 26_000 : 12_000;

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = target.DistanceTo(HexCoordinate.Origin);
        if (targetDistance == 0)
        {
            bias += 12_000;
        }
        else if (targetDistance == 1)
        {
            bias += 8_000;
        }
        else if (targetDistance == 2)
        {
            bias += 3_000;
        }

        if (IsDefenderUnit(previousState, sourceUnit, playerId) &&
            sourceDistance == 2 &&
            targetDistance == 2)
        {
            bias += 18_000;
        }

        bias += attackingUnit.Strength * 1_500;

        if (CanThreatenObjectiveOnNextTurn(nextState, attackingUnit))
        {
            bias += 5_000;
        }

        bias -= EvaluateImmediateRecapturePenalty(nextState, attackingUnit, sourceDistance, targetDistance);

        return bias;
    }

    private static int EvaluateKillSelectionScore(
        KingOfTheHillGameState state,
        GameCommand command,
        bool innerOrSameRing)
    {
        var expectedOpportunity = innerOrSameRing
            ? KillOpportunity.InnerOrSameRingFavorable
            : KillOpportunity.OuterRingFavorable;

        if (EvaluateKillOpportunity(state, command) != expectedOpportunity)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out _, out var movedUnit))
        {
            return 0;
        }

        var sourceDistance = state.FindUnit(command.GetRequiredArgument("unitId"))?.Position.DistanceTo(HexCoordinate.Origin) ?? movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance == 1 && targetDistance == 1)
        {
            if (HasAdjacentEnemyStrengthAtLeast(nextState, movedUnit, 4))
            {
                return 0;
            }

            return 120_000 + movedUnit.Strength * 2_000;
        }

        return EvaluateKillCommandBias(state, nextState, command, state.CurrentPlayerId, innerOrSameRing);
    }

    private static int EvaluateMergeCommandBias(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command,
        string playerId,
        bool nearObjective)
    {
        if (command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return 0;
        }

        var previousUnits = previousState.Units
            .Where(unit => unit.OwnerPlayerId == playerId)
            .ToArray();
        var nextUnits = nextState.Units
            .Where(unit => unit.OwnerPlayerId == playerId)
            .ToArray();

        var previousStrongest = previousUnits.Max(unit => unit.Strength);
        var nextStrongest = nextUnits.Max(unit => unit.Strength);
        var strongestGain = Math.Max(0, nextStrongest - previousStrongest);

        var mergedPosition = new HexCoordinate(q, r);
        var mergedUnit = nextState.FindUnitAt(mergedPosition);
        if (mergedUnit is null || mergedUnit.OwnerPlayerId != playerId)
        {
            return 0;
        }

        var sourceUnit = previousState.FindUnit(command.GetRequiredArgument("unitId"));
        var mergeTargetUnit = previousState.FindUnitAt(mergedPosition);
        if ((sourceUnit is not null && IsDefenderUnit(previousState, sourceUnit, playerId)) ||
            (mergeTargetUnit is not null && IsDefenderUnit(previousState, mergeTargetUnit, playerId)))
        {
            return -60_000;
        }

        if (IsDefenderToDefenderMerge(previousState, command, playerId))
        {
            return -42_000;
        }

        var opportunity = EvaluateMergeOpportunity(previousState, command);
        var bias = opportunity switch
        {
            MergeOpportunity.DistanceOneFavorable => 22_000,
            MergeOpportunity.DistanceTwoFavorable => 12_000,
            MergeOpportunity.DistanceThreeOrMore => -30_000,
            _ => 2_000
        };

        bias += strongestGain * 2_500 + mergedUnit.Strength * 1_500;

        if (nearObjective)
        {
            bias += 4_000;
        }

        if (mergedUnit.Position == HexCoordinate.Origin)
        {
            bias += 10_000;
        }
        else if (previousState.Board.AreAdjacent(mergedUnit.Position, HexCoordinate.Origin))
        {
            bias += 4_500;
        }
        else if (CanThreatenObjectiveOnNextTurn(nextState, mergedUnit))
        {
            bias += 3_000;
        }

        var canAttackNearbyEnemy = nextState.Units.Any(enemy =>
            enemy.OwnerPlayerId != playerId &&
            enemy.Position.DistanceTo(HexCoordinate.Origin) <= 2 &&
            nextState.Board.AreAdjacent(enemy.Position, mergedUnit.Position) &&
            mergedUnit.Strength > enemy.Strength);

        if (canAttackNearbyEnemy)
        {
            bias += 4_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, mergedUnit, 2))
        {
            bias += 4_000;
        }

        return bias;
    }

    private static int EvaluatePassBias(KingOfTheHillGameState state, GameCommand command)
    {
        if (!string.Equals(command.Name, "pass", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var currentPlayerUnitOnObjective = state.Units.SingleOrDefault(unit =>
            unit.OwnerPlayerId == state.CurrentPlayerId &&
            unit.Position == HexCoordinate.Origin);

        if (currentPlayerUnitOnObjective is null)
        {
            return -7_500;
        }

        var friendlyUnitCount = state.Units.Count(unit => unit.OwnerPlayerId == state.CurrentPlayerId);
        if (!IsObjectiveHoldClearlyLost(state, currentPlayerUnitOnObjective))
        {
            return friendlyUnitCount == 1 ? 48_000 : 18_000;
        }

        return 2_000;
    }

    private static int EvaluatePositionalCommandBias(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command,
        string playerId)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluatePassBias(previousState, command);
        }

        if (!TryGetMoveContext(previousState, nextState, command, playerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        var unsafeAdvancePenalty = EstimateUnsafeAdvancePenalty(nextState, movedUnit);
        var unjustifiedRetreatPenalty = EvaluateUnjustifiedInnerRetreatPenalty(previousState, sourceUnit, targetDistance);
        var immediateRecapturePenalty = EvaluateImmediateRecapturePenalty(nextState, movedUnit, sourceDistance, targetDistance);
        var defensiveAnchorAdvancePenalty = EvaluateDefenderAdvancePenalty(previousState, sourceUnit, movedUnit);

        if (defensiveAnchorAdvancePenalty != 0)
        {
            return defensiveAnchorAdvancePenalty;
        }

        if (targetDistance < sourceDistance && (unsafeAdvancePenalty > 0 || immediateRecapturePenalty > 0))
        {
            var bias = -14_000 - unsafeAdvancePenalty * 1_200 - immediateRecapturePenalty;
            if (targetDistance <= 1)
            {
                bias -= 6_000;
            }

            return bias;
        }

        return -unjustifiedRetreatPenalty;
    }

    private static CandidateClassification ClassifyCommand(KingOfTheHillGameState state, GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return CandidateClassification.Other;
        }

        if (command.Arguments is null ||
            !command.Arguments.TryGetValue("unitId", out var unitId) ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return CandidateClassification.Other;
        }

        var unit = state.FindUnit(unitId);
        if (unit is null)
        {
            return CandidateClassification.Other;
        }

        var target = new HexCoordinate(q, r);
        var targetUnit = state.FindUnitAt(target);

        if (target == HexCoordinate.Origin)
        {
            return CandidateClassification.Objective;
        }

        if (targetUnit is not null && targetUnit.OwnerPlayerId != unit.OwnerPlayerId)
        {
            return EvaluateKillOpportunity(state, command) switch
            {
                KillOpportunity.InnerOrSameRingFavorable => CandidateClassification.KillInnerOrSameRing,
                KillOpportunity.OuterRingFavorable => CandidateClassification.KillOuterSafe,
                _ => CandidateClassification.Other
            };
        }

        if (targetUnit is not null && targetUnit.OwnerPlayerId == unit.OwnerPlayerId)
        {
            return target.DistanceTo(HexCoordinate.Origin) <= 3
                ? CandidateClassification.MergeTowardObjective
                : CandidateClassification.Merge;
        }

        return CandidateClassification.Other;
    }

    private static bool TryGetMoveContext(
        KingOfTheHillGameState previousState,
        KingOfTheHillGameState nextState,
        GameCommand command,
        string playerId,
        out KingOfTheHillUnitState sourceUnit,
        out KingOfTheHillUnitState movedUnit)
    {
        sourceUnit = null!;
        movedUnit = null!;

        if (command.Arguments is null ||
            !command.Arguments.TryGetValue("unitId", out var unitId) ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return false;
        }

        sourceUnit = previousState.Units.SingleOrDefault(unit =>
            string.Equals(unit.Id, unitId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))!;

        if (sourceUnit is null)
        {
            return false;
        }

        var target = new HexCoordinate(q, r);
        movedUnit = nextState.FindUnitAt(target);

        return movedUnit is not null &&
               string.Equals(movedUnit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
    }

    private static MergeOpportunity EvaluateMergeOpportunity(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (ClassifyCommand(state, command) is not CandidateClassification.Merge and not CandidateClassification.MergeTowardObjective)
        {
            return MergeOpportunity.NotAMerge;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null ||
            command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return MergeOpportunity.NotAMerge;
        }

        var target = new HexCoordinate(q, r);
        var targetUnit = state.FindUnitAt(target);
        if (targetUnit is null || targetUnit.OwnerPlayerId != sourceUnit.OwnerPlayerId)
        {
            return MergeOpportunity.NotAMerge;
        }

        var mergedStrength = sourceUnit.Strength + targetUnit.Strength;
        if (mergedStrength > KingOfTheHillGameState.MaximumBlockStrength)
        {
            return MergeOpportunity.NotFavorable;
        }

        var centerDistance = target.DistanceTo(HexCoordinate.Origin);

        if (centerDistance >= 3)
        {
            return MergeOpportunity.DistanceThreeOrMore;
        }

        var enemyUnits = state.Units
            .Where(unit => unit.OwnerPlayerId != sourceUnit.OwnerPlayerId)
            .ToArray();

        var threateningEnemies = enemyUnits
            .Where(enemy =>
                enemy.Position.DistanceTo(HexCoordinate.Origin) <= 1 ||
                CanReachDistanceOneRingNextTurn(state, enemy))
            .ToArray();

        var threatensRelevantEnemy = threateningEnemies.Any(enemy =>
            state.Board.AreAdjacent(target, enemy.Position) &&
            mergedStrength > enemy.Strength);

        if (centerDistance == 1 && threatensRelevantEnemy)
        {
            return MergeOpportunity.DistanceOneFavorable;
        }

        if (centerDistance == 2 && threatensRelevantEnemy)
        {
            return MergeOpportunity.DistanceTwoFavorable;
        }

        return MergeOpportunity.NotFavorable;
    }

    private static int EvaluateObjectiveSiegeMergeScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out var currentContestCapacity) ||
            ClassifyCommand(state, command) is not CandidateClassification.Merge and not CandidateClassification.MergeTowardObjective)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var mergedUnit))
        {
            return 0;
        }

        var targetDistance = mergedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance > 3)
        {
            return 0;
        }

        var alreadyHasWinningContestStrength = currentContestCapacity > enemyOnObjective.Strength;
        if (alreadyHasWinningContestStrength)
        {
            var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
            var mergeImprovesArrival = targetDistance < sourceDistance && mergedUnit.Strength > enemyOnObjective.Strength;
            if (!mergeImprovesArrival)
            {
                return 0;
            }
        }

        var nextContestCapacity = GetContestCapacity(nextState, state.CurrentPlayerId);
        if (nextContestCapacity <= currentContestCapacity && mergedUnit.Strength <= currentContestCapacity)
        {
            return 0;
        }

        var score = 28_000;
        score += (nextContestCapacity - currentContestCapacity) * 8_000;
        score += mergedUnit.Strength * 2_800;

        if (mergedUnit.Strength > enemyOnObjective.Strength)
        {
            score += 24_000;
        }
        else if (mergedUnit.Strength == enemyOnObjective.Strength)
        {
            score += 8_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, mergedUnit, 2))
        {
            score += 10_000;
        }

        if (targetDistance <= 2)
        {
            score += 8_000;
        }
        else if (targetDistance == 3)
        {
            score += 2_000;
        }

        score -= EvaluateExcessiveSiegeMergePenalty(
            state,
            nextState,
            sourceUnit,
            mergedUnit,
            enemyOnObjective,
            currentContestCapacity);

        return score;
    }

    private static int EvaluateObjectiveOverrunSetupScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out _) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null ||
            command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return 0;
        }

        var target = new HexCoordinate(q, r);
        var movedUnit = nextState.FindUnitAt(target);
        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = target.DistanceTo(HexCoordinate.Origin);

        if (sourceUnit.Position == HexCoordinate.Origin || targetDistance > 2)
        {
            return 0;
        }

        var currentAdjacentStrength = GetAdjacentObjectiveStrength(state, state.CurrentPlayerId);
        var nextAdjacentStrength = GetAdjacentObjectiveStrength(nextState, state.CurrentPlayerId);
        var currentDeficit = enemyOnObjective.Strength + 1 - currentAdjacentStrength;
        var nextObjectiveHolder = nextState.FindUnitAt(HexCoordinate.Origin);

        if (nextObjectiveHolder is not null &&
            string.Equals(nextObjectiveHolder.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            var score = 90_000;
            score += nextObjectiveHolder.Strength * 10_000;
            score += sourceUnit.Strength * 3_000;

            if (targetDistance == 1)
            {
                score += 18_000;
            }

            return score;
        }

        var nextEnemyObjectiveHolder = nextState.Units.SingleOrDefault(unit =>
            unit.Position == HexCoordinate.Origin &&
            !string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase));

        if (nextEnemyObjectiveHolder is null)
        {
            return 0;
        }

        var nextDeficit = nextEnemyObjectiveHolder.Strength + 1 - nextAdjacentStrength;
        if (nextAdjacentStrength <= currentAdjacentStrength && nextDeficit >= currentDeficit)
        {
            return 0;
        }

        var scoreBase = 18_000;
        scoreBase += Math.Max(0, currentDeficit - nextDeficit) * 14_000;
        scoreBase += Math.Max(0, nextAdjacentStrength - currentAdjacentStrength) * 6_500;
        scoreBase += (movedUnit?.Strength ?? sourceUnit.Strength) * 2_500;

        if (targetDistance == 1)
        {
            scoreBase += 20_000;
        }
        else if (targetDistance == 2)
        {
            scoreBase += 4_000;
        }

        if (sourceDistance > targetDistance)
        {
            scoreBase += 7_000;
        }

        if (nextDeficit == 1)
        {
            scoreBase += 16_000;
        }
        else if (nextDeficit == 2)
        {
            scoreBase += 8_000;
        }

        scoreBase -= EvaluateImmediateRecapturePenalty(nextState, movedUnit, sourceDistance, targetDistance);
        return scoreBase;
    }

    private static int EvaluateObjectiveEntryTimingScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (GetMatchPhase(state) != MatchPhase.Endgame ||
            ClassifyCommand(state, command) != CandidateClassification.Objective ||
            ShouldSuppressDefenderObjectiveEntry(state, command) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        var objectiveHolder = nextState.FindUnitAt(HexCoordinate.Origin);
        if (objectiveHolder is null ||
            !string.Equals(objectiveHolder.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var currentPlayerId = state.CurrentPlayerId;
        var opponentPlayerId = state.Players
            .Select(player => player.Id)
            .First(playerId => !string.Equals(playerId, currentPlayerId, StringComparison.OrdinalIgnoreCase));

        if (sourceUnit.Strength == 1 &&
            HasStrongerInwardApproachAvailable(state, sourceUnit.OwnerPlayerId, sourceUnit.Id))
        {
            return 0;
        }

        var opponentRemainingStrength = nextState.Units
            .Where(unit => string.Equals(unit.OwnerPlayerId, opponentPlayerId, StringComparison.OrdinalIgnoreCase))
            .Sum(unit => unit.Strength);

        var enemyAdjacentStrength = nextState.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, opponentPlayerId, StringComparison.OrdinalIgnoreCase) &&
                nextState.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        var friendlyAdjacentStrength = nextState.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, currentPlayerId, StringComparison.OrdinalIgnoreCase) &&
                unit.Id != objectiveHolder.Id &&
                nextState.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        var immediateRecapturePenalty = EvaluateImmediateRecapturePenalty(nextState, objectiveHolder, sourceDistance, 0);
        var cannotBeExceeded = opponentRemainingStrength <= objectiveHolder.Strength;
        var holdsInnerAdvantage = friendlyAdjacentStrength >= enemyAdjacentStrength;
        var dominatesInnerRing = friendlyAdjacentStrength > enemyAdjacentStrength;
        var objectiveIsExposed = enemyAdjacentStrength >= objectiveHolder.Strength;

        if (!cannotBeExceeded &&
            friendlyAdjacentStrength == 0 &&
            immediateRecapturePenalty > 0)
        {
            return 0;
        }

        if (!cannotBeExceeded && !holdsInnerAdvantage)
        {
            return 0;
        }

        if (!cannotBeExceeded && objectiveIsExposed && !dominatesInnerRing)
        {
            return 0;
        }

        if (!cannotBeExceeded && immediateRecapturePenalty > 0 && !dominatesInnerRing)
        {
            return 0;
        }

        var score = 30_000;
        score += objectiveHolder.Strength * 9_000;
        score += sourceUnit.Strength * 3_000;

        if (sourceDistance == 1)
        {
            score += 12_000;
        }
        else if (sourceDistance == 2)
        {
            score += 5_000;
        }

        score += friendlyAdjacentStrength * 3_500;
        score -= enemyAdjacentStrength * 2_500;
        score -= immediateRecapturePenalty;

        if (cannotBeExceeded)
        {
            score += 120_000;
        }
        else if (dominatesInnerRing)
        {
            score += 32_000;
        }
        else if (holdsInnerAdvantage)
        {
            score += 10_000;
        }
        else
        {
            score -= 18_000;
        }

        return Math.Max(0, score);
    }

    private static int EvaluateObjectiveSiegeApproachScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out _) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase) ||
            ClassifyCommand(state, command) != CandidateClassification.Other)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        if (sourceUnit.Strength == 1 &&
            HasStrongerInwardApproachAvailable(state, sourceUnit.OwnerPlayerId, sourceUnit.Id))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance >= sourceDistance || targetDistance > 3)
        {
            return 0;
        }

        var currentContestCapacity = GetContestCapacity(state, state.CurrentPlayerId);
        var alreadyHasWinningContestStrength = currentContestCapacity > enemyOnObjective.Strength;

        var mergePartnersNearby = nextState.Units.Count(unit =>
            unit.OwnerPlayerId == movedUnit.OwnerPlayerId &&
            unit.Id != movedUnit.Id &&
            unit.Position.DistanceTo(HexCoordinate.Origin) <= 3 &&
            nextState.Board.AreAdjacent(unit.Position, movedUnit.Position));

        var score = 12_000;
        score += (sourceDistance - targetDistance) * 4_000;
        score += mergePartnersNearby * 3_500;

        if (alreadyHasWinningContestStrength)
        {
            score += 18_000;
            score += movedUnit.Strength * 1_400;
            if (targetDistance == 1)
            {
                score += 12_000;
            }
        }

        if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 2))
        {
            score += 6_000;
        }

        score -= EvaluateImmediateRecapturePenalty(nextState, movedUnit, sourceDistance, targetDistance);

        return score;
    }

    private static int EvaluateLevelFourSiegeApproachScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out var currentContestCapacity) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        if (sourceUnit.Position == HexCoordinate.Origin ||
            movedUnit.Position == HexCoordinate.Origin)
        {
            return 0;
        }

        if (ClassifyCommand(state, command) is CandidateClassification.Merge or CandidateClassification.MergeTowardObjective)
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance >= sourceDistance || targetDistance > 2)
        {
            return 0;
        }

        var nextContestCapacity = GetContestCapacity(nextState, state.CurrentPlayerId);
        var capacityGain = nextContestCapacity - currentContestCapacity;
        var requiredStrength = enemyOnObjective.Strength + 1;
        var movedUnitCanBeatObjective = movedUnit.Strength >= requiredStrength;
        var canReachR1Soon =
            movedUnit.Position.DistanceTo(HexCoordinate.Origin) == 1 ||
            CanReachDistanceOneRingNextTurn(nextState, movedUnit);
        var nearbyFriendlyStrength = nextState.Units
            .Where(unit =>
                unit.OwnerPlayerId == movedUnit.OwnerPlayerId &&
                unit.Id != movedUnit.Id &&
                unit.Position.DistanceTo(HexCoordinate.Origin) <= 2)
            .Sum(unit => unit.Strength);
        var adjacentFriendlyFollowUps = nextState.Units.Count(unit =>
            unit.OwnerPlayerId == movedUnit.OwnerPlayerId &&
            unit.Id != movedUnit.Id &&
            nextState.Board.AreAdjacent(unit.Position, movedUnit.Position) &&
            unit.Position.DistanceTo(HexCoordinate.Origin) <= 3);

        if (!movedUnitCanBeatObjective &&
            HasObjectiveBeatingInwardApproachAvailable(
                state,
                sourceUnit.OwnerPlayerId,
                sourceUnit.Id,
                enemyOnObjective.Strength))
        {
            return 0;
        }

        if (!movedUnitCanBeatObjective &&
            HasHigherStrengthInwardApproachAvailable(state, sourceUnit.OwnerPlayerId, sourceUnit.Id, movedUnit.Strength))
        {
            return 0;
        }

        if (!movedUnitCanBeatObjective &&
            capacityGain <= 0 &&
            !canReachR1Soon)
        {
            return 0;
        }

        var score = 34_000;
        score += movedUnit.Strength * 9_000;
        score += (sourceDistance - targetDistance) * 11_000;
        score += capacityGain * 14_000;
        score += nearbyFriendlyStrength * 1_400;
        score += adjacentFriendlyFollowUps * 3_200;

        if (targetDistance == 1)
        {
            score += 20_000;
        }
        else if (targetDistance == 2)
        {
            score += 8_000;
        }

        if (movedUnitCanBeatObjective)
        {
            score += 50_000;
        }
        else
        {
            score -= 12_000;
        }

        if (canReachR1Soon)
        {
            score += 10_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 2))
        {
            score += 12_000;
        }

        score -= EvaluateImmediateRecapturePenalty(nextState, movedUnit, sourceDistance, targetDistance);
        return score;
    }

    private static int EvaluateSiegeProjection(
        KingOfTheHillGameState state,
        string maximizingPlayerId)
    {
        var minimizingPlayerId = state.Players.Single(player => player.Id != maximizingPlayerId).Id;
        var maximizingOnObjective = state.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, maximizingPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);
        var minimizingOnObjective = state.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, minimizingPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        if (maximizingOnObjective is not null)
        {
            var holdScore = 30_000 + maximizingOnObjective.Strength * 4_500;
            if (!IsObjectiveHoldClearlyLost(state, maximizingOnObjective))
            {
                holdScore += 18_000;
            }

            return holdScore;
        }

        if (minimizingOnObjective is null)
        {
            return 0;
        }

        var contestCapacity = GetContestCapacity(state, maximizingPlayerId);
        var adjacentDefenseStrength = GetAdjacentObjectiveStrength(state, maximizingPlayerId);
        var zoneDefenseStrength = Math.Max(contestCapacity, adjacentDefenseStrength);
        var score = -18_000 - minimizingOnObjective.Strength * 2_500;

        if (contestCapacity > minimizingOnObjective.Strength)
        {
            score += 28_000;
        }
        else if (contestCapacity == minimizingOnObjective.Strength)
        {
            score += 10_000;
        }
        else
        {
            score += contestCapacity * 2_200;
        }

        if (zoneDefenseStrength > minimizingOnObjective.Strength)
        {
            score += 8_000;
        }
        else if (zoneDefenseStrength == minimizingOnObjective.Strength)
        {
            score += 3_000;
        }

        var nearObjectiveStrength = state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, maximizingPlayerId, StringComparison.OrdinalIgnoreCase) &&
                unit.Position.DistanceTo(HexCoordinate.Origin) <= 2)
            .Sum(unit => unit.Strength);

        score += nearObjectiveStrength * 700;
        return score;
    }

    private static int EvaluateObjectiveReinforcementScore(
        KingOfTheHillGameState state,
        GameCommand command,
        KingOfTheHillUnitState currentObjectiveHolder)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        var reinforcedUnit = nextState.FindUnitAt(HexCoordinate.Origin);
        if (reinforcedUnit is null ||
            !string.Equals(reinforcedUnit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) ||
            reinforcedUnit.Strength <= currentObjectiveHolder.Strength)
        {
            return 0;
        }

        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out _))
        {
            return 0;
        }

        var sourceThreat = GetNextTurnThreatStrength(state, currentObjectiveHolder);
        var reinforcedThreat = GetNextTurnThreatStrength(nextState, reinforcedUnit);
        if (sourceThreat > 0 && reinforcedThreat >= sourceThreat)
        {
            return 0;
        }

        var strengthGain = reinforcedUnit.Strength - currentObjectiveHolder.Strength;
        var score = 70_000;
        score += strengthGain * 16_000;
        score += reinforcedUnit.Strength * 5_000;

        if (sourceUnit.Position.DistanceTo(HexCoordinate.Origin) == 1)
        {
            score += 8_000;
        }
        else if (sourceUnit.Strength == 1 && sourceUnit.Position.DistanceTo(HexCoordinate.Origin) == 2)
        {
            score += 4_000;
        }

        var adjacentEnemyStrength = nextState.Units
            .Where(unit =>
                unit.OwnerPlayerId != state.CurrentPlayerId &&
                nextState.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);
        var adjacentFriendlySupport = nextState.Units
            .Where(unit =>
                unit.OwnerPlayerId == state.CurrentPlayerId &&
                unit.Position != HexCoordinate.Origin &&
                nextState.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        score += adjacentFriendlySupport * 1_600;
        score -= adjacentEnemyStrength * 900;

        return score;
    }

    private static int EvaluateObjectiveSupportApproachScore(
        KingOfTheHillGameState state,
        GameCommand command,
        KingOfTheHillUnitState currentObjectiveHolder)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        if (sourceUnit.Position == HexCoordinate.Origin || movedUnit.Position == HexCoordinate.Origin)
        {
            return 0;
        }

        var objectiveStillHeld = nextState.Units.Any(unit =>
            string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);
        if (!objectiveStillHeld)
        {
            return 0;
        }

        if (GetNextTurnThreatStrength(state, currentObjectiveHolder) > 0)
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance >= sourceDistance || targetDistance > 3)
        {
            return 0;
        }

        var beforeReinforceTurns = EstimateTurnsToReinforceObjective(sourceUnit);
        var afterReinforceTurns = EstimateTurnsToReinforceObjective(movedUnit);
        if (afterReinforceTurns >= beforeReinforceTurns)
        {
            return 0;
        }

        var holderThreat = nextState.Units
            .Where(unit =>
                unit.OwnerPlayerId != state.CurrentPlayerId &&
                nextState.Board.AreAdjacent(unit.Position, HexCoordinate.Origin))
            .Sum(unit => unit.Strength);

        var score = 38_000;
        score += (beforeReinforceTurns - afterReinforceTurns) * 16_000;
        score += (sourceDistance - targetDistance) * 5_000;
        score += movedUnit.Strength * 2_500;
        score += currentObjectiveHolder.Strength * 1_800;

        if (targetDistance == 1)
        {
            score += 12_000;
        }
        else if (targetDistance == 2)
        {
            score += 5_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 2))
        {
            score += 8_000;
        }

        score += holderThreat * 900;
        return score;
    }

    private static int EvaluateObjectiveReserveMobilizationScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out var currentContestCapacity) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceUnit.Position == HexCoordinate.Origin ||
            movedUnit.Position == HexCoordinate.Origin ||
            sourceDistance < 3 ||
            targetDistance >= sourceDistance)
        {
            return 0;
        }

        var alreadyHasWinningContestStrength = currentContestCapacity > enemyOnObjective.Strength;
        if (alreadyHasWinningContestStrength)
        {
            return 0;
        }

        if (sourceUnit.Strength == 1 &&
            HasStrongerInwardApproachAvailable(state, sourceUnit.OwnerPlayerId, sourceUnit.Id))
        {
            return 0;
        }

        var score = 24_000;
        score += movedUnit.Strength * 8_000;
        score += (sourceDistance - targetDistance) * 10_000;

        if (sourceDistance >= 4)
        {
            score += 6_000;
        }

        if (targetDistance == 2)
        {
            score += 14_000;
        }
        else if (targetDistance == 1)
        {
            score += 18_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 2))
        {
            score += 16_000;
        }
        else if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 3))
        {
            score += 8_000;
        }

        var nearbyFriendlyPressure = nextState.Units.Count(unit =>
            unit.OwnerPlayerId == movedUnit.OwnerPlayerId &&
            unit.Id != movedUnit.Id &&
            unit.Position.DistanceTo(HexCoordinate.Origin) <= 2);
        score += nearbyFriendlyPressure * 2_000;

        return score;
    }

    private static int EvaluateExcessiveSiegeMergePenalty(
        KingOfTheHillGameState state,
        KingOfTheHillGameState nextState,
        KingOfTheHillUnitState sourceUnit,
        KingOfTheHillUnitState mergedUnit,
        KingOfTheHillUnitState enemyOnObjective,
        int currentContestCapacity)
    {
        if (mergedUnit.Position == HexCoordinate.Origin)
        {
            return 0;
        }

        var mergedDistance = mergedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (mergedDistance > 2)
        {
            return 0;
        }

        var nextContestCapacity = GetContestCapacity(nextState, state.CurrentPlayerId);
        var requiredStrength = enemyOnObjective.Strength + 1;
        var usefulStrengthCeiling = Math.Min(KingOfTheHillGameState.MaximumBlockStrength, requiredStrength);
        var excessStrength = Math.Max(0, mergedUnit.Strength - usefulStrengthCeiling);
        var capacityGain = nextContestCapacity - currentContestCapacity;

        if (excessStrength == 0 && capacityGain > 0)
        {
            return 0;
        }

        var penalty = 0;

        if (excessStrength > 0)
        {
            penalty += excessStrength * 12_000;
        }

        if (capacityGain <= 0)
        {
            penalty += 18_000;
        }

        if (sourceUnit.Position.DistanceTo(HexCoordinate.Origin) >= 2 &&
            mergedDistance >= 2)
        {
            penalty += 10_000;
        }

        if (mergedDistance == 2 &&
            !CanThreatenObjectiveWithinTurns(nextState, mergedUnit, 2))
        {
            penalty += 10_000;
        }

        return penalty;
    }

    private static bool HasStrongerInwardApproachAvailable(
        KingOfTheHillGameState state,
        string playerId,
        string excludedUnitId)
    {
        return state.Units.Any(unit =>
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(unit.Id, excludedUnitId, StringComparison.OrdinalIgnoreCase) &&
            unit.Strength > 1 &&
            unit.Position != HexCoordinate.Origin &&
            state.Board.GetReachableCoordinates(unit.Position, unit.Strength == 1 ? 2 : 1)
                .Any(target => target.DistanceTo(HexCoordinate.Origin) < unit.Position.DistanceTo(HexCoordinate.Origin)));
    }

    private static bool HasHigherStrengthInwardApproachAvailable(
        KingOfTheHillGameState state,
        string playerId,
        string excludedUnitId,
        int currentStrength)
    {
        return state.Units.Any(unit =>
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(unit.Id, excludedUnitId, StringComparison.OrdinalIgnoreCase) &&
            unit.Strength > currentStrength &&
            unit.Position != HexCoordinate.Origin &&
            state.Board.GetReachableCoordinates(unit.Position, unit.Strength == 1 ? 2 : 1)
                .Any(target =>
                    target.DistanceTo(HexCoordinate.Origin) < unit.Position.DistanceTo(HexCoordinate.Origin) &&
                    target.DistanceTo(HexCoordinate.Origin) <= 2));
    }

    private static bool HasObjectiveBeatingInwardApproachAvailable(
        KingOfTheHillGameState state,
        string playerId,
        string excludedUnitId,
        int defenderStrength)
    {
        return state.Units.Any(unit =>
            string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(unit.Id, excludedUnitId, StringComparison.OrdinalIgnoreCase) &&
            unit.Strength > defenderStrength &&
            unit.Position != HexCoordinate.Origin &&
            state.Board.GetReachableCoordinates(unit.Position, unit.Strength == 1 ? 2 : 1)
                .Any(target =>
                    target.DistanceTo(HexCoordinate.Origin) < unit.Position.DistanceTo(HexCoordinate.Origin) &&
                    target.DistanceTo(HexCoordinate.Origin) <= 2));
    }

    private static int EvaluateObjectiveBreakthroughApproachScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!NeedsObjectiveSiege(state, out var enemyOnObjective, out _) ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        if (sourceUnit.Strength == 1 &&
            HasStrongerInwardApproachAvailable(state, sourceUnit.OwnerPlayerId, sourceUnit.Id))
        {
            return 0;
        }

        if (movedUnit.Strength <= enemyOnObjective.Strength)
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance >= sourceDistance)
        {
            return 0;
        }

        var score = 54_000;
        score += (movedUnit.Strength - enemyOnObjective.Strength) * 6_000;
        score += (sourceDistance - targetDistance) * 12_000;
        score += movedUnit.Strength * 1_800;

        if (targetDistance == 1)
        {
            score += 20_000;
        }
        else if (targetDistance == 2)
        {
            score += 10_000;
        }

        if (CanThreatenObjectiveWithinTurns(nextState, movedUnit, 2))
        {
            score += 16_000;
        }

        return score;
    }

    private static int EvaluateObjectiveEmergencyRetreatScore(
        KingOfTheHillGameState state,
        GameCommand command,
        KingOfTheHillUnitState currentObjectiveHolder)
    {
        if (currentObjectiveHolder.Position != HexCoordinate.Origin ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase) ||
            GetNextTurnThreatStrength(state, currentObjectiveHolder) == 0)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit) ||
            sourceUnit.Position != HexCoordinate.Origin ||
            movedUnit.Position == HexCoordinate.Origin)
        {
            return 0;
        }

        var sourceThreat = GetNextTurnThreatStrength(state, sourceUnit);
        var targetThreat = GetNextTurnThreatStrength(nextState, movedUnit);
        if (targetThreat >= sourceThreat)
        {
            return 0;
        }

        var score = 85_000;
        score += (sourceThreat - targetThreat) * 10_000;
        score += sourceUnit.Strength * 4_000;

        if (targetThreat == 0)
        {
            score += 20_000;
        }
        else if (targetThreat <= movedUnit.Strength)
        {
            score += 8_000;
        }

        if (movedUnit.Position.DistanceTo(HexCoordinate.Origin) == 1)
        {
            score += 6_000;
        }

        return score;
    }

    private static int EvaluateDefenderResetScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase) ||
            state.Units.Any(unit => unit.Position == HexCoordinate.Origin))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit) ||
            !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance != 1)
        {
            return 0;
        }

        var defenderCoordinate = GetDefenderCoordinate(sourceUnit.Id);
        if (movedUnit.Position != defenderCoordinate)
        {
            return 0;
        }

        var score = 44_000;
        score += sourceUnit.Strength * 4_000;
        score += GetDefenderPressure(state, sourceUnit) * 2_500;

        return score;
    }

    private static int EvaluateDefenderInterceptScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!IsAcceptedDefenderR1Intercept(state, command))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        var interceptedEnemy = state.FindUnitAt(movedUnit.Position);
        if (interceptedEnemy is null)
        {
            return 0;
        }

        var score = 140_000;
        score += sourceUnit.Strength * 3_000;
        score += interceptedEnemy.Strength * 2_000;
        score += GetDefenderPressure(state, sourceUnit) * 500;

        return score;
    }

    private static int EvaluateDefenderLaneDenialScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!TryGetDefenderLaneDenialContext(state, command, out var sourceUnit, out var interceptedEnemy))
        {
            return 0;
        }

        var score = 74_000;
        score += interceptedEnemy.Strength * 4_000;
        score += sourceUnit.Strength * 2_000;

        if (interceptedEnemy.Position.DistanceTo(HexCoordinate.Origin) == 3)
        {
            score += 8_000;
        }

        return score;
    }

    private static int EvaluateDefenderAdvancePenalty(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState sourceUnit,
        KingOfTheHillUnitState movedUnit)
    {
        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4 ||
            state.Units.Any(unit => unit.Position == HexCoordinate.Origin) ||
            !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId))
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance != 2 || targetDistance >= sourceDistance)
        {
            return 0;
        }

        var anchorPressure = GetDefenderPressure(state, sourceUnit);
        if (anchorPressure == 0)
        {
            return 0;
        }

        return -48_000 - anchorPressure * 3_000 - movedUnit.Strength * 2_500;
    }

    private static IReadOnlyList<PreviewedCommand> ApplyDefenderAdvanceRestrictions(
        KingOfTheHillGameState state,
        IReadOnlyList<PreviewedCommand> rankedEntries)
    {
        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4)
        {
            return rankedEntries;
        }

        var filteredEntries = rankedEntries
            .Where(entry =>
                !ShouldSuppressDefenderInnerAdvance(state, entry.Command) &&
                !ShouldSuppressDefenderAnchorDrift(state, entry.Command))
            .ToArray();

        return filteredEntries.Length > 0
            ? filteredEntries
            : rankedEntries;
    }

    private static int EstimateTurnsToReinforceObjective(KingOfTheHillUnitState unit)
    {
        var distance = unit.Position.DistanceTo(HexCoordinate.Origin);
        if (distance == 0)
        {
            return 0;
        }

        return unit.Strength == 1
            ? (int)Math.Ceiling(distance / 2.0)
            : distance;
    }

    private static IReadOnlyList<KingOfTheHillUnitState> GetDefenderUnits(
        KingOfTheHillGameState state,
        string playerId)
    {
        var defenderIds = playerId.Equals("P1", StringComparison.OrdinalIgnoreCase)
            ? new[] { "1T", "1V", "1X" }
            : new[] { "2T", "2V", "2X" };

        return defenderIds
            .Select(id => state.FindUnit(id))
            .Where(unit => unit is not null)
            .Cast<KingOfTheHillUnitState>()
            .ToArray();
    }

    private static HexCoordinate GetDefenderCoordinate(string unitId) =>
        unitId switch
        {
            "1T" => new HexCoordinate(1, -2),
            "1V" => new HexCoordinate(-1, -1),
            "1X" => new HexCoordinate(2, -1),
            "2T" => new HexCoordinate(-1, 2),
            "2V" => new HexCoordinate(1, 1),
            "2X" => new HexCoordinate(-2, 1),
            _ => HexCoordinate.Origin
        };

    private static bool IsDefenderUnit(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState unit,
        string playerId)
    {
        if (!string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsDefenderIdentifier(unit.Id) &&
               !state.IsDefenderRetired(unit.Id) &&
               !HasLostDefenderRoleToRingTwoThreat(state, unit);
    }

    private static bool IsDefenderIdentifier(string unitId) =>
        unitId is "1T" or "1V" or "1X" or "2T" or "2V" or "2X";

    private static bool HasLostDefenderRoleToRingTwoThreat(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState unit)
    {
        if (unit.Position.DistanceTo(HexCoordinate.Origin) != 2)
        {
            return false;
        }

        return state.Units.Any(other =>
            !string.Equals(other.OwnerPlayerId, unit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
            other.Position.DistanceTo(HexCoordinate.Origin) == 2 &&
            other.Strength >= 4 &&
            state.Board.AreAdjacent(other.Position, unit.Position));
    }

    private static bool ShouldSuppressDefenderObjectiveEntry(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4 ||
            state.FindUnitAt(HexCoordinate.Origin) is not null ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        return sourceUnit is not null &&
               IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId);
    }

    private static bool ShouldSuppressDefenderInnerAdvance(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4 ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null || !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId) || command.Arguments is null)
        {
            return false;
        }

        if (!command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return false;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = new HexCoordinate(q, r).DistanceTo(HexCoordinate.Origin);
        if (sourceDistance < 2 || targetDistance >= 2)
        {
            return false;
        }

        return !IsImmediateDefenderInnerAdvanceException(state, command) &&
               !IsAcceptedDefenderR1Intercept(state, command);
    }

    private static bool IsImmediateDefenderInnerAdvanceException(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return false;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (nextState.IsCompleted &&
            string.Equals(nextState.WinnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var enemyOnObjective = state.Units.SingleOrDefault(unit =>
            !string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        if (enemyOnObjective is null)
        {
            return false;
        }

        var currentPlayerOnObjective = nextState.Units.SingleOrDefault(unit =>
            string.Equals(unit.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
            unit.Position == HexCoordinate.Origin);

        if (currentPlayerOnObjective is not null)
        {
            return true;
        }

        return GetAdjacentObjectiveStrength(nextState, state.CurrentPlayerId) > enemyOnObjective.Strength ||
               GetObjectiveZoneDefenseStrength(nextState, state.CurrentPlayerId) > enemyOnObjective.Strength;
    }

    private static bool ShouldSuppressDefenderAnchorDrift(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4 ||
            state.FindUnitAt(HexCoordinate.Origin) is not null ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null ||
            !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId) ||
            command.Arguments is null)
        {
            return false;
        }

        var anchorCoordinate = GetDefenderCoordinate(sourceUnit.Id);
        if (sourceUnit.Position != anchorCoordinate ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return false;
        }

        var targetCoordinate = new HexCoordinate(q, r);
        if (targetCoordinate == anchorCoordinate)
        {
            return false;
        }

        if (IsImmediateDefenderInnerAdvanceException(state, command) ||
            IsAcceptedDefenderR1Intercept(state, command) ||
            IsAcceptedDefenderLaneDenial(state, command) ||
            IsAcceptedDefenderRingTwoKill(state, command, targetCoordinate) ||
            IsAcceptedDefenderRingTwoMerge(state, command, targetCoordinate))
        {
            return false;
        }

        return true;
    }

    private static bool IsAcceptedDefenderRingTwoKill(
        KingOfTheHillGameState state,
        GameCommand command,
        HexCoordinate targetCoordinate)
    {
        if (targetCoordinate.DistanceTo(HexCoordinate.Origin) != 2 ||
            ClassifyCommand(state, command) is not CandidateClassification.KillInnerOrSameRing and not CandidateClassification.KillOuterSafe)
        {
            return false;
        }

        return KingOfTheHillGameRules.Execute(state, command).Accepted;
    }

    private static bool IsAcceptedDefenderR1Intercept(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (ClassifyCommand(state, command) is not CandidateClassification.KillInnerOrSameRing and not CandidateClassification.KillOuterSafe ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return false;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit) ||
            !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId))
        {
            return false;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance != 2 || targetDistance != 1)
        {
            return false;
        }

        var interceptedEnemy = state.FindUnitAt(movedUnit.Position);
        return interceptedEnemy is not null &&
               !string.Equals(interceptedEnemy.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAcceptedDefenderLaneDenial(
        KingOfTheHillGameState state,
        GameCommand command) =>
        TryGetDefenderLaneDenialContext(state, command, out _, out _);

    private static bool IsAcceptedDefenderRingTwoMerge(
        KingOfTheHillGameState state,
        GameCommand command,
        HexCoordinate targetCoordinate)
    {
        if (targetCoordinate.DistanceTo(HexCoordinate.Origin) != 2 ||
            !IsDefenderToDefenderMerge(state, command, state.CurrentPlayerId))
        {
            return false;
        }

        return KingOfTheHillGameRules.Execute(state, command).Accepted;
    }

    private static bool IsDefenderToDefenderMerge(
        KingOfTheHillGameState state,
        GameCommand command,
        string playerId)
    {
        if (ClassifyCommand(state, command) is not CandidateClassification.Merge and not CandidateClassification.MergeTowardObjective)
        {
            return false;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null || !IsDefenderUnit(state, sourceUnit, playerId) || command.Arguments is null)
        {
            return false;
        }

        if (!command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return false;
        }

        var targetUnit = state.FindUnitAt(new HexCoordinate(q, r));
        return targetUnit is not null &&
               IsDefenderUnit(state, targetUnit, playerId);
    }

    private static int GetDefenderPressure(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState defenderUnit)
    {
        return state.Units
            .Where(unit =>
                !string.Equals(unit.OwnerPlayerId, defenderUnit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
                (unit.Position.DistanceTo(HexCoordinate.Origin) <= 3 || CanReachDistanceOneRingNextTurn(state, unit)))
            .Sum(unit => unit.Strength);
    }

    private static bool HasAdjacentEnemyStrengthAtLeast(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState unit,
        int minimumStrength)
    {
        return state.Units.Any(other =>
            !string.Equals(other.OwnerPlayerId, unit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
            other.Strength >= minimumStrength &&
            state.Board.AreAdjacent(other.Position, unit.Position));
    }

    private static bool CanEnemyBuildSiegePressureSoon(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState enemyUnit)
    {
        var enemyDistance = enemyUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (enemyDistance > 3)
        {
            return false;
        }

        if (enemyDistance == 3 && enemyUnit.Strength >= 2)
        {
            return true;
        }

        return state.Units.Any(other =>
            other.Id != enemyUnit.Id &&
            string.Equals(other.OwnerPlayerId, enemyUnit.OwnerPlayerId, StringComparison.OrdinalIgnoreCase) &&
            state.Board.AreAdjacent(other.Position, enemyUnit.Position) &&
            enemyUnit.Strength + other.Strength <= KingOfTheHillGameState.MaximumBlockStrength &&
            (enemyDistance <= 3 && enemyUnit.Strength + other.Strength >= 3));
    }

    private static bool TryGetDefenderLaneDenialContext(
        KingOfTheHillGameState state,
        GameCommand command,
        out KingOfTheHillUnitState sourceUnit,
        out KingOfTheHillUnitState interceptedEnemy)
    {
        sourceUnit = null!;
        interceptedEnemy = null!;

        var currentPlayer = state.Players.Single(player => player.Id == state.CurrentPlayerId);
        if (currentPlayer.ControllerType != PlayerControllerType.IaLevel4 ||
            GetMatchPhase(state) != MatchPhase.Opening ||
            state.FindUnitAt(HexCoordinate.Origin) is not null ||
            !string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return false;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out sourceUnit, out var movedUnit) ||
            !IsDefenderUnit(state, sourceUnit, state.CurrentPlayerId))
        {
            return false;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance != 2 || targetDistance != 3)
        {
            return false;
        }

        interceptedEnemy = state.FindUnitAt(movedUnit.Position)!;
        return interceptedEnemy is not null &&
               !string.Equals(interceptedEnemy.OwnerPlayerId, state.CurrentPlayerId, StringComparison.OrdinalIgnoreCase) &&
               CanEnemyBuildSiegePressureSoon(state, interceptedEnemy);
    }

    private static int EvaluateDefensiveMergeScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (ClassifyCommand(state, command) is not CandidateClassification.Merge and not CandidateClassification.MergeTowardObjective)
        {
            return 0;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null ||
            command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return 0;
        }

        var target = new HexCoordinate(q, r);
        var targetUnit = state.FindUnitAt(target);
        if (targetUnit is null || targetUnit.OwnerPlayerId != sourceUnit.OwnerPlayerId)
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = target.DistanceTo(HexCoordinate.Origin);
        if (targetDistance > 2 || sourceDistance > 2)
        {
            return 0;
        }

        var sourceThreat = GetImmediateThreatStrength(state, sourceUnit);
        var targetThreat = GetImmediateThreatStrength(state, targetUnit);
        if (sourceThreat == 0 && targetThreat == 0)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        var mergedUnit = nextState.FindUnitAt(target);
        if (mergedUnit is null || mergedUnit.OwnerPlayerId != sourceUnit.OwnerPlayerId)
        {
            return 0;
        }

        var mergedThreat = GetImmediateThreatStrength(nextState, mergedUnit);
        var threatReduction = (sourceThreat + targetThreat) - mergedThreat;
        if (threatReduction <= 0)
        {
            return 0;
        }

        var threatenedMaterial = 0;
        if (sourceThreat > 0)
        {
            threatenedMaterial += sourceUnit.Strength;
        }

        if (targetThreat > 0)
        {
            threatenedMaterial += targetUnit.Strength;
        }

        var score = 20_000;
        score += threatReduction * 3_000;
        score += threatenedMaterial * 2_200;
        score += mergedUnit.Strength * 1_500;

        if (targetDistance <= 1)
        {
            score += 6_000;
        }
        else if (targetDistance == 2)
        {
            score += 2_500;
        }

        if (CanThreatenObjectiveOnNextTurn(nextState, mergedUnit))
        {
            score += 3_500;
        }

        return score;
    }

    private static int EvaluateSurvivalRetreatScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out var sourceUnit, out var movedUnit))
        {
            return 0;
        }

        var sourceThreat = GetNextTurnThreatStrength(state, sourceUnit);
        if (sourceThreat == 0)
        {
            return 0;
        }

        var targetThreat = GetNextTurnThreatStrength(nextState, movedUnit);
        if (targetThreat >= sourceThreat)
        {
            return 0;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        var score = 26_000;
        score += (sourceThreat - targetThreat) * 6_000;
        score += sourceUnit.Strength * 3_000;

        if (sourceUnit.Position == HexCoordinate.Origin)
        {
            score += 12_000;
        }
        else if (sourceDistance <= 1)
        {
            score += 6_000;
        }

        if (targetThreat == 0)
        {
            score += 10_000;
        }
        else if (targetThreat <= movedUnit.Strength)
        {
            score += 5_000;
        }

        if (targetDistance <= sourceDistance)
        {
            score += 2_500;
        }

        return score;
    }

    private static int EvaluateForcedInnerThreatScore(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (ClassifyCommand(state, command) != CandidateClassification.Other)
        {
            return 0;
        }

        var result = KingOfTheHillGameRules.Execute(state, command);
        if (!result.Accepted)
        {
            return 0;
        }

        var nextState = (KingOfTheHillGameState)result.State;
        if (!TryGetMoveContext(state, nextState, command, state.CurrentPlayerId, out _, out var movedUnit))
        {
            return 0;
        }

        var targetDistance = movedUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (targetDistance > 2)
        {
            return 0;
        }

        var threatenedEnemies = nextState.Units
            .Where(enemy =>
                enemy.OwnerPlayerId != movedUnit.OwnerPlayerId &&
                enemy.Position.DistanceTo(HexCoordinate.Origin) <= 2 &&
                nextState.Board.AreAdjacent(enemy.Position, movedUnit.Position) &&
                movedUnit.Strength > enemy.Strength)
            .ToArray();

        if (threatenedEnemies.Length == 0)
        {
            return 0;
        }

        var sourceDistance = state.FindUnit(command.GetRequiredArgument("unitId"))?.Position.DistanceTo(HexCoordinate.Origin) ?? targetDistance;
        var score = 16_000;
        score += threatenedEnemies.Length * 5_500;
        score += threatenedEnemies.Sum(enemy => enemy.Strength) * 2_200;
        score += movedUnit.Strength * 1_300;

        if (targetDistance == 1)
        {
            score += 7_000;
        }
        else if (targetDistance == 2)
        {
            score += 3_000;
        }

        if (targetDistance < sourceDistance)
        {
            score += 4_000;
        }

        if (CanThreatenObjectiveOnNextTurn(nextState, movedUnit))
        {
            score += 3_500;
        }

        score -= EvaluateImmediateRecapturePenalty(nextState, movedUnit, sourceDistance, targetDistance);

        return score;
    }

    private static KillOpportunity EvaluateKillOpportunity(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return KillOpportunity.NotAKill;
        }

        var sourceUnit = state.FindUnit(command.GetRequiredArgument("unitId"));
        if (sourceUnit is null ||
            command.Arguments is null ||
            !command.Arguments.TryGetValue("q", out var qValue) ||
            !command.Arguments.TryGetValue("r", out var rValue) ||
            !int.TryParse(qValue, out var q) ||
            !int.TryParse(rValue, out var r))
        {
            return KillOpportunity.NotAKill;
        }

        var target = new HexCoordinate(q, r);
        var targetUnit = state.FindUnitAt(target);
        if (targetUnit is null ||
            targetUnit.OwnerPlayerId == sourceUnit.OwnerPlayerId ||
            sourceUnit.Strength <= targetUnit.Strength)
        {
            return KillOpportunity.NotAKill;
        }

        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = target.DistanceTo(HexCoordinate.Origin);

        if (targetDistance <= sourceDistance)
        {
            return KillOpportunity.InnerOrSameRingFavorable;
        }

        return LeavesInnerRingStrengthInferiorAfterOuterKill(state, sourceUnit, target)
            ? KillOpportunity.NotFavorable
            : KillOpportunity.OuterRingFavorable;
    }

    private static bool LeavesInnerRingStrengthInferiorAfterOuterKill(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState sourceUnit,
        HexCoordinate target)
    {
        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        var targetDistance = target.DistanceTo(HexCoordinate.Origin);

        if (targetDistance <= sourceDistance)
        {
            return false;
        }

        var innerRingDistance = Math.Max(0, targetDistance - 1);

        var friendlyInnerStrengthBefore = state.Units
            .Where(unit =>
                unit.OwnerPlayerId == sourceUnit.OwnerPlayerId &&
                unit.Position.DistanceTo(HexCoordinate.Origin) <= innerRingDistance)
            .Sum(unit => unit.Strength);

        var enemyInnerStrength = state.Units
            .Where(unit =>
                unit.OwnerPlayerId != sourceUnit.OwnerPlayerId &&
                unit.Position.DistanceTo(HexCoordinate.Origin) <= innerRingDistance)
            .Sum(unit => unit.Strength);

        var friendlyInnerStrengthAfter = friendlyInnerStrengthBefore;
        if (sourceUnit.Position.DistanceTo(HexCoordinate.Origin) <= innerRingDistance)
        {
            friendlyInnerStrengthAfter -= sourceUnit.Strength;
        }

        return friendlyInnerStrengthAfter < enemyInnerStrength;
    }

    private static bool CanReachDistanceOneRingNextTurn(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        var movementDepth = unit.Strength == 1 ? 2 : 1;
        return state.Board.GetReachableCoordinates(unit.Position, movementDepth)
            .Any(coordinate => coordinate.DistanceTo(HexCoordinate.Origin) == 1);
    }

    private static bool CanThreatenObjectiveWithinTurns(
        HexBoard board,
        HexCoordinate position,
        int strength,
        int turnCount)
    {
        if (turnCount <= 1)
        {
            var movementDepth = strength == 1 ? 2 : 1;
            return board.GetReachableCoordinates(position, movementDepth).Contains(HexCoordinate.Origin);
        }

        var currentFrontier = new HashSet<HexCoordinate> { position };
        var visited = new HashSet<HexCoordinate> { position };

        for (var turn = 0; turn < turnCount; turn++)
        {
            var nextFrontier = new HashSet<HexCoordinate>();

            foreach (var frontierPosition in currentFrontier)
            {
                var depth = strength == 1 ? 2 : 1;
                foreach (var reachable in board.GetReachableCoordinates(frontierPosition, depth))
                {
                    if (reachable == HexCoordinate.Origin)
                    {
                        return true;
                    }

                    if (visited.Add(reachable))
                    {
                        nextFrontier.Add(reachable);
                    }
                }
            }

            currentFrontier = nextFrontier;
            if (currentFrontier.Count == 0)
            {
                break;
            }
        }

        return false;
    }


    private static bool NeedsObjectiveSiege(
        KingOfTheHillGameState state,
        out KingOfTheHillUnitState enemyOnObjective,
        out int currentContestCapacity)
    {
        enemyOnObjective = state.Units.SingleOrDefault(unit =>
            unit.OwnerPlayerId != state.CurrentPlayerId &&
            unit.Position == HexCoordinate.Origin)!;

        if (enemyOnObjective is null)
        {
            currentContestCapacity = 0;
            return false;
        }

        currentContestCapacity = GetContestCapacity(state, state.CurrentPlayerId);
        return currentContestCapacity <= enemyOnObjective.Strength;
    }

    private static int GetContestCapacity(KingOfTheHillGameState state, string playerId) =>
        state.Units
            .Where(unit =>
                string.Equals(unit.OwnerPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                CanThreatenObjectiveOnNextTurn(state, unit))
            .Max(unit => (int?)unit.Strength) ?? 0;

    private static int GetImmediateThreatStrength(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        return state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position) &&
                other.Strength > unit.Strength)
            .Max(other => (int?)other.Strength) ?? 0;
    }

    private static int GetNextTurnThreatStrength(KingOfTheHillGameState state, KingOfTheHillUnitState unit)
    {
        return state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                other.Strength > unit.Strength &&
                CanEliminateUnitOnNextTurn(state, other, unit))
            .Max(other => (int?)other.Strength) ?? 0;
    }

    private static bool CanEliminateUnitOnNextTurn(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState attacker,
        KingOfTheHillUnitState target)
    {
        var movementDepth = attacker.Strength == 1 ? 2 : 1;
        if (!state.Board.GetReachableCoordinates(attacker.Position, movementDepth).Contains(target.Position))
        {
            return false;
        }

        var simulatedTurnState = string.Equals(state.CurrentPlayerId, attacker.OwnerPlayerId, StringComparison.OrdinalIgnoreCase)
            ? state
            : state with { CurrentPlayerId = attacker.OwnerPlayerId };

        return KingOfTheHillGameRules.Execute(
            simulatedTurnState,
            CreateMoveCommand(attacker.Id, target.Position)).Accepted;
    }

    private static int EvaluateUnjustifiedInnerRetreatPenalty(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState sourceUnit,
        int targetDistance)
    {
        var sourceDistance = sourceUnit.Position.DistanceTo(HexCoordinate.Origin);
        if (sourceDistance > 1 || targetDistance <= sourceDistance)
        {
            return 0;
        }

        var currentPlayerHasObjective = state.Units.Any(unit =>
            unit.OwnerPlayerId == sourceUnit.OwnerPlayerId &&
            unit.Position == HexCoordinate.Origin);
        if (currentPlayerHasObjective)
        {
            return 0;
        }

        var immediateThreat = GetImmediateThreatStrength(state, sourceUnit);
        if (immediateThreat > sourceUnit.Strength)
        {
            return 0;
        }

        var penalty = 28_000 + sourceUnit.Strength * 3_000;

        if (targetDistance == 2)
        {
            penalty += 8_000;
        }
        else if (targetDistance > 2)
        {
            penalty += 14_000;
        }

        return penalty;
    }

    private static int EvaluateImmediateRecapturePenalty(
        KingOfTheHillGameState state,
        KingOfTheHillUnitState unit,
        int sourceDistance,
        int targetDistance)
    {
        var strongerAdjacentEnemies = state.Units
            .Where(other =>
                other.OwnerPlayerId != unit.OwnerPlayerId &&
                state.Board.AreAdjacent(other.Position, unit.Position) &&
                other.Strength > unit.Strength)
            .ToArray();

        if (strongerAdjacentEnemies.Length == 0)
        {
            return 0;
        }

        var strongestEnemy = strongerAdjacentEnemies.Max(enemy => enemy.Strength);
        var penalty = 24_000;
        penalty += (strongestEnemy - unit.Strength) * 4_000;
        penalty += unit.Strength * 2_500;

        if (targetDistance <= sourceDistance)
        {
            penalty += 10_000;
        }

        if (targetDistance <= 1)
        {
            penalty += 12_000;
        }
        else if (targetDistance == 2)
        {
            penalty += 4_000;
        }

        if (CanThreatenObjectiveOnNextTurn(state, unit))
        {
            penalty += 6_000;
        }

        return penalty;
    }

    private static GameCommand CreateMoveCommand(string unitId, HexCoordinate target) =>
        new(
            "move",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitId"] = unitId,
                ["q"] = target.Q.ToString(),
                ["r"] = target.R.ToString()
            });

    private static string FormatCommand(GameCommand command)
    {
        if (command.Arguments is null)
        {
            return command.Name;
        }

        return $"{command.Name}:{string.Join(",", command.Arguments.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"))}";
    }

    private static bool CommandsMatch(GameCommand left, GameCommand right) =>
        string.Equals(FormatCommand(left), FormatCommand(right), StringComparison.OrdinalIgnoreCase);

    private static PreviewedCommand FindRankedEntry(
        IReadOnlyList<PreviewedCommand> rankedEntries,
        GameCommand command,
        string decisionRuleCode,
        string decisionRuleName) =>
        rankedEntries.First(entry => CommandsMatch(entry.Command, command)) with
        {
            DecisionRuleCode = decisionRuleCode,
            DecisionRuleName = decisionRuleName
        };

    private AutomatedDecisionResult BuildDecisionResult(
        KingOfTheHillGameState state,
        PlayerToken player,
        PreviewedCommand chosenCommand,
        double elapsedMilliseconds,
        SearchInstrumentation instrumentation)
    {
        var chosenCommandDescription = FormatCommand(chosenCommand.Command) +
            FormatDefenderRoleLogSuffix(state, chosenCommand.Command);

        var telemetry = new AutomatedDecisionTelemetry(
            player.Id,
            player.DisplayName,
            player.ControllerType,
            Configuration.SearchDepth,
            Configuration.TimeBudgetMilliseconds,
            Configuration.SecondChoiceProbability,
            Configuration.MaxCandidateCount,
            instrumentation.LegalCommandCount,
            instrumentation.CandidateCommandCount,
            instrumentation.NodesVisited,
            instrumentation.LeafEvaluations,
            chosenCommand.Score,
            elapsedMilliseconds,
            instrumentation.TimeBudgetReached,
            chosenCommandDescription,
            chosenCommand.DecisionRuleCode,
            chosenCommand.DecisionRuleName);

        return new AutomatedDecisionResult(chosenCommand.Command, telemetry);
    }

    private static string FormatDefenderRoleLogSuffix(
        KingOfTheHillGameState state,
        GameCommand command)
    {
        if (!string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var unitId = command.GetRequiredArgument("unitId");
        if (!IsDefenderIdentifier(unitId))
        {
            return string.Empty;
        }

        var unit = state.FindUnit(unitId);
        if (unit is null)
        {
            return string.Empty;
        }

        if (state.IsDefenderRetired(unitId))
        {
            return " | former defender (retired)";
        }

        if (HasLostDefenderRoleToRingTwoThreat(state, unit))
        {
            return " | defender role disabled by adjacent S4+ threat on r2";
        }

        return string.Empty;
    }

    private sealed record ScoredCommand(GameCommand Command, int Score);

    private sealed record PreviewedCommand(
        GameCommand Command,
        int Score,
        string DecisionRuleCode = "KH-000",
        string DecisionRuleName = "Unlabelled preview");

    private enum CandidateClassification
    {
        Other,
        KillInnerOrSameRing,
        KillOuterSafe,
        Merge,
        MergeTowardObjective,
        Objective
    }

    private enum MergeOpportunity
    {
        NotAMerge,
        NotFavorable,
        DistanceOneFavorable,
        DistanceTwoFavorable,
        DistanceThreeOrMore
    }

    private enum KillOpportunity
    {
        NotAKill,
        NotFavorable,
        InnerOrSameRingFavorable,
        OuterRingFavorable
    }

    private sealed class SearchInstrumentation
    {
        public int LegalCommandCount { get; set; }

        public int CandidateCommandCount { get; set; }

        public int NodesVisited { get; set; }

        public int LeafEvaluations { get; set; }

        public bool TimeBudgetReached { get; set; }
    }

    private static MatchPhase GetMatchPhase(KingOfTheHillGameState state)
    {
        if (state.FindUnitAt(HexCoordinate.Origin) is not null)
        {
            var minimumAliveStrength = state.Players
                .Select(player => state.Units
                    .Where(unit => string.Equals(unit.OwnerPlayerId, player.Id, StringComparison.OrdinalIgnoreCase))
                    .Sum(unit => unit.Strength))
                .Min();

            if (minimumAliveStrength <= 10)
            {
                return MatchPhase.Endgame;
            }
        }

        return state.TurnNumber <= 4
            ? MatchPhase.Opening
            : MatchPhase.Midgame;
    }

    private enum MatchPhase
    {
        Opening,
        Midgame,
        Endgame
    }

}

internal sealed class KingOfTheHillAiLevel1Player : KingOfTheHillMinimaxAiPlayer
{
    protected override KingOfTheHillAiConfiguration Configuration { get; } = new(1, 0, 0.00, 1, 0, 0, 0.20, 0.00);

    public override PlayerControllerType ControllerType => PlayerControllerType.IaLevel1;
}

internal sealed class KingOfTheHillAiLevel2Player : KingOfTheHillMinimaxAiPlayer
{
    protected override KingOfTheHillAiConfiguration Configuration { get; } = new(1, 0, 0.00, 1, 0, 0, 0.45, 0.00);

    public override PlayerControllerType ControllerType => PlayerControllerType.IaLevel2;
}

internal sealed class KingOfTheHillAiLevel3Player : KingOfTheHillMinimaxAiPlayer
{
    protected override KingOfTheHillAiConfiguration Configuration { get; } = new(2, 0, 0.00, 6, 0, 0, 0.65, 0.50);

    public override PlayerControllerType ControllerType => PlayerControllerType.IaLevel3;
}

internal sealed class KingOfTheHillAiLevel4Player : KingOfTheHillMinimaxAiPlayer
{
    protected override KingOfTheHillAiConfiguration Configuration { get; } = new(3, 0, 0.00, 8, 0, 0, 1.00, 1.00);

    public override PlayerControllerType ControllerType => PlayerControllerType.IaLevel4;
}

internal static class KingOfTheHillAiMoveGenerator
{
    public static IReadOnlyList<GameCommand> GenerateLegalCommands(KingOfTheHillGameState state)
    {
        var commands = state.Units
            .Where(unit => unit.OwnerPlayerId == state.CurrentPlayerId)
            .SelectMany(unit =>
            {
                var movementDepth = unit.Strength == 1 ? 2 : 1;
                return state.Board
                    .GetReachableCoordinates(unit.Position, movementDepth)
                    .Select(target => CreateMoveCommand(unit.Id, target));
            })
            .Where(command => KingOfTheHillGameRules.Execute(state, command).Accepted)
            .Distinct()
            .ToList();

        var pass = new GameCommand("pass");
        if (KingOfTheHillGameRules.Execute(state, pass).Accepted)
        {
            commands.Add(pass);
        }

        return commands;
    }

    private static GameCommand CreateMoveCommand(string unitId, HexCoordinate target) =>
        new(
            "move",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unitId"] = unitId,
                ["q"] = target.Q.ToString(),
                ["r"] = target.R.ToString()
            });
}
