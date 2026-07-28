using HexStrategy.Core.Contracts;

namespace HexStrategy.Core.Commands;

public sealed record GameCommandResult(
    bool Accepted,
    string Message,
    IGameState State)
{
    public static GameCommandResult Success(IGameState state, string message) =>
        new(true, message, state);

    public static GameCommandResult Rejected(IGameState state, string message) =>
        new(false, message, state);
}
