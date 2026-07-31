using HexStrategy.Core.Commands;
using HexStrategy.Core.Players;

namespace HexStrategy.Application.Games;

public sealed class GameMatchService
{
    private readonly GameCatalog gameCatalog;

    public GameMatchService(GameCatalog gameCatalog)
    {
        this.gameCatalog = gameCatalog;
    }

    public GameMatch StartNew(string gameDefinitionId, IReadOnlyList<PlayerToken>? players = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDefinitionId);

        var gameDefinition = ResolveDefinition(gameDefinitionId);

        return new GameMatch(Guid.NewGuid(), gameDefinition, gameDefinition.CreateInitialState(players));
    }

    public GameMatch Restore(string gameDefinitionId, Core.Contracts.IGameState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDefinitionId);
        ArgumentNullException.ThrowIfNull(state);

        var gameDefinition = ResolveDefinition(gameDefinitionId);

        if (!string.Equals(state.GameDefinitionId, gameDefinitionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Snapshot game '{state.GameDefinitionId}' does not match requested game '{gameDefinitionId}'.");
        }

        return new GameMatch(Guid.NewGuid(), gameDefinition, state);
    }

    public GameMatchCommandResult Execute(GameMatch match, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);

        var commandResult = match.Definition.ExecuteCommand(match.State, command);
        var updatedMatch = match with { State = commandResult.State };

        return new GameMatchCommandResult(commandResult.Accepted, commandResult.Message, updatedMatch);
    }

    private Core.Contracts.IGameDefinition ResolveDefinition(string gameDefinitionId)
    {
        if (!gameCatalog.TryGet(gameDefinitionId, out var gameDefinition) || gameDefinition is null)
        {
            throw new InvalidOperationException(
                $"Game definition '{gameDefinitionId}' is not registered.");
        }

        return gameDefinition;
    }
}
