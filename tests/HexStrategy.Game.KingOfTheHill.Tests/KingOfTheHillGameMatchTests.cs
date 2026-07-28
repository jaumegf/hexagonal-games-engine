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
        Assert.Equal(4, state.Units.Count);
        Assert.Equal(0, state.ControlScores["P1"]);
        Assert.Equal(0, state.ControlScores["P2"]);
    }

    [Fact]
    public void Execute_LegalAdjacentMove_Succeeds()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("p1a", -1, 0));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Contains(state.Units, unit => unit.Id == "p1a" && unit.Position == new HexCoordinate(-1, 0));
    }

    [Fact]
    public void Execute_MoveIntoOccupiedCell_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("p1a", -2, 1));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P1", state.CurrentPlayerId);
        Assert.Contains("occupied", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_MoveOutsideBoard_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("p1a", -3, 0));

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
                .Select(unit => unit.Id == "p1a" ? unit with { Position = HexCoordinate.Origin } : unit)
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
                .Select(unit => unit.Id == "p1a" ? unit with { Position = HexCoordinate.Origin } : unit)
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
