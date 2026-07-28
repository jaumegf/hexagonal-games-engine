using HexStrategy.Core.Hexes;

namespace HexStrategy.Game.KingOfTheHill;

public sealed record KingOfTheHillUnitState(
    string Id,
    string OwnerPlayerId,
    HexCoordinate Position);
