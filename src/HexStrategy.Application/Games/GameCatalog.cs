using HexStrategy.Core.Contracts;

namespace HexStrategy.Application.Games;

public sealed class GameCatalog
{
    private readonly IReadOnlyDictionary<string, IGameDefinition> definitionsById;

    public GameCatalog(IEnumerable<IGameDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        definitionsById = definitions.ToDictionary(
            definition => definition.Metadata.Id,
            definition => definition,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IGameDefinition> Definitions => definitionsById.Values.ToArray();

    public bool TryGet(string gameDefinitionId, out IGameDefinition? gameDefinition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDefinitionId);
        return definitionsById.TryGetValue(gameDefinitionId, out gameDefinition);
    }
}
