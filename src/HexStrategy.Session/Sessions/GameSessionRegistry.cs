using HexStrategy.Application.Games;

namespace HexStrategy.Session.Sessions;

public sealed class GameSessionRegistry
{
    private readonly Dictionary<Guid, GameSessionRecord> sessions = new();
    private readonly GameCatalog gameCatalog;

    public GameSessionRegistry(GameCatalog gameCatalog)
    {
        this.gameCatalog = gameCatalog;
    }

    public IReadOnlyCollection<GameSessionRecord> Sessions => sessions.Values;

    public GameSessionRecord Create(string gameDefinitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDefinitionId);

        if (!gameCatalog.TryGet(gameDefinitionId, out _))
        {
            throw new InvalidOperationException(
                $"Cannot create a session for unregistered game definition '{gameDefinitionId}'.");
        }

        var session = new GameSessionRecord(Guid.NewGuid(), gameDefinitionId, DateTimeOffset.UtcNow);
        sessions.Add(session.SessionId, session);
        return session;
    }
}
