using HexStrategy.Core.Contracts;
using HexStrategy.Core.Hexes;
using HexStrategy.Core.Players;

namespace HexStrategy.Game.KingOfTheHill;

public sealed record KingOfTheHillGameState(
    HexBoard Board,
    IReadOnlyList<PlayerToken> Players,
    IReadOnlyList<KingOfTheHillUnitState> Units,
    IReadOnlyDictionary<string, int> ControlScores,
    string CurrentPlayerId,
    int TurnNumber,
    bool IsCompleted,
    string? WinnerPlayerId) : IGameState
{
    public string GameDefinitionId => KingOfTheHillGameDefinition.GameDefinitionId;

    public HexCoordinate ObjectiveCoordinate => HexCoordinate.Origin;

    public static KingOfTheHillGameState CreateDefault()
    {
        var players = new[]
        {
            new PlayerToken("P1", "Player 1", PlayerKind.Human),
            new PlayerToken("P2", "Player 2", PlayerKind.Human)
        };

        var units = new[]
        {
            new KingOfTheHillUnitState("p1a", "P1", new HexCoordinate(-2, 0)),
            new KingOfTheHillUnitState("p1b", "P1", new HexCoordinate(-2, 1)),
            new KingOfTheHillUnitState("p2a", "P2", new HexCoordinate(2, 0)),
            new KingOfTheHillUnitState("p2b", "P2", new HexCoordinate(2, -1))
        };

        return new KingOfTheHillGameState(
            new HexBoard(radius: 2),
            players,
            units,
            new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            CurrentPlayerId: "P1",
            TurnNumber: 1,
            IsCompleted: false,
            WinnerPlayerId: null);
    }

    public PlayerToken CurrentPlayer => Players.Single(player => player.Id == CurrentPlayerId);

    public KingOfTheHillUnitState? FindUnit(string unitId) =>
        Units.SingleOrDefault(unit => string.Equals(unit.Id, unitId, StringComparison.OrdinalIgnoreCase));

    public bool IsOccupied(HexCoordinate coordinate) =>
        Units.Any(unit => unit.Position == coordinate);
}
