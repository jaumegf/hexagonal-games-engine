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
            KingOfTheHillUnitState.CreateSingle("1A", "P1", new HexCoordinate(-2, 3)),
            KingOfTheHillUnitState.CreateSingle("1B", "P1", new HexCoordinate(-1, 3)),
            KingOfTheHillUnitState.CreateSingle("1C", "P1", new HexCoordinate(0, 3)),
            KingOfTheHillUnitState.CreateSingle("1D", "P1", new HexCoordinate(1, 3)),
            KingOfTheHillUnitState.CreateSingle("1E", "P1", new HexCoordinate(-1, 4)),
            KingOfTheHillUnitState.CreateSingle("2A", "P2", new HexCoordinate(2, -3)),
            KingOfTheHillUnitState.CreateSingle("2B", "P2", new HexCoordinate(1, -3)),
            KingOfTheHillUnitState.CreateSingle("2C", "P2", new HexCoordinate(0, -3)),
            KingOfTheHillUnitState.CreateSingle("2D", "P2", new HexCoordinate(-1, -3)),
            KingOfTheHillUnitState.CreateSingle("2E", "P2", new HexCoordinate(0, -4))
        };

        return new KingOfTheHillGameState(
            new HexBoard(CreateBoardCoordinates()),
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

    public KingOfTheHillUnitState? FindUnitAt(HexCoordinate coordinate) =>
        Units.SingleOrDefault(unit => unit.Position == coordinate);

    public bool IsOccupied(HexCoordinate coordinate) =>
        FindUnitAt(coordinate) is not null;

    private static IReadOnlyList<HexCoordinate> CreateBoardCoordinates() =>
        CreateCoordinatesFromRows(
            (-4, -1, 3),
            (-3, -1, 4),
            (-2, -2, 5),
            (-1, -2, 6),
            (0, -3, 7),
            (1, -3, 6),
            (2, -3, 5),
            (3, -2, 4),
            (4, -2, 3));

    private static IReadOnlyList<HexCoordinate> CreateCoordinatesFromRows(
        params (int r, int startQ, int count)[] rows) =>
        rows
            .SelectMany(row => Enumerable
                .Range(row.startQ, row.count)
                .Select(q => new HexCoordinate(q, row.r)))
            .ToArray();
}
