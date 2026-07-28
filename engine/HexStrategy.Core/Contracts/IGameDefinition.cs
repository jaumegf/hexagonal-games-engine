namespace HexStrategy.Core.Contracts;

using HexStrategy.Core.Commands;

public interface IGameDefinition
{
    GameDefinitionMetadata Metadata { get; }

    IGameState CreateInitialState();

    GameCommandResult ExecuteCommand(IGameState state, GameCommand command);
}
