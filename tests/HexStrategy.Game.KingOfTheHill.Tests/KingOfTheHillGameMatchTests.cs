using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Core.Hexes;
using HexStrategy.Game.KingOfTheHill;

namespace HexStrategy.Game.KingOfTheHill.Tests;

public sealed class KingOfTheHillGameMatchTests
{
    private readonly GameMatchService matchService;

    public KingOfTheHillGameMatchTests()
    {
        var catalog = new GameCatalog(new[] { new KingOfTheHillGameDefinition() });
        matchService = new GameMatchService(catalog);
    }

    [Fact]
    public void StartNew_InitializesExpectedState()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);

        Assert.Equal("P1", state.CurrentPlayerId);
        Assert.Equal(10, state.Units.Count);
        Assert.Equal(0, state.ControlScores["P1"]);
        Assert.Equal(0, state.ControlScores["P2"]);
        Assert.Contains(state.Units, unit => unit.Id == "1A" && unit.Position == new HexCoordinate(-2, 3));
        Assert.Contains(state.Units, unit => unit.Id == "2E" && unit.Position == new HexCoordinate(0, -4));
        Assert.All(state.Units, unit => Assert.Equal(1, unit.Strength));
        var rowSizes = state.Board.Coordinates
            .GroupBy(coordinate => coordinate.R)
            .OrderBy(group => group.Key)
            .Select(group => group.Count())
            .ToArray();

        Assert.Equal([3, 4, 5, 6, 7, 6, 5, 4, 3], rowSizes);
    }

    [Fact]
    public void Execute_LegalAdjacentMove_Succeeds()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1A", -2, 2));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Contains(state.Units, unit => unit.Id == "1A" && unit.Position == new HexCoordinate(-2, 2));
    }

    [Fact]
    public void Execute_UsesBoardGeometryForAdjacency()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var accepted = matchService.Execute(match, Move("1A", -3, 2));
        var rejected = matchService.Execute(match, Move("1A", -1, 2));

        Assert.True(accepted.Accepted);
        Assert.False(rejected.Accepted);
        Assert.Contains("not adjacent", rejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_MoveIntoFriendlyOccupiedCell_MergesUnits()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1A", -1, 3));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Equal(9, state.Units.Count);

        var mergedUnit = Assert.Single(state.Units, unit => unit.Position == new HexCoordinate(-1, 3));
        Assert.Equal("1A", mergedUnit.Id);
        Assert.Equal(2, mergedUnit.Strength);
        Assert.Equal(["1A", "1B"], mergedUnit.MemberUnitIds);
        Assert.DoesNotContain(state.Units, unit => unit.Id == "1B");
    }

    [Fact]
    public void Execute_MergeKeepsLowestAlphabeticalReference()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1B", -2, 3));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        var mergedUnit = Assert.Single(state.Units, unit => unit.Position == new HexCoordinate(-2, 3));
        Assert.Equal("1A", mergedUnit.Id);
        Assert.Equal(["1A", "1B"], mergedUnit.MemberUnitIds);
    }

    [Fact]
    public void Execute_MergedBlockCanMergeAgain()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var firstMerge = matchService.Execute(match, Move("1A", -1, 3));
        var mergedState = Assert.IsType<KingOfTheHillGameState>(firstMerge.Match.State);
        var arrangedState = mergedState with { CurrentPlayerId = "P1" };

        var secondMerge = matchService.Execute(firstMerge.Match with { State = arrangedState }, Move("1A", 0, 3));
        var state = Assert.IsType<KingOfTheHillGameState>(secondMerge.Match.State);

        Assert.True(secondMerge.Accepted);
        Assert.Equal(8, state.Units.Count);

        var mergedUnit = Assert.Single(state.Units, unit => unit.Position == new HexCoordinate(0, 3));
        Assert.Equal("1A", mergedUnit.Id);
        Assert.Equal(3, mergedUnit.Strength);
        Assert.Equal(["1A", "1B", "1C"], mergedUnit.MemberUnitIds);
    }

    [Fact]
    public void Execute_MoveOutsideBoard_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1A", -3, 3));

        Assert.False(result.Accepted);
        Assert.Contains("outside the board", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_EndTurnOnCenter_IncrementsControlScore()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = state.Units
                .Select(unit => unit.Id == "1A" ? unit with { Position = HexCoordinate.Origin } : unit)
                .ToArray()
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal(1, updatedState.ControlScores["P1"]);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
    }

    [Fact]
    public void Execute_ReachingThreeControlPoints_DeclaresWinner()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = state.Units
                .Select(unit => unit.Id == "1A" ? unit with { Position = HexCoordinate.Origin } : unit)
                .ToArray(),
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 2,
                ["P2"] = 0
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.True(updatedState.IsCompleted);
        Assert.Equal("P1", updatedState.WinnerPlayerId);
        Assert.Equal(3, updatedState.ControlScores["P1"]);
    }

    private static GameCommand Move(string unitId, int q, int r) =>
        new(
            "move",
            new Dictionary<string, string>
            {
                ["unitId"] = unitId,
                ["q"] = q.ToString(),
                ["r"] = r.ToString()
            });

}
