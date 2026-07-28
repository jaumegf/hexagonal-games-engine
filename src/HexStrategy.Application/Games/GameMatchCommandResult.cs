namespace HexStrategy.Application.Games;

public sealed record GameMatchCommandResult(
    bool Accepted,
    string Message,
    GameMatch Match);
