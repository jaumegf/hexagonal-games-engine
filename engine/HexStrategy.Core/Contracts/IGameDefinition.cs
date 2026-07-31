namespace HexStrategy.Core.Contracts;

using HexStrategy.Core.Commands;
using HexStrategy.Core.Players;

public interface IGameDefinition
{
    GameDefinitionMetadata Metadata { get; }

    IGameState CreateInitialState(IReadOnlyList<PlayerToken>? players = null);

    GameCommandResult ExecuteCommand(IGameState state, GameCommand command);
}
