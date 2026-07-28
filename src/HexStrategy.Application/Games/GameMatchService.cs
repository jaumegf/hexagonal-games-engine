using HexStrategy.Core.Commands;

namespace HexStrategy.Application.Games;

public sealed class GameMatchService
{
    private readonly GameCatalog gameCatalog;

    public GameMatchService(GameCatalog gameCatalog)
    {
        this.gameCatalog = gameCatalog;
    }

    public GameMatch StartNew(string gameDefinitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDefinitionId);

        if (!gameCatalog.TryGet(gameDefinitionId, out var gameDefinition) || gameDefinition is null)
        {
            throw new InvalidOperationException(
                $"Game definition '{gameDefinitionId}' is not registered.");
        }

        return new GameMatch(Guid.NewGuid(), gameDefinition, gameDefinition.CreateInitialState());
    }

    public GameMatchCommandResult Execute(GameMatch match, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);

        var commandResult = match.Definition.ExecuteCommand(match.State, command);
        var updatedMatch = match with { State = commandResult.State };

        return new GameMatchCommandResult(commandResult.Accepted, commandResult.Message, updatedMatch);
    }
}
