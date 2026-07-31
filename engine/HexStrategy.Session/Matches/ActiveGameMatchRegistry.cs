using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Core.Contracts;
using HexStrategy.Core.Players;

namespace HexStrategy.Session.Matches;

public sealed class ActiveGameMatchRegistry
{
    private readonly Dictionary<Guid, ActiveGameMatch> matches = new();
    private readonly GameMatchService matchService;

    public ActiveGameMatchRegistry(GameMatchService matchService)
    {
        this.matchService = matchService;
    }

    public ActiveGameMatch Create(string gameDefinitionId, IReadOnlyList<PlayerToken>? players = null)
    {
        var match = matchService.StartNew(gameDefinitionId, players);
        var activeMatch = new ActiveGameMatch(match.MatchId, match, "Match created.");
        matches[activeMatch.MatchId] = activeMatch;
        return activeMatch;
    }

    public ActiveGameMatch Import(string gameDefinitionId, HexStrategy.Core.Contracts.IGameState state, string? lastMessage = null)
    {
        var match = matchService.Restore(gameDefinitionId, state);
        var activeMatch = new ActiveGameMatch(match.MatchId, match, string.IsNullOrWhiteSpace(lastMessage) ? "Match restored." : lastMessage);
        matches[activeMatch.MatchId] = activeMatch;
        return activeMatch;
    }

    public bool TryGet(Guid matchId, out ActiveGameMatch? match) =>
        matches.TryGetValue(matchId, out match);

    public ActiveGameMatch Execute(Guid matchId, GameCommand command)
    {
        if (!matches.TryGetValue(matchId, out var activeMatch) || activeMatch is null)
        {
            throw new KeyNotFoundException($"Match '{matchId}' was not found.");
        }

        var commandResult = matchService.Execute(activeMatch.Match, command);
        var updatedMatch = activeMatch with
        {
            Match = commandResult.Match,
            LastMessage = commandResult.Message,
            LastAutomatedDecisionTelemetry = null
        };

        matches[matchId] = updatedMatch;
        return updatedMatch;
    }

    public ActiveGameMatch ExecuteAutomatedTurn(Guid matchId)
    {
        if (!matches.TryGetValue(matchId, out var activeMatch) || activeMatch is null)
        {
            throw new KeyNotFoundException($"Match '{matchId}' was not found.");
        }

        if (activeMatch.Match.State is not ITurnBasedGameState turnState)
        {
            throw new InvalidOperationException("The current match does not expose turn-based state.");
        }

        if (turnState.IsCompleted)
        {
            throw new InvalidOperationException("The current match is already complete.");
        }

        if (turnState.CurrentPlayer.ControllerType == PlayerControllerType.Human)
        {
            throw new InvalidOperationException("The current player is human, so there is no automated turn to execute.");
        }

        if (activeMatch.Match.Definition is not IAiCapableGameDefinition aiDefinition)
        {
            throw new InvalidOperationException("The current game definition does not support automated players.");
        }

        var decision = aiDefinition.ChooseAutomatedCommand(activeMatch.Match.State, turnState.CurrentPlayer);
        var result = matchService.Execute(activeMatch.Match, decision.Command);

        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"Automated player '{turnState.CurrentPlayer.DisplayName}' generated an invalid command.");
        }

        var updatedMatch = activeMatch with
        {
            Match = result.Match,
            LastMessage = result.Message,
            LastAutomatedDecisionTelemetry = decision.Telemetry
        };

        matches[matchId] = updatedMatch;
        return updatedMatch;
    }
}
