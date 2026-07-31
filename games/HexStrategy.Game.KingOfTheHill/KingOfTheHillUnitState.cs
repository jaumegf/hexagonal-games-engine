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

    public static KingOfTheHillUnitState CreateSeededBlock(
        string id,
        string ownerPlayerId,
        HexCoordinate position,
        int strength)
    {
        if (strength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be at least 1.");
        }

        if (strength == 1)
        {
            return CreateSingle(id, ownerPlayerId, position);
        }

        var memberIds = Enumerable
            .Range(1, strength)
            .Select(index => index == 1 ? id : $"{id}~{index}")
            .ToArray();

        return new(id, ownerPlayerId, position, memberIds);
    }
}
