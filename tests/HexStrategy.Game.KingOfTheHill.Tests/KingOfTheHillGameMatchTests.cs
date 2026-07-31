using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Core.Players;
using HexStrategy.Core.Hexes;
using HexStrategy.Game.KingOfTheHill;
using HexStrategy.Session.Matches;
using System.Reflection;

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
        Assert.Equal(46, state.Units.Count);
        Assert.Equal(0, state.ControlScores["P1"]);
        Assert.Equal(0, state.ControlScores["P2"]);
        Assert.Equal(30, state.Units.Where(unit => unit.OwnerPlayerId == "P1").Sum(unit => unit.Strength));
        Assert.Equal(30, state.Units.Where(unit => unit.OwnerPlayerId == "P2").Sum(unit => unit.Strength));
        Assert.Contains(state.Units, unit => unit.Id == "1E" && unit.Position == new HexCoordinate(-5, 2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1F" && unit.Position == new HexCoordinate(-4, 2) && unit.Strength == 2);
        Assert.Contains(state.Units, unit => unit.Id == "1G" && unit.Position == new HexCoordinate(2, 2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1H" && unit.Position == new HexCoordinate(-5, 1) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1I" && unit.Position == new HexCoordinate(3, 1) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1J" && unit.Position == new HexCoordinate(-3, 4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1O" && unit.Position == new HexCoordinate(3, 4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1S" && unit.Position == new HexCoordinate(3, 3) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1T" && unit.Position == new HexCoordinate(1, -2) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "1U" && unit.Position == new HexCoordinate(3, 2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1V" && unit.Position == new HexCoordinate(-1, -1) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "1W" && unit.Position == new HexCoordinate(-6, 2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1X" && unit.Position == new HexCoordinate(2, -1) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "1B" && unit.Position == new HexCoordinate(-1, 4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1C" && unit.Position == new HexCoordinate(0, 4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "1D" && unit.Position == new HexCoordinate(1, 4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2E" && unit.Position == new HexCoordinate(5, -2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2F" && unit.Position == new HexCoordinate(4, -2) && unit.Strength == 2);
        Assert.Contains(state.Units, unit => unit.Id == "2G" && unit.Position == new HexCoordinate(-2, -2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2H" && unit.Position == new HexCoordinate(5, -1) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2I" && unit.Position == new HexCoordinate(-3, -1) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2J" && unit.Position == new HexCoordinate(3, -4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2O" && unit.Position == new HexCoordinate(-3, -4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2S" && unit.Position == new HexCoordinate(-2, -3) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2T" && unit.Position == new HexCoordinate(-1, 2) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "2U" && unit.Position == new HexCoordinate(-3, -2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2V" && unit.Position == new HexCoordinate(1, 1) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "2W" && unit.Position == new HexCoordinate(6, -2) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2X" && unit.Position == new HexCoordinate(-2, 1) && unit.Strength == 3);
        Assert.Contains(state.Units, unit => unit.Id == "2B" && unit.Position == new HexCoordinate(1, -4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2C" && unit.Position == new HexCoordinate(0, -4) && unit.Strength == 1);
        Assert.Contains(state.Units, unit => unit.Id == "2D" && unit.Position == new HexCoordinate(-1, -4) && unit.Strength == 1);
        Assert.DoesNotContain(new HexCoordinate(-2, -5), state.Board.Coordinates);
        Assert.DoesNotContain(new HexCoordinate(3, -5), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(6, -2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-6, 2), state.Board.Coordinates);
        Assert.DoesNotContain(new HexCoordinate(-3, 5), state.Board.Coordinates);
        Assert.DoesNotContain(new HexCoordinate(2, 5), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-3, -4), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(3, -4), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-3, 4), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(3, 4), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(3, -2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-3, -1), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-2, -2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-4, 2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-5, 1), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(-5, 3), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(3, 1), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(2, 2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(4, -2), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(5, -1), state.Board.Coordinates);
        Assert.Contains(new HexCoordinate(5, -3), state.Board.Coordinates);
        var rowSizes = state.Board.Coordinates
            .GroupBy(coordinate => coordinate.R)
            .OrderBy(group => group.Key)
            .Select(group => group.Count())
            .ToArray();

        Assert.Equal([4, 7, 6, 9, 8, 9, 8, 9, 6, 7, 4], rowSizes);
    }

    [Fact]
    public void StartNew_UsesConfiguredPlayerControllers()
    {
        var match = matchService.StartNew(
            KingOfTheHillGameDefinition.GameDefinitionId,
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel2));
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);

        Assert.Equal(PlayerControllerType.Human, state.Players.Single(player => player.Id == "P1").ControllerType);
        Assert.Equal(PlayerControllerType.IaLevel2, state.Players.Single(player => player.Id == "P2").ControllerType);
    }

    [Fact]
    public void DefenderThatCommitsIntoR1_PermanentlyLosesDefenderRole()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var afterCommit = matchService.Execute(match, Move("1X", 1, 0));
        var state = Assert.IsType<KingOfTheHillGameState>(afterCommit.Match.State);

        Assert.Contains(state.RetiredDefenderIds, unitId => unitId == "1X");
    }

    [Fact]
    public void DefenderRoleCollapses_WhenFewerThanTwoActiveDefendersRemain()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState());
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1T", "P1", new HexCoordinate(1, -2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("1V", "P1", new HexCoordinate(-1, -1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("1X", "P1", new HexCoordinate(2, -1), 3)
            },
            RetiredDefenderIds = new[] { "1T" },
            CurrentPlayerId = "P1",
            TurnNumber = 3,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var result = definition.ExecuteCommand(state, Move("1V", -1, 0));
        var nextState = Assert.IsType<KingOfTheHillGameState>(result.State);

        Assert.Contains(nextState.RetiredDefenderIds, unitId => unitId == "1V");
        Assert.Contains(nextState.RetiredDefenderIds, unitId => unitId == "1X");
    }

    [Fact]
    public void AutomatedDefinition_ChoosesLegalCommand_ForAiPlayer()
    {
        var definition = new KingOfTheHillGameDefinition();
        var state = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.IaLevel4, PlayerControllerType.Human)));

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);
        var result = definition.ExecuteCommand(state, decision.Command);

        Assert.True(result.Accepted);
        Assert.Equal(PlayerControllerType.IaLevel4, decision.Telemetry.ControllerType);
        Assert.True(decision.Telemetry.NodesVisited > 0);
    }

    [Fact]
    public void AutomatedDefinition_RetreatsFromObjective_WhenEliminationNextTurnIsLikely()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.IaLevel1, PlayerControllerType.Human)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSingle("1H", "P1", HexCoordinate.Origin),
                KingOfTheHillUnitState.CreateSeededBlock("2I", "P2", new HexCoordinate(-1, 0), 2)
            },
            CurrentPlayerId = "P1",
            TurnNumber = 1,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);
        var result = definition.ExecuteCommand(state, decision.Command);
        var nextState = Assert.IsType<KingOfTheHillGameState>(result.State);

        Assert.Equal("move", decision.Command.Name);
        Assert.True(result.Accepted);
        Assert.DoesNotContain(nextState.Units, unit => unit.OwnerPlayerId == "P1" && unit.Position == HexCoordinate.Origin);
    }

    [Fact]
    public void AutomatedDefinition_ObjectiveEmergencyRetreatScore_IsPositive_ForSafeExit()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.IaLevel1, PlayerControllerType.Human)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSingle("1H", "P1", HexCoordinate.Origin),
                KingOfTheHillUnitState.CreateSeededBlock("2I", "P2", new HexCoordinate(-1, 0), 2)
            },
            CurrentPlayerId = "P1"
        };

        var controllerType = typeof(KingOfTheHillGameDefinition).Assembly
            .GetType("HexStrategy.Game.KingOfTheHill.KingOfTheHillMinimaxAiPlayer", throwOnError: true)!;
        var method = controllerType.GetMethod(
            "EvaluateObjectiveEmergencyRetreatScore",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var holder = state.Units.Single(unit => unit.Id == "1H");
        var score = (int)method.Invoke(null, [state, Move("1H", 1, 0), holder])!;

        Assert.True(score > 0);
    }

    [Fact]
    public void AutomatedDefinition_DefenderInterceptsEnemyThatStepsIntoAdjacentR1()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(-1, 2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2V", "P2", new HexCoordinate(1, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2X", "P2", new HexCoordinate(-2, 1), 3),
                KingOfTheHillUnitState.CreateSingle("1A", "P1", new HexCoordinate(0, 1))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 2,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);

        Assert.Equal("move", decision.Command.Name);
        Assert.Equal("2T", decision.Command.GetRequiredArgument("unitId"));
        Assert.Equal("0", decision.Command.GetRequiredArgument("q"));
        Assert.Equal("1", decision.Command.GetRequiredArgument("r"));
        Assert.Equal("KH-085", decision.Telemetry.DecisionRuleCode);
    }

    [Fact]
    public void AutomatedDefinition_DefenderReturnsToR2_AfterEarlyInterception_WhenHillIsEmpty()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(0, 1), 2),
                KingOfTheHillUnitState.CreateSingle("2V", "P2", new HexCoordinate(1, 1)),
                KingOfTheHillUnitState.CreateSingle("1A", "P1", new HexCoordinate(-1, 0))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 2,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);

        Assert.Equal("move", decision.Command.Name);
        Assert.Equal("2T", decision.Command.GetRequiredArgument("unitId"));
        Assert.Equal("-1", decision.Command.GetRequiredArgument("q"));
        Assert.Equal("2", decision.Command.GetRequiredArgument("r"));
        Assert.Equal("KH-055", decision.Telemetry.DecisionRuleCode);
    }

    [Fact]
    public void AutomatedDefinition_DefenderDoesNotVoluntarilyEnterR1_WithoutAdjacentIntrusion()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(-1, 2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2V", "P2", new HexCoordinate(1, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2X", "P2", new HexCoordinate(-2, 1), 3),
                KingOfTheHillUnitState.CreateSingle("1A", "P1", new HexCoordinate(2, 1))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 2,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);

        Assert.False(
            string.Equals(decision.Command.Name, "move", StringComparison.OrdinalIgnoreCase) &&
            decision.Command.GetRequiredArgument("unitId") == "2T" &&
            decision.Command.GetRequiredArgument("q") == "0" &&
            decision.Command.GetRequiredArgument("r") == "1");
    }

    [Fact]
    public void AutomatedDefinition_DefenderLosesRole_WhenThreatenedByAdjacentS4OnR2()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(-1, 2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2V", "P2", new HexCoordinate(1, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2X", "P2", new HexCoordinate(-2, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(0, 2), 4),
                KingOfTheHillUnitState.CreateSeededBlock("1H", "P1", HexCoordinate.Origin, 3)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 2,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var controllerType = typeof(KingOfTheHillGameDefinition).Assembly
            .GetType("HexStrategy.Game.KingOfTheHill.KingOfTheHillMinimaxAiPlayer", throwOnError: true)!;
        var method = controllerType.GetMethod(
            "IsDefenderUnit",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var threatenedDefender = state.Units.Single(unit => unit.Id == "2T");
        var stillDefender = (bool)method.Invoke(null, [state, threatenedDefender, "P2"])!;

        Assert.False(stillDefender);
    }

    [Fact]
    public void AutomatedDefinition_DefenderPrioritizesOpeningLaneDenial_BeforeGenericFallback()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(-1, 2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2V", "P2", new HexCoordinate(1, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2X", "P2", new HexCoordinate(-2, 1), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2E", "P2", new HexCoordinate(5, -2), 1),
                KingOfTheHillUnitState.CreateSeededBlock("1F", "P1", new HexCoordinate(-3, 1), 2),
                KingOfTheHillUnitState.CreateSingle("1E", "P1", new HexCoordinate(-4, 2))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 2,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);

        Assert.Equal("move", decision.Command.Name);
        Assert.Equal("2X", decision.Command.GetRequiredArgument("unitId"));
        Assert.Equal("-3", decision.Command.GetRequiredArgument("q"));
        Assert.Equal("1", decision.Command.GetRequiredArgument("r"));
        Assert.Equal("KH-088", decision.Telemetry.DecisionRuleCode);
    }

    [Fact]
    public void AutomatedDefinition_PrefersObjectiveReinforcement_OverRetreat_WhenHoldCanBePreserved()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel1)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(0, 1), 4),
                KingOfTheHillUnitState.CreateSeededBlock("2G", "P2", HexCoordinate.Origin, 3),
                KingOfTheHillUnitState.CreateSingle("2D", "P2", new HexCoordinate(1, 0))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);
        var result = definition.ExecuteCommand(state, decision.Command);
        var nextState = Assert.IsType<KingOfTheHillGameState>(result.State);
        var unitOnObjective = Assert.Single(nextState.Units, unit => unit.OwnerPlayerId == "P2" && unit.Position == HexCoordinate.Origin);

        Assert.Equal("move", decision.Command.Name);
        Assert.Equal("2D", decision.Command.GetRequiredArgument("unitId"));
        Assert.Equal("0", decision.Command.GetRequiredArgument("q"));
        Assert.Equal("0", decision.Command.GetRequiredArgument("r"));
        Assert.True(result.Accepted);
        Assert.Equal(4, unitOnObjective.Strength);
    }

    [Fact]
    public void AutomatedDefinition_PrefersStrongerReserveOverSingleUnit_WhenSiegingObjective()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 5),
                KingOfTheHillUnitState.CreateSeededBlock("2C", "P2", new HexCoordinate(0, -3), 2),
                KingOfTheHillUnitState.CreateSingle("2E", "P2", new HexCoordinate(4, 0))
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);

        Assert.Equal("move", decision.Command.Name);
        Assert.NotEqual("2E", decision.Command.GetRequiredArgument("unitId"));
    }

    [Fact]
    public void AutomatedDefinition_PrefersR1OverrunSetup_WhenItCanCaptureObjectiveByAdjacentStrength()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 5),
                KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(-1, 0), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2B", "P2", new HexCoordinate(0, -2), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2F", "P2", new HexCoordinate(3, -2), 2)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var decision = definition.ChooseAutomatedCommand(state, state.CurrentPlayer);
        var result = definition.ExecuteCommand(state, decision.Command);
        var nextState = Assert.IsType<KingOfTheHillGameState>(result.State);
        var unitOnObjective = Assert.Single(nextState.Units, unit => unit.Position == HexCoordinate.Origin);

        Assert.Equal("move", decision.Command.Name);
        Assert.Equal("2B", decision.Command.GetRequiredArgument("unitId"));
        Assert.Equal("0", decision.Command.GetRequiredArgument("q"));
        Assert.Equal("-1", decision.Command.GetRequiredArgument("r"));
        Assert.True(result.Accepted);
        Assert.Equal("P2", unitOnObjective.OwnerPlayerId);
    }

    [Fact]
    public void AutomatedDefinition_Level4SiegeApproachScore_BeatsRedundantSiegeMerge()
    {
        var definition = new KingOfTheHillGameDefinition();
        var template = Assert.IsType<KingOfTheHillGameState>(definition.CreateInitialState(
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4)));
        var state = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 3),
                KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(0, -3), 4),
                KingOfTheHillUnitState.CreateSeededBlock("2B", "P2", new HexCoordinate(1, -2), 2),
                KingOfTheHillUnitState.CreateSeededBlock("2C", "P2", new HexCoordinate(1, -3), 2)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1,
            IsCompleted = false,
            WinnerPlayerId = null,
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            }
        };

        var controllerType = typeof(KingOfTheHillGameDefinition).Assembly
            .GetType("HexStrategy.Game.KingOfTheHill.KingOfTheHillMinimaxAiPlayer", throwOnError: true)!;
        var approachMethod = controllerType.GetMethod(
            "EvaluateLevelFourSiegeApproachScore",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var mergeMethod = controllerType.GetMethod(
            "EvaluateObjectiveSiegeMergeScore",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var strongApproachScore = (int)approachMethod.Invoke(null, [state, Move("2A", 0, -2)])!;
        var redundantMergeScore = (int)mergeMethod.Invoke(null, [state, Move("2C", 1, -2)])!;

        Assert.True(strongApproachScore > 0);
        Assert.True(strongApproachScore > redundantMergeScore);
    }

    [Fact]
    public void Registry_LeavesAiReplyPending_AfterHumanTurn()
    {
        var registry = new ActiveGameMatchRegistry(matchService);
        var activeMatch = registry.Create(
            KingOfTheHillGameDefinition.GameDefinitionId,
            CreatePlayers(PlayerControllerType.Human, PlayerControllerType.IaLevel4));

        var updatedMatch = registry.Execute(activeMatch.MatchId, Move("1B", -1, 2));
        var state = Assert.IsType<KingOfTheHillGameState>(updatedMatch.Match.State);

        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Equal(1, state.TurnNumber);
    }

    [Fact]
    public void Registry_ExecutesAutomatedTurn_WhenRequested()
    {
        var registry = new ActiveGameMatchRegistry(matchService);
        var activeMatch = registry.Create(
            KingOfTheHillGameDefinition.GameDefinitionId,
            CreatePlayers(PlayerControllerType.IaLevel4, PlayerControllerType.Human));
        var initialState = Assert.IsType<KingOfTheHillGameState>(activeMatch.Match.State);

        Assert.Equal("P1", initialState.CurrentPlayerId);

        var updatedMatch = registry.ExecuteAutomatedTurn(activeMatch.MatchId);
        var state = Assert.IsType<KingOfTheHillGameState>(updatedMatch.Match.State);

        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Equal(1, state.TurnNumber);
        Assert.NotNull(updatedMatch.LastAutomatedDecisionTelemetry);
    }

    [Fact]
    public void Execute_TurnNumberIncrementsOnlyAfterBothPlayersCompleteRound()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var afterFirstPlayer = matchService.Execute(match, Move("1B", -1, 2));
        var firstState = Assert.IsType<KingOfTheHillGameState>(afterFirstPlayer.Match.State);

        Assert.Equal("P2", firstState.CurrentPlayerId);
        Assert.Equal(1, firstState.TurnNumber);

        var afterSecondPlayer = matchService.Execute(afterFirstPlayer.Match, Move("2B", 1, -2));
        var secondState = Assert.IsType<KingOfTheHillGameState>(afterSecondPlayer.Match.State);

        Assert.Equal("P1", secondState.CurrentPlayerId);
        Assert.Equal(2, secondState.TurnNumber);
    }

    [Fact]
    public void Execute_LegalAdjacentMove_Succeeds()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1B", -1, 3));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Contains(state.Units, unit => unit.Id == "1B" && unit.Position == new HexCoordinate(-1, 3));
    }

    [Fact]
    public void Execute_UsesBoardGeometryForAdjacency()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var accepted = matchService.Execute(match, Move("1B", -1, 2));
        var rejected = matchService.Execute(match, Move("1B", 1, 1));

        Assert.True(accepted.Accepted);
        Assert.False(rejected.Accepted);
        Assert.Contains("movement range", rejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_SingleUnitCanMoveTwoHexesInOneTurn()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1B", -1, 2));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Contains(state.Units, unit => unit.Id == "1B" && unit.Position == new HexCoordinate(-1, 2));
    }

    [Fact]
    public void Execute_SingleUnitCannotMoveThroughOccupiedIntermediateHex()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var template = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSingle("1B", "P1", new HexCoordinate(-2, 0)),
                KingOfTheHillUnitState.CreateSingle("2D", "P2", new HexCoordinate(-1, -1)),
                KingOfTheHillUnitState.CreateSingle("2E", "P2", new HexCoordinate(-1, 0))
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1B", 0, -1));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P1", updatedState.CurrentPlayerId);
        Assert.Contains("no traversable path", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_StackedUnitCanMoveOnlyOneHex()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var initialState = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = initialState with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(-1, 3), 2),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(2, -3))
            },
            CurrentPlayerId = "P1"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1A", 1, 3));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P1", state.CurrentPlayerId);
        Assert.Contains("movement range", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_MoveIntoFriendlyOccupiedCell_MergesUnits()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1C", 1, 4));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", state.CurrentPlayerId);
        Assert.Equal(43, state.Units.Count);

        var mergedUnit = Assert.Single(state.Units, unit => unit.Position == new HexCoordinate(1, 4));
        Assert.Equal("1C", mergedUnit.Id);
        Assert.Equal(2, mergedUnit.Strength);
        Assert.Equal(["1C", "1D"], mergedUnit.MemberUnitIds);
        Assert.DoesNotContain(state.Units, unit => unit.Id == "1D");
    }

    [Fact]
    public void Execute_MergeThatWouldExceedMaximumBlockStrength_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(-1, 3), 3),
                KingOfTheHillUnitState.CreateSeededBlock("1C", "P1", new HexCoordinate(0, 3), 3),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(2, -3))
            },
            CurrentPlayerId = "P1"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1A", 0, 3));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P1", updatedState.CurrentPlayerId);
        Assert.Contains("cannot exceed S4", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_SingleUnitCanMergeIntoFriendlyUnitAtDepthTwo_WhenTraversablePathExists()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var template = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = template with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(-1, 1), 2),
                KingOfTheHillUnitState.CreateSingle("1F", "P1", new HexCoordinate(-2, 0)),
                KingOfTheHillUnitState.CreateSeededBlock("2D", "P2", new HexCoordinate(-1, -1), 2),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(1, -1))
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1F", -1, 1));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        var mergedUnit = Assert.Single(updatedState.Units, unit => unit.Position == new HexCoordinate(-1, 1));
        Assert.Equal("1A", mergedUnit.Id);
        Assert.Equal(3, mergedUnit.Strength);
        Assert.Equal(["1A", "1A~2", "1F"], mergedUnit.MemberUnitIds);
    }

    [Fact]
    public void Execute_MoveIntoEqualStrengthEnemy_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSingle("1B", "P1", HexCoordinate.Origin),
                KingOfTheHillUnitState.CreateSingle("2B", "P2", new HexCoordinate(1, 0))
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1B", 1, 0));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P1", updatedState.CurrentPlayerId);
        Assert.Contains("cannot defeat", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_StrongerBlockCanEliminateWeakerEnemy()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1B", "P1", HexCoordinate.Origin, 3),
                KingOfTheHillUnitState.CreateSingle("2B", "P2", new HexCoordinate(1, 0))
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("1B", 1, 0));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
        Assert.DoesNotContain(updatedState.Units, unit => unit.Id == "2B");
        Assert.Contains(updatedState.Units, unit =>
            unit.Id == "1B" &&
            unit.Position == new HexCoordinate(1, 0) &&
            unit.Strength == 3);
    }

    [Fact]
    public void Execute_ScreenshotCase_TwoCStrengthTwo_CanEliminateOneBStrengthOne()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedUnits = state.Units
            .Where(unit => unit.Id != "2D")
            .Select(unit => unit.Id switch
            {
                "2C" => unit with
                {
                    Position = new HexCoordinate(0, -2),
                    MemberUnitIds = ["2C", "2D"]
                },
                "1B" => unit with { Position = new HexCoordinate(0, -1) },
                _ => unit
            })
            .ToArray();
        var arrangedState = state with
        {
            Units = arrangedUnits,
            CurrentPlayerId = "P2"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("2C", 0, -1));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.DoesNotContain(updatedState.Units, unit => unit.Id == "1B");
        Assert.Contains(updatedState.Units, unit =>
            unit.Id == "2C" &&
            unit.Position == new HexCoordinate(0, -1) &&
            unit.Strength == 2);
    }

    [Fact]
    public void Execute_ObjectiveEntryBlocked_WhenAdjacentEnemyPressureEqualsMoverStrength()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = state.Units
                .Select(unit => unit.Id switch
                {
                    "2B" => unit with { Position = new HexCoordinate(1, -1) },
                    "1A" => unit with { Position = new HexCoordinate(-1, 1) },
                    "1C" => unit with { Position = new HexCoordinate(0, 1) },
                    _ => unit
                })
                .ToArray(),
            CurrentPlayerId = "P2"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("2B", 0, 0));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
        Assert.Contains("enemy pressure blocks access", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ObjectiveEntryAllowed_WhenMoverStrengthExceedsAdjacentEnemyPressure()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedUnits = state.Units
            .Where(unit => unit.Id != "2D")
            .Select(unit => unit.Id switch
            {
                "2C" => unit with
                {
                    Position = new HexCoordinate(0, -1),
                    MemberUnitIds = ["2C", "2D", "2E"]
                },
                "1A" => unit with { Position = new HexCoordinate(-1, 1), MemberUnitIds = ["1A"] },
                _ => unit
            })
            .ToArray();
        var arrangedState = state with
        {
            Units = arrangedUnits,
            CurrentPlayerId = "P2"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("2C", 0, 0));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.False(updatedState.IsCompleted);
        Assert.Equal("P1", updatedState.CurrentPlayerId);
        Assert.Contains(updatedState.Units, unit =>
            unit.Id == "2C" &&
            unit.Position == HexCoordinate.Origin &&
            unit.Strength == 3);
    }

    [Fact]
    public void Execute_CenterOccupiedByEnemy_CanBeCapturedByStrongerAttacker()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedUnits = state.Units
            .Where(unit => unit.Id != "1C")
            .Select(unit => unit.Id switch
            {
                "1B" => unit with
                {
                    Position = HexCoordinate.Origin,
                    MemberUnitIds = ["1B", "1C"]
                },
                "2C" => unit with { Position = new HexCoordinate(0, -1) },
                _ => unit
            })
            .ToArray();
        var arrangedState = state with
        {
            Units = arrangedUnits,
            CurrentPlayerId = "P2"
        };

        var result = matchService.Execute(match with { State = arrangedState }, Move("2C", 0, 0));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.False(result.Accepted);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
        Assert.Contains("cannot defeat", result.Message, StringComparison.OrdinalIgnoreCase);

        var strongerAttackerState = arrangedState with
        {
            Units = arrangedState.Units
                .Where(unit => unit.Id != "2D" && unit.Id != "2E")
                .Select(unit => unit.Id == "2C"
                    ? unit with
                    {
                        Position = new HexCoordinate(0, -1),
                        MemberUnitIds = ["2C", "2D", "2E"]
                    }
                    : unit)
                .ToArray()
        };

        var strongerResult = matchService.Execute(match with { State = strongerAttackerState }, Move("2C", 0, 0));
        var strongerUpdatedState = Assert.IsType<KingOfTheHillGameState>(strongerResult.Match.State);

        Assert.True(strongerResult.Accepted);
        Assert.DoesNotContain(strongerUpdatedState.Units, unit => unit.Id == "1B");
        Assert.Contains(strongerUpdatedState.Units, unit =>
            unit.Id == "2C" &&
            unit.Position == HexCoordinate.Origin &&
            unit.Strength == 3);
    }

    [Fact]
    public void Execute_MergeKeepsLowestAlphabeticalReference()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1C", 1, 4));
        var state = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        var mergedUnit = Assert.Single(state.Units, unit => unit.Position == new HexCoordinate(1, 4));
        Assert.Equal("1C", mergedUnit.Id);
        Assert.Equal(["1C", "1D"], mergedUnit.MemberUnitIds);
    }

    [Fact]
    public void Execute_MergedBlockCanMergeAgain()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var initialState = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = initialState with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(-1, 3), 2),
                KingOfTheHillUnitState.CreateSingle("1C", "P1", new HexCoordinate(0, 3)),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(2, -3))
            },
            CurrentPlayerId = "P1"
        };

        var secondMerge = matchService.Execute(match with { State = arrangedState }, Move("1A", 0, 3));
        var finalState = Assert.IsType<KingOfTheHillGameState>(secondMerge.Match.State);

        Assert.True(secondMerge.Accepted);
        Assert.Equal(2, finalState.Units.Count);

        var mergedUnit = Assert.Single(finalState.Units, unit => unit.Position == new HexCoordinate(0, 3));
        Assert.Equal("1A", mergedUnit.Id);
        Assert.Equal(3, mergedUnit.Strength);
        Assert.Equal(["1A", "1A~2", "1C"], mergedUnit.MemberUnitIds);
    }

    [Fact]
    public void AiMoveGenerator_ExcludesMergesThatWouldExceedMaximumBlockStrength()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", new HexCoordinate(-1, 3), 3),
                KingOfTheHillUnitState.CreateSeededBlock("1C", "P1", new HexCoordinate(0, 3), 3),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(2, -3))
            },
            CurrentPlayerId = "P1"
        };

        var moveGeneratorType = typeof(KingOfTheHillGameDefinition).Assembly
            .GetType("HexStrategy.Game.KingOfTheHill.KingOfTheHillAiMoveGenerator", throwOnError: true)!;
        var method = moveGeneratorType.GetMethod("GenerateLegalCommands", BindingFlags.Public | BindingFlags.Static)!;
        var commands = Assert.IsAssignableFrom<IReadOnlyList<GameCommand>>(method.Invoke(null, [arrangedState])!);

        Assert.DoesNotContain(commands, command =>
            string.Equals(command.Name, "move", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(command.GetRequiredArgument("unitId"), "1A", StringComparison.OrdinalIgnoreCase) &&
            command.GetRequiredArgument("q") == "0" &&
            command.GetRequiredArgument("r") == "3");
    }

    [Fact]
    public void Execute_MoveOutsideBoard_Fails()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

        var result = matchService.Execute(match, Move("1B", -1, 6));

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
                .Select(unit => unit.Id == "1B" ? unit with { Position = HexCoordinate.Origin } : unit)
                .ToArray()
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.Equal(1, updatedState.ControlScores["P1"]);
        Assert.False(updatedState.IsCompleted);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
    }

    [Fact]
    public void Execute_Pass_TriggersAutomaticObjectiveAssault_WhenAdjacentStrengthExceedsDefender()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 5),
                KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(-1, 0), 3),
                KingOfTheHillUnitState.CreateSeededBlock("2B", "P2", new HexCoordinate(1, -1), 4)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);
        var unitOnObjective = Assert.Single(updatedState.Units, unit => unit.Position == HexCoordinate.Origin);

        Assert.True(result.Accepted);
        Assert.Equal("P2", unitOnObjective.OwnerPlayerId);
        Assert.Equal(2, unitOnObjective.Strength);
        Assert.DoesNotContain(updatedState.Units, unit => unit.Id == "1A");
        Assert.DoesNotContain(updatedState.Units, unit => unit.Id == "2B");
        Assert.Equal(["2A", "2A~2"], unitOnObjective.MemberUnitIds);
        Assert.Equal(1, updatedState.ControlScores["P2"]);
        Assert.Contains("overruns the hill", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_Pass_DoesNotTriggerAutomaticObjectiveAssault_WhenAdjacentStrengthOnlyEqualsDefender()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 5),
                KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(-1, 0), 2),
                KingOfTheHillUnitState.CreateSeededBlock("2B", "P2", new HexCoordinate(1, -1), 3)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);
        var unitOnObjective = Assert.Single(updatedState.Units, unit => unit.Position == HexCoordinate.Origin);

        Assert.True(result.Accepted);
        Assert.Equal("P1", unitOnObjective.OwnerPlayerId);
        Assert.Equal(5, unitOnObjective.Strength);
        Assert.Equal(0, updatedState.ControlScores["P2"]);
        Assert.DoesNotContain(result.Message, "overruns the hill", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_Pass_DoesNotApplySiegePressure_WhenObjectiveAndAdjacentDefenseMatchPressure()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 2),
                KingOfTheHillUnitState.CreateSeededBlock("1B", "P1", new HexCoordinate(1, 0), 2),
                KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(-1, 0), 3)
            },
            CurrentPlayerId = "P2",
            TurnNumber = 1
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);
        var objectiveHolder = Assert.Single(updatedState.Units, unit => unit.Position == HexCoordinate.Origin);

        Assert.True(result.Accepted);
        Assert.Equal("P1", objectiveHolder.OwnerPlayerId);
        Assert.Equal(2, objectiveHolder.Strength);
        Assert.DoesNotContain("siege pressure", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ScoreLeadDoesNotEndMatch_WhenTrailingPlayerStillHasEnoughStrengthToRetakeObjective()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
            {
                Units = new[]
                {
                    KingOfTheHillUnitState.CreateSingle("1A", "P1", HexCoordinate.Origin),
                    KingOfTheHillUnitState.CreateSeededBlock("2A", "P2", new HexCoordinate(0, -1), 2)
                },
                CurrentPlayerId = "P1",
                ControlScores = new Dictionary<string, int>
                {
                    ["P1"] = 9,
                    ["P2"] = 0
                }
            };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.False(updatedState.IsCompleted);
        Assert.Equal("P2", updatedState.CurrentPlayerId);
        Assert.Equal(10, updatedState.ControlScores["P1"]);
    }

    [Fact]
    public void Execute_ScoreLeadEndsMatch_WhenTrailingPlayerLacksEnoughStrengthToRetakeObjective()
    {
        var match = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);
        var state = Assert.IsType<KingOfTheHillGameState>(match.State);
        var arrangedState = state with
        {
            Units = new[]
            {
                KingOfTheHillUnitState.CreateSeededBlock("1A", "P1", HexCoordinate.Origin, 4),
                KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(4, -2))
            },
            CurrentPlayerId = "P1",
            ControlScores = new Dictionary<string, int>
            {
                ["P1"] = 3,
                ["P2"] = 0
            }
        };

        var result = matchService.Execute(match with { State = arrangedState }, new GameCommand("pass"));
        var updatedState = Assert.IsType<KingOfTheHillGameState>(result.Match.State);

        Assert.True(result.Accepted);
        Assert.True(updatedState.IsCompleted);
        Assert.Equal("P1", updatedState.WinnerPlayerId);
        Assert.Equal(4, updatedState.ControlScores["P1"]);
        Assert.Contains("cannot exceed the strength on Objective", result.Message, StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<PlayerToken> CreatePlayers(
        PlayerControllerType player1Controller,
        PlayerControllerType player2Controller) =>
        new[]
        {
            new PlayerToken("P1", "Player 1", player1Controller),
            new PlayerToken("P2", "Player 2", player2Controller)
        };

}
