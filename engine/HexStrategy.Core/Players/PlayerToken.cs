namespace HexStrategy.Core.Players;

public sealed record PlayerToken(
    string Id,
    string DisplayName,
    PlayerControllerType ControllerType)
{
    public PlayerKind Kind =>
        ControllerType == PlayerControllerType.Human
            ? PlayerKind.Human
            : PlayerKind.ArtificialIntelligence;
}
