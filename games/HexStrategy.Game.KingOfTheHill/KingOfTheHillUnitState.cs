using HexStrategy.Core.Hexes;

namespace HexStrategy.Game.KingOfTheHill;

public sealed record KingOfTheHillUnitState(
    string Id,
    string OwnerPlayerId,
    HexCoordinate Position,
    IReadOnlyList<string> MemberUnitIds)
{
    public int Strength => MemberUnitIds.Count;

    public static KingOfTheHillUnitState CreateSingle(
        string id,
        string ownerPlayerId,
        HexCoordinate position) =>
        new(id, ownerPlayerId, position, [id]);
}
