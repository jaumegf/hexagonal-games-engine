namespace HexStrategy.Core.Players;

public sealed record PlayerToken(
    string Id,
    string DisplayName,
    PlayerKind Kind);
