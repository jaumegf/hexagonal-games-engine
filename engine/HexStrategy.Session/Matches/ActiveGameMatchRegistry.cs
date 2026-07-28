using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;

namespace HexStrategy.Session.Matches;

public sealed class ActiveGameMatchRegistry
{
    private readonly Dictionary<Guid, ActiveGameMatch> matches = new();
    private readonly GameMatchService matchService;

    public ActiveGameMatchRegistry(GameMatchService matchService)
    {
        this.matchService = matchService;
    }

    public ActiveGameMatch Create(string gameDefinitionId)
    {
        var match = matchService.StartNew(gameDefinitionId);
        var activeMatch = new ActiveGameMatch(match.MatchId, match, "Match created.");
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
            LastMessage = commandResult.Message
        };

        matches[matchId] = updatedMatch;
        return updatedMatch;
    }
}
