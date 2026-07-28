using HexStrategy.Core.Players;

namespace HexStrategy.Session.Sessions;

public sealed record PlayerSlotBinding(
    Guid SessionId,
    string SlotId,
    PlayerKind PlayerKind,
    string? ConnectionId);
