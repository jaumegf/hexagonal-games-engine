using HexStrategy.Core.Commands;
using HexStrategy.Core.Contracts;

namespace HexStrategy.Game.KingOfTheHill;

public sealed class KingOfTheHillGameDefinition : IGameDefinition
{
    public const string GameDefinitionId = "king-of-the-hill";

    public GameDefinitionMetadata Metadata { get; } =
        new(GameDefinitionId, "King of the Hill");

    public IGameState CreateInitialState() => KingOfTheHillGameState.CreateDefault();

    public GameCommandResult ExecuteCommand(IGameState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (state is not KingOfTheHillGameState gameState)
        {
            throw new ArgumentException("KingOfTheHillGameDefinition requires KingOfTheHillGameState.", nameof(state));
        }

        return KingOfTheHillGameRules.Execute(gameState, command);
    }
}
