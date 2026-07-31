using HexStrategy.Core.Commands;
using HexStrategy.Core.Contracts;
using HexStrategy.Core.Players;

namespace HexStrategy.Game.KingOfTheHill;

public sealed class KingOfTheHillGameDefinition : IGameDefinition, IAiCapableGameDefinition
{
    public const string GameDefinitionId = "king-of-the-hill";

    public GameDefinitionMetadata Metadata { get; } =
        new(GameDefinitionId, "King of the Hill");

    public IGameState CreateInitialState(IReadOnlyList<PlayerToken>? players = null) =>
        KingOfTheHillGameState.CreateDefault(players);

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

    public AutomatedDecisionResult ChooseAutomatedCommand(IGameState state, PlayerToken player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);

        if (state is not KingOfTheHillGameState gameState)
        {
            throw new ArgumentException("KingOfTheHillGameDefinition requires KingOfTheHillGameState.", nameof(state));
        }

        return KingOfTheHillAiController.ChooseCommand(gameState, player);
    }
}
