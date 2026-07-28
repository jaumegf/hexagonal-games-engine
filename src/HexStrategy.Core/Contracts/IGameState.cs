namespace HexStrategy.Core.Contracts;

public interface IGameState
{
    string GameDefinitionId { get; }

    string CurrentPlayerId { get; }

    int TurnNumber { get; }

    bool IsCompleted { get; }

    string? WinnerPlayerId { get; }
}
