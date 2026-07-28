namespace HexStrategy.Session.Sessions;

public sealed record GameSessionRecord(
    Guid SessionId,
    string GameDefinitionId,
    DateTimeOffset CreatedAtUtc);
